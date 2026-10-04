using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

/// <summary>How a refund attempt ended.</summary>
public enum PaymentRefundResult
{
    /// <summary>The order carries no captured payment - there is nothing to give back.</summary>
    NothingToRefund,

    /// <summary>The money is on its way back to the customer (Razorpay reports the refund as processed).</summary>
    Refunded,

    /// <summary>Razorpay accepted the refund but it is still travelling back.</summary>
    Pending,

    /// <summary>The refund was refused or the gateway could not be reached - the money is still owed.</summary>
    Failed
}

/// <summary>
/// Gives a captured payment back through Razorpay's REST API and writes the result onto the order's
/// payment row (OrderPayments). This is what turns "cancel the order" into "and the money goes back"
/// instead of leaving the customer to e-mail support.
///
/// The refund is written down on the payment row itself - refund id, Razorpay's refund status, the
/// amount and, when it fails, why - so a refusal is visible to the shop team and a refund can never
/// be sent twice: only a row that still holds money (Captured, or the customer's own
/// RefundRequested, or a RefundFailed one being retried) is ever refunded, and a row that already
/// carries a refund id is turned away as "already refunded".
///
/// A customer's cancellation does not call this: it only ASKS for the refund (the payment row is
/// left as RefundRequested), and the shop team approves it from the admin order screen - which is
/// when <see cref="RefundOrderPaymentAsync"/> runs. <see cref="RecordManualRefundAsync"/> closes the
/// books on a refund the shop team made in the Razorpay dashboard itself.
/// </summary>
public static class RazorpayRefunds
{
    private static readonly HttpClient Http = new();

    /// <summary>
    /// RefundStatus written when the shop team refunded in the Razorpay dashboard itself: there is no
    /// refund id from us, so this is how a hand-made refund is told apart from one the app sent.
    /// </summary>
    public const string ManualRefundStatus = "manual";

    /// <summary>True when both Razorpay credentials are present (the gateway can be used).</summary>
    public static bool IsConfigured(string keyId, string keySecret) =>
        !string.IsNullOrWhiteSpace(keyId) && !string.IsNullOrWhiteSpace(keySecret);

    /// <summary>Razorpay's own words for how a refund ended (the <c>status</c> of a refund entity).</summary>
    public const string RefundStatusProcessed = "processed";

    /// <summary>Sent, but still travelling back to the customer's bank.</summary>
    public const string RefundStatusPending = "pending";

    /// <summary>Razorpay refused it (or the bank returned it) - the money is still owed.</summary>
    public const string RefundStatusFailed = "failed";

