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

        // Razorpay is asked for the amount it was actually told to take; the order total is the
        // fallback for a row that was recorded before the amount was stored.
        var amountInPaise = refundable.AmountInPaise > 0
            ? refundable.AmountInPaise
            : (int)(order.OrderItems.Sum(oi => oi.UnitPrice) * 100);

        var now = DateTime.UtcNow;
        var (refundId, refundStatus, error) = await PostRefundAsync(
            keyId, keySecret, refundable.RazorpayPaymentId!, amountInPaise, order.OrderId, reason);

        if (refundId == null || string.Equals(refundStatus, "failed", StringComparison.Ordinal))
        {
            // The refusal is written on the request itself, so the shop team sees what Razorpay said
            // and can approve it again (or refund it by hand) instead of the order looking settled.
            refundable.Status = OrderPaymentStatus.RefundFailed;
            refundable.RefundStatus = refundStatus ?? "failed";
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
        refundable.RefundStatus = refundStatus ?? "processed";
        refundable.RefundAmount = amountInPaise / 100m;
        refundable.RefundFailureReason = null;
        refundable.RefundedOn = now;
        refundable.UpdatedOn = now;
        await context.SaveChangesAsync();

        // Razorpay answers 'pending' while a refund is still travelling back to the customer's bank;
        // the money is committed either way.
        return string.Equals(refundStatus, "processed", StringComparison.OrdinalIgnoreCase)
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
            : (int)(order.OrderItems.Sum(oi => oi.UnitPrice) * 100);

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
    /// customer asked for the whole order back, never for part of it). Returns the refund id and
    /// Razorpay's own refund status, or an error text when nothing was refunded.
    /// </summary>
    private static async Task<(string? RefundId, string? RefundStatus, string? Error)> PostRefundAsync(
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
                return (null, null,
                    $"Razorpay returned {(int)response.StatusCode} {response.ReasonPhrase}: " +
                    $"{ReadRazorpayError(body) ?? Truncate(body, 300)}");
            }

            using var document = JsonDocument.Parse(body);

            return (ReadString(document.RootElement, "id"), ReadString(document.RootElement, "status"), null);
        }
        catch (Exception ex)
        {
            return (null, null, $"The call to Razorpay failed: {ex.GetBaseException().Message}");
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

    private static string? ReadString(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(property, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "...";

    private static string OrderNumber(long orderId) => $"HC{orderId:D6}";
}