    /// <summary>
    /// True while the gateway still has something to say about a refund: one was sent and Razorpay has not
    /// finished with it. A refund travels for a day or two - it is answered 'pending' and settles into
    /// 'processed' together with the bank's own reference - so a row that is not yet settled, or one that is
    /// settled but has no bank reference yet, is worth asking about. The question is dropped a month on: a
    /// gateway that never reports a reference (some payment methods do not) must not be asked forever. A
    /// refund Razorpay refused, and one the shop team made by hand, have nothing left to hear at all.
    /// </summary>
    public static bool RefundInFlight(OrderPayment payment) =>
        !string.IsNullOrEmpty(payment.RefundId)
        && !string.Equals(payment.RefundStatus, RefundStatusFailed, StringComparison.OrdinalIgnoreCase)
        && (!string.Equals(payment.RefundStatus, RefundStatusProcessed, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrEmpty(payment.RefundArn))
        && (payment.RefundedOn == null || payment.RefundedOn > DateTime.UtcNow.AddDays(-30));

    /// <summary>
    /// Writes what Razorpay said about a refund onto the payment row: the refund's own status, the amount it
    /// really gave back, the bank's reference (<c>acquirer_data.arn</c>) and how the money was sent
    /// (<c>speed_processed</c>). Shared by the three places a refund is heard about - the refund call itself
    /// (<see cref="RefundOrderPaymentAsync"/>), the refund webhook (see
    /// <c>OrderService.HandlePaymentWebhookAsync</c>) and the daily gateway sync
    /// (<see cref="RefreshPendingRefundsAsync"/>) - so all three read one payload one way. Returns true when
    /// anything on the row changed.
    /// </summary>
    public static bool ApplyRefundOutcome(OrderPayment payment, JsonElement refund, DateTime? seenOn = null)
    {
        var now = DateTime.UtcNow;
        var changed = false;

        var refundId = ReadString(refund, "id");
        if (!string.IsNullOrEmpty(refundId) && !string.Equals(payment.RefundId, refundId, StringComparison.Ordinal))
        {
            payment.RefundId = Truncate(refundId, 50);
            changed = true;
        }

        var status = ReadString(refund, "status");
        if (!string.IsNullOrEmpty(status) && !string.Equals(payment.RefundStatus, status, StringComparison.Ordinal))
        {
            payment.RefundStatus = Truncate(status, 20);
            changed = true;
        }

        // What was really given back. The refund is always for the whole payment here, but the figure the
        // gateway reports is the one the bank saw, so it is what the books are kept on.
        var amountInPaise = ReadInt(refund, "amount");
        if (amountInPaise.HasValue && amountInPaise.Value > 0 && payment.RefundAmountInPaise != amountInPaise)
        {
            payment.RefundAmountInPaise = amountInPaise;
            payment.RefundAmount = amountInPaise.Value / 100m;
            changed = true;
        }

        // The bank's own reference for the money going back, so a customer who cannot find the refund can be
        // answered with something their bank recognises. It only exists once the refund has settled.
        var arn = ReadString(refund, "acquirer_data", "arn") ?? ReadString(refund, "arn");
        if (!string.IsNullOrEmpty(arn) && !string.Equals(payment.RefundArn, arn, StringComparison.Ordinal))
        {
            payment.RefundArn = Truncate(arn, 50);
            changed = true;
        }

        var speed = ReadString(refund, "speed_processed");
        if (!string.IsNullOrEmpty(speed) && !string.Equals(payment.RefundSpeedProcessed, speed, StringComparison.Ordinal))
        {
            payment.RefundSpeedProcessed = Truncate(speed, 20);
            changed = true;
        }

        // Razorpay's own created_at is the day the money went back - a better answer than the day the shop
        // team pressed the button, which can be a retry days later.
        var refundedOn = ReadTimestamp(refund, "created_at") ?? seenOn;
        if (refundedOn.HasValue && payment.RefundedOn == null)
        {
            payment.RefundedOn = refundedOn.Value;
            changed = true;
        }

        if (string.Equals(status, RefundStatusProcessed, StringComparison.OrdinalIgnoreCase) &&
            payment.Status != OrderPaymentStatus.Refunded)
        {
            payment.Status = OrderPaymentStatus.Refunded;
            payment.RefundFailureReason = null;
            changed = true;
        }
        else if (string.Equals(status, RefundStatusFailed, StringComparison.OrdinalIgnoreCase) &&
                 payment.Status != OrderPaymentStatus.RefundFailed)
        {
            // Refused after it was sent: the money is still owed, so the request is left open for the shop
            // team to retry (see RefundOrderPaymentAsync) and why is written down beside it.
            payment.Status = OrderPaymentStatus.RefundFailed;
            payment.RefundFailureReason = Truncate(
                ReadString(refund, "error", "description") ?? "Razorpay reported the refund as failed.", 500);
            changed = true;
        }

        if (changed)
            payment.UpdatedOn = now;

        return changed;
    }

    /// <summary>
    /// Asks the gateway what became of the refunds that are still in flight and writes the answers on their
    /// payment rows (see <see cref="ApplyRefundOutcome"/>). This is the pull half of the loop the refund
    /// webhook closes instantly: the daily gateway sync calls it on the way past, so a refund that was
    /// recorded as 'pending' picks up its final status, its amount and the bank's reference even when the
    /// shop team never turned refund events on. Returns how many rows were brought up to date.
    /// </summary>
    public static async Task<int> RefreshPendingRefundsAsync(
        HomecutiesDbContext context,
        string keyId,
        string keySecret)
    {
        if (!IsConfigured(keyId, keySecret))
            return 0;

        // Only rows that were sent a refund can be asked about. The list is bounded and read newest first,
        // so a shop with years of refunds asks about the recent ones and stops.
        var candidates = await context.OrderPayments
            .Where(p => p.RefundId != null)
            .OrderByDescending(p => p.PaymentId)
            .Take(200)
            .ToListAsync();

        var refreshed = 0;

        foreach (var payment in candidates.Where(RefundInFlight))
        {
            if (await RefreshRefundAsync(context, keyId, keySecret, payment))
                refreshed++;
        }

        return refreshed;
    }

    /// <summary>
    /// Asks the gateway about one refund (see <see cref="FetchRefundAsync"/>) and writes the answer on the
    /// payment row. Returns true when the row changed, false when the gateway had nothing new to say or could
    /// not be reached - refreshing a refund is bookkeeping and must never break the screen that runs it.
    /// </summary>
    public static async Task<bool> RefreshRefundAsync(
        HomecutiesDbContext context,
        string keyId,
        string keySecret,
        OrderPayment payment)
    {
        if (!IsConfigured(keyId, keySecret))
            return false;

        var refund = await FetchRefundAsync(keyId, keySecret, payment);

        if (refund == null || !ApplyRefundOutcome(payment, refund.Value))
            return false;

        await context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// GET /v1/refunds/{refundId} for the refund we sent, or GET /v1/payments/{paymentId}/refunds - and the
    /// newest refund of that payment - for a row that remembers only the payment (a refund made in the
    /// Razorpay dashboard, or one recorded here before the refund id was kept). Null when the gateway could
    /// not be asked or answered nothing usable.
    /// </summary>
    private static async Task<JsonElement?> FetchRefundAsync(string keyId, string keySecret, OrderPayment payment)
    {
        var url = !string.IsNullOrEmpty(payment.RefundId)
            ? $"https://api.razorpay.com/v1/refunds/{Uri.EscapeDataString(payment.RefundId)}"
            : string.IsNullOrEmpty(payment.RazorpayPaymentId)
                ? null
                : $"https://api.razorpay.com/v1/payments/{Uri.EscapeDataString(payment.RazorpayPaymentId)}/refunds";

        if (url == null)
            return null;

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, url);
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);

            using var response = await Http.SendAsync(message);

            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            // One refund comes back as the refund itself; a payment's refunds come back as a collection,
            // whose latest-created refund is the one this row is waiting for (the order of the list is not
            // promised, so the timestamps decide).
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                JsonElement? newest = null;
                var newestOn = int.MinValue;

                foreach (var item in items.EnumerateArray())
                {
                    var createdOn = ReadInt(item, "created_at") ?? 0;

                    if (newest == null || createdOn >= newestOn)
                    {
                        newest = item;
                        newestOn = createdOn;
                    }
                }

                return newest?.Clone();
            }

            return root.Clone();
        }
        catch (Exception)
        {
            // Never let a bookkeeping read break the caller: the row keeps what it already knows and the
            // next sync asks again.
            return null;
        }
    }

    /// <summary>
    /// Asks for the refund of an order whose money we still hold - nothing is sent to Razorpay here. The request
    /// is written on the order's payment row as <see cref="OrderPaymentStatus.RefundRequested"/> (with the date
    /// and the reason), and the shop team approves it from the admin order screen, which is what actually calls
    /// the gateway (see <see cref="RefundOrderPaymentAsync"/>). Until then the customer is told the refund is
    /// under review.
    ///
    /// It is the one place that asks, so a customer's cancellation and a closed return ask in exactly the same
    /// way. <paramref name="moneyWasTaken"/> is the caller's own knowledge of the order it is holding - a
    /// Confirmed order being cancelled, a Delivered one whose parcel came back - read BEFORE the caller moves the
    /// order on: an order that was never charged has nothing owed, and no payment row is invented for it.
    ///
    /// Fails closed twice over: nothing is asked for an order that was never charged, and nothing is asked again
    /// for one that has already been refunded - so a cancellation and a return racing each other, or the shop
    /// team pressing 'Approve refund' twice, can only ever produce one refund. A row is added when the order has
    /// none at all (paid before payments were recorded here, or paid through a checkout whose result never
    /// reached us): the refund is still owed, so the shop team must see it - that row simply carries no gateway
    /// payment id, and approving it tells them to refund by hand.
    /// </summary>
    public static async Task<(bool RefundOwed, string Message)> RequestRefundAsync(
        HomecutiesDbContext context,
        Order order,
        string comment,
        bool moneyWasTaken)
    {
        var orderNumber = OrderNumber(order.OrderId);
        var now = DateTime.UtcNow;

        var payments = await context.OrderPayments
            .Where(p => p.OrderId == order.OrderId)
            .OrderByDescending(p => p.PaymentId)
            .ToListAsync();

        var alreadyRefunded = payments.FirstOrDefault(p =>
            p.Status == OrderPaymentStatus.Refunded || !string.IsNullOrEmpty(p.RefundId));

        if (alreadyRefunded != null)
        {
            return (false,
                $"The payment of {orderNumber} has already been refunded" +
                (string.IsNullOrEmpty(alreadyRefunded.RefundId)
                    ? "."
                    : $" (Razorpay refund {alreadyRefunded.RefundId})."));
        }

        if (!moneyWasTaken)
        {
            return (false, $"No payment was taken for {orderNumber}, so there is nothing to refund.");
        }

        // The attempt the refund will be approved against: the one the gateway confirmed (that is the row that
        // knows the payment id and the amount), else whatever the order has.
        var payment = payments.FirstOrDefault(p =>
            p.Status == OrderPaymentStatus.Captured && !string.IsNullOrEmpty(p.RazorpayPaymentId))
            ?? payments.FirstOrDefault();

        if (payment == null)
        {
            payment = new OrderPayment
            {
                OrderId = order.OrderId,
                Provider = OrderPayment.RazorpayProvider,
                CreatedOn = now
            };

            context.OrderPayments.Add(payment);

            if (order.OrderItems.Count > 0)
            {
                payment.AmountInPaise = (int)(ProductPricing.ChargedTotal(order.OrderItems) * 100);
                payment.Amount = payment.AmountInPaise / 100m;
            }
        }

        payment.Status = OrderPaymentStatus.RefundRequested;
        payment.RefundRequestedOn = now;
        payment.RefundRequestedComment = Truncate(comment, 500);
        payment.UpdatedOn = now;

        await context.SaveChangesAsync();

        return (true, $"The refund of {orderNumber} is waiting for the Homecuties team to approve it.");
    }

    /// <summary>
    /// Refunds the captured payment of <paramref name="order"/> in full and records it on the payment
    /// row. Runs on the caller's <see cref="HomecutiesDbContext"/> (and inside its transaction, when it
    /// has one); it never throws - a refusal is reported and the money is left owed on the payment row
    /// (RefundFailed, with the gateway's own wording in <c>RefundFailureReason</c>), so the caller decides
    /// what the order does. A cancellation that is already going through carries on either way (see
    /// AdminDashboardService.UpdateOrderStatusAsync): the refund simply waits for another approval.
    /// </summary>
    public static async Task<(PaymentRefundResult Outcome, string Message)> RefundOrderPaymentAsync(
        HomecutiesDbContext context,
        string keyId,
        string keySecret,
        Order order,
        string reason)
    {
        var orderNumber = OrderNumber(order.OrderId);

        var payments = await context.OrderPayments
            .Where(p => p.OrderId == order.OrderId)
            .OrderByDescending(p => p.PaymentId)
            .ToListAsync();

        // The one attempt a refund can be sent against: money was really taken (the gateway gave it a
        // payment id), nothing has gone back yet, and the shop team has approved it - the customer's
        // cancellation leaves the row as RefundRequested, and a previous approval that Razorpay
        // refused leaves it as RefundFailed for a second attempt.
        var refundable = payments.FirstOrDefault(p =>
            !string.IsNullOrEmpty(p.RazorpayPaymentId) &&
            string.IsNullOrEmpty(p.RefundId) &&
            (p.Status == OrderPaymentStatus.Captured ||
             p.Status == OrderPaymentStatus.RefundRequested ||
             p.Status == OrderPaymentStatus.RefundFailed));

        if (refundable == null)
        {
            // Already given back (approved twice, or refunded in the Razorpay dashboard and recorded
            // here), or an order that was paid before payments were recorded at all - such a request
            // carries no gateway payment id and Razorpay cannot be asked for it.
            var refunded = payments.FirstOrDefault(p =>
                p.Status == OrderPaymentStatus.Refunded || !string.IsNullOrEmpty(p.RefundId));

            if (refunded != null)
            {
                return (PaymentRefundResult.Refunded,
                    $"The payment of {orderNumber} was already refunded (Razorpay refund {refunded.RefundId}).");
            }

            return (PaymentRefundResult.NothingToRefund,
                $"No captured payment is on record for {orderNumber}, so nothing was refunded. " +
                "Please refund it in the Razorpay dashboard and mark it refunded here.");
        }

        if (!IsConfigured(keyId, keySecret))
        {
            return (PaymentRefundResult.Failed,
                $"The payment of {orderNumber} could not be refunded because the payment gateway is not " +
                "configured. The money is still owed - refund it in the Razorpay dashboard and mark it " +
                "refunded here.");
        }

        // Razorpay is asked for the amount it was actually told to take; what the order's own lines were charged
        // for is the fallback for a row that was recorded before the amount was stored.
        var amountInPaise = refundable.AmountInPaise > 0
            ? refundable.AmountInPaise
            : (int)(ProductPricing.ChargedTotal(order.OrderItems) * 100);

        var now = DateTime.UtcNow;
        var (refund, refundId, refundStatus, error) = await PostRefundAsync(
            keyId, keySecret, refundable.RazorpayPaymentId!, amountInPaise, order.OrderId, reason);

        if (refundId == null || string.Equals(refundStatus, RefundStatusFailed, StringComparison.Ordinal))
        {
            // The refusal is written on the request itself, so the shop team sees what Razorpay said
            // and can approve it again (or refund it by hand) instead of the order looking settled.
            // Nothing was given back, so the row keeps no refund id: a row that carries one is a refund
            // (see RequestRefundAsync), and this one must stay refundable.
            refundable.Status = OrderPaymentStatus.RefundFailed;
            refundable.RefundStatus = refundStatus ?? RefundStatusFailed;
            refundable.RefundFailureReason = Truncate(error ?? "Razorpay did not report a refund.", 500);
            refundable.UpdatedOn = now;
            await context.SaveChangesAsync();

            return (PaymentRefundResult.Failed,
                $"The payment of {orderNumber} could not be refunded: {refundable.RefundFailureReason} " +
                "The money is still owed - please try again, or refund it in the Razorpay dashboard and " +
                "mark it refunded here.");
        }

        refundable.Status = OrderPaymentStatus.Refunded;
        refundable.RefundId = refundId;
        refundable.RefundStatus = refundStatus ?? RefundStatusProcessed;
        refundable.RefundAmount = amountInPaise / 100m;
        refundable.RefundFailureReason = null;
        refundable.UpdatedOn = now;

        // What was asked for is what the books use when the answer carries no amount of its own.
        refundable.RefundAmountInPaise ??= amountInPaise;

        // What the gateway answered is written on the row through the same reader the refund webhook and the
        // daily sync use, so the amount it really gave back, the bank's reference (acquirer_data.arn) and how
        // the money was sent (speed_processed) all land the same way whichever of the three heard it first.
        if (refund.HasValue)
            ApplyRefundOutcome(refundable, refund.Value, now);

        if (refundable.RefundedOn == null)
            refundable.RefundedOn = now;

        await context.SaveChangesAsync();

        // Razorpay answers 'pending' while a refund is still travelling back to the customer's bank;
        // the money is committed either way.
        return string.Equals(refundStatus, RefundStatusProcessed, StringComparison.OrdinalIgnoreCase)
            ? (PaymentRefundResult.Refunded,
                $"The payment of {orderNumber} was refunded (Razorpay refund {refundId}).")
            : (PaymentRefundResult.Pending,
                $"The payment of {orderNumber} was refunded and is on its way back to the customer " +
                $"(Razorpay refund {refundId}, status '{refundStatus}').");
    }

    /// <summary>
    /// Writes down a refund the shop team made in the Razorpay dashboard itself, so the system stops
    /// saying the money is still ours: the order's refund request is marked Refunded with the amount
    /// and the note, and the method returns what to tell the shop team. Used for the requests the app
    /// cannot send itself (no captured payment on record) and for a refund made by hand after a
    /// refusal - the reason is kept in <c>RefundStatus</c> as <c>manual</c>, so a hand-made refund can
    /// always be told apart from one the app sent.
    ///
    /// It never invents money: the amount is what the gateway took when that is on record, else the
    /// order total. Fails closed - a request that is already refunded is reported instead of being
    /// written over.
    /// </summary>
    public static async Task<(bool Recorded, string Message)> RecordManualRefundAsync(
        HomecutiesDbContext context,
        Order order,
        string reason)
    {
        var orderNumber = OrderNumber(order.OrderId);
        var now = DateTime.UtcNow;

        var payments = await context.OrderPayments
            .Where(p => p.OrderId == order.OrderId)
            .OrderByDescending(p => p.PaymentId)
            .ToListAsync();

        var settled = payments.FirstOrDefault(p =>
            p.Status == OrderPaymentStatus.Refunded || !string.IsNullOrEmpty(p.RefundId));

        if (settled != null)
        {
            return (false,
                $"The payment of {orderNumber} has already been refunded" +
                (string.IsNullOrEmpty(settled.RefundId) ? "." : $" (Razorpay refund {settled.RefundId})."));
        }

        // The request being closed: the newest row that is still holding money, else whatever the
        // order has - a request with no gateway payment id (money we never saw a capture for) is
        // exactly the case this method exists for.
        var payment = payments.FirstOrDefault(p =>
            p.Status == OrderPaymentStatus.RefundRequested ||
            p.Status == OrderPaymentStatus.RefundFailed ||
            p.Status == OrderPaymentStatus.Captured)
            ?? payments.FirstOrDefault();

        if (payment == null)
        {
            payment = new OrderPayment
            {
                OrderId = order.OrderId,
                Provider = OrderPayment.RazorpayProvider,
                CreatedOn = now
            };

            context.OrderPayments.Add(payment);
        }

        var amountInPaise = payment.AmountInPaise > 0
            ? payment.AmountInPaise
            : (int)(ProductPricing.ChargedTotal(order.OrderItems) * 100);

        payment.Status = OrderPaymentStatus.Refunded;
        payment.RefundId = null;
        payment.RefundStatus = ManualRefundStatus;
        payment.RefundAmount = amountInPaise / 100m;
        payment.RefundFailureReason = Truncate(reason, 500);
        payment.RefundedOn = now;
        payment.UpdatedOn = now;

        if (payment.AmountInPaise <= 0)
        {
            payment.AmountInPaise = amountInPaise;
            payment.Amount = amountInPaise / 100m;
        }

        await context.SaveChangesAsync();

        return (true,
            $"The refund of {orderNumber} ({amountInPaise / 100m:0.00}) was recorded as made by hand.");
    }

    /// <summary>
    /// POST /v1/payments/{paymentId}/refund. The refund is always for the amount that was taken (the
    /// customer asked for the whole order back, never for part of it). Returns Razorpay's own refund entity
    /// - which carries the amount it really gave back, the bank's reference and how the money was sent, not
    /// just the id - or an error text when nothing was refunded. The entity is a clone: the JSON document it
    /// was read from does not outlive this call.
    /// </summary>
    private static async Task<(JsonElement? Refund, string? RefundId, string? RefundStatus, string? Error)> PostRefundAsync(
        string keyId,
        string keySecret,
        string paymentId,
        int amountInPaise,
        long orderId,
        string reason)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                amount = amountInPaise,
                speed = "normal",
                receipt = OrderNumber(orderId),
                notes = new Dictionary<string, string>
                {
                    ["hc_order_id"] = orderId.ToString(),
                    ["hc_reason"] = Truncate(string.IsNullOrWhiteSpace(reason) ? "Order cancelled" : reason, 200)
                }
            });

            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var message = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://api.razorpay.com/v1/payments/{Uri.EscapeDataString(paymentId)}/refund")
            {
                Content = content
            };

            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);

            using var response = await Http.SendAsync(message);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return (null, null, null,
                    $"Razorpay returned {(int)response.StatusCode} {response.ReasonPhrase}: " +
                    $"{ReadRazorpayError(body) ?? Truncate(body, 300)}");
            }

            using var document = JsonDocument.Parse(body);
            var refund = document.RootElement;

            return (refund.Clone(),
                ReadString(refund, "id"),
                ReadString(refund, "status"),
                null);
        }
        catch (Exception ex)
        {
            return (null, null, null, $"The call to Razorpay failed: {ex.GetBaseException().Message}");
        }
    }

    /// <summary>Razorpay's error text (<c>error.code - error.description</c>), or null when the body is not an error object.</summary>
    private static string? ReadRazorpayError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            if (!document.RootElement.TryGetProperty("error", out var error) ||
                error.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var code = ReadString(error, "code");
            var description = ReadString(error, "description");

            return string.Join(" - ", new[] { code, description }.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// A string property of a Razorpay payload, walked down <paramref name="path"/> when more than one name
    /// is given (<c>ReadString(refund, "acquirer_data", "arn")</c>). Null when any step is missing or is not
    /// a string - every payload field is optional as far as the books are concerned.
    /// </summary>
    private static string? ReadString(JsonElement element, params string[] path)
    {
        foreach (var property in path)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out element))
                return null;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    /// <summary>A whole number property of a Razorpay payload (amounts are in paise, timestamps in seconds).</summary>
    private static int? ReadInt(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(property, out var value) &&
           value.ValueKind == JsonValueKind.Number &&
           value.TryGetInt32(out var number)
            ? number
            : null;

    /// <summary>
    /// Razorpay's Unix-second timestamp as a UTC DateTime (the convention the payment columns are written
    /// in), or null when the payload does not carry one.
    /// </summary>
    private static DateTime? ReadTimestamp(JsonElement element, string property)
    {
        var seconds = ReadInt(element, property);

        return seconds.HasValue && seconds.Value > 0
            ? DateTimeOffset.FromUnixTimeSeconds(seconds.Value).UtcDateTime
            : null;
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "...";

    private static string OrderNumber(long orderId) => $"HC{orderId:D6}";
}
