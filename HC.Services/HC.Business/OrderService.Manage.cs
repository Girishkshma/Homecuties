// ============================================================
// OrderService.Manage.cs
// Partial class: OrderService - 'My Orders' operations
// (cancel an unpaid order, re-check a payment at Razorpay)
// ============================================================

using System.Text.Json;
using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HC.Business;

public partial class OrderService : IOrderService
{
    /// <summary>
    /// How far around the order date Razorpay's payment list is searched. Our own order id is not
    /// stored next to the Razorpay order id, so the checkout window is reconstructed from the order
    /// date (Razorpay's orders/payments list cannot be filtered by receipt).
    /// </summary>
    private static readonly TimeSpan PaymentSearchWindowBefore = TimeSpan.FromDays(1);
    private static readonly TimeSpan PaymentSearchWindowAfter = TimeSpan.FromDays(7);

    /// <summary>What a payment lookup at Razorpay found for an order.</summary>
    private enum PaymentLookup
    {
        /// <summary>Razorpay could not be reached - nothing can be said about the payment.</summary>
        Unavailable,

        /// <summary>No payment, or only a failed/abandoned one, exists for the order.</summary>
        NotPaid,

        /// <summary>The money was captured and the order has been confirmed.</summary>
        Captured,

        /// <summary>The money is authorised but could not be captured - a human has to look at it.</summary>
        AwaitingCapture
    }

    /// <summary>
    /// Lets the customer cancel an order from 'My Orders' while it is still in the shop's hands: an
    /// unpaid (Pending) order, or a paid (Confirmed) one, whose money is then owed back. Cancellation
    /// puts the reserved units straight back into the available pool, so the items can be bought by
    /// somebody else. Once the order is Shipped the parcel is on its way, so this is refused and the
    /// customer is told to contact support, where a cancellation becomes a return.
    ///
    /// A paid order is not refunded on the spot: the refund is requested on the payment row
    /// (see <see cref="OrderService.RequestOrderRefundAsync"/>) and the shop team approves it from the
    /// admin order screen, which is what sends it to Razorpay. The cancellation itself always goes
    /// through - the customer is told the refund is under review - and the money stays flagged as owed
    /// until it is really given back.
    ///
    /// Which statuses may be cancelled is decided by <see cref="OrderStatusFlow.CanCustomerCancel"/>,
    /// the same rule 'My Orders' uses to offer the button.
    /// </summary>
    public async Task<ResultDto> CancelOrderAsync(long customerId, long orderId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.OrderStatus)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null || order.CustomerId != customerId)
            {
                _logger.LogWarning(
                    "Cancelling order {OrderId} was refused: not found for customer {CustomerId}.", orderId, customerId);

                return PaymentError("Order not found.");
            }

            var orderNumber = OrderNumber(order.OrderId);

            if (order.OrderStatusId == OrderStatusCancelled)
                return PaymentError($"{orderNumber} is already cancelled.");

            if (!OrderStatusFlow.CanCustomerCancel(order.OrderStatusId))
            {
                _logger.LogWarning(
                    "Cancelling order {OrderId} was refused: its status is {StatusId}.", orderId, order.OrderStatusId);

                return PaymentError(
                    $"{orderNumber} is {order.OrderStatus?.Status ?? "on its way"}, so it can no longer be cancelled " +
                    "here - an order can only be cancelled until it is shipped. Please contact " +
                    "support@homecuties.com so we can help you with a return.");
            }

            // Whether the money is already ours decides what cancelling this order has to do: a Pending
            // order has nothing to give back, a Confirmed one has to be refunded.
            var isPaid = OrderStatusFlow.IsPaid(order.OrderStatusId);

            // A 'Pending' order can still have been paid - the customer may have paid and closed the
            // browser before the result reached us. Never cancel a paid order silently: look the
            // payment up first and confirm the order when the money is ours. A Confirmed order is
            // already paid, so there is nothing left to look up - it is refunded further down instead.
            if (OrderStatusFlow.CanCustomerPay(order.OrderStatusId) && IsRazorpayConfigured)
            {
                var orderTotalInPaise = (int)(order.OrderItems.Sum(oi => oi.UnitPrice) * 100);
                var (lookup, message) = await FindAndApplyOrderPaymentAsync(order, orderTotalInPaise);

                if (lookup == PaymentLookup.Captured)
                {
                    await transaction.CommitAsync();
                    return new ResultDto { Result = 1, Messages = new[] { message } };
                }

                if (lookup != PaymentLookup.NotPaid)
                    return PaymentError(message);
            }

            // A paid order is one whose money we are holding, so cancelling it means giving that money
            // back - but not from here: the refund is ASKED FOR and the shop team approves it on the
            // admin order screen (AdminDashboardService.ApproveOrderRefundAsync), which is what calls
            // Razorpay. The cancellation itself always goes through, so the customer is never left
            // waiting for an approval to know where their order stands, while the money stays flagged
            // as owed until it has really been given back.
            var historyComment = "Order cancelled by the customer";
            var successMessage = $"{orderNumber} has been cancelled. No payment will be taken.";

            if (isPaid)
            {
                // An approval refunds against the payment on file, so make sure there is one: an order
                // paid before payments were recorded here, or one whose browser callback never reached
                // us, has none until the gateway is asked (see EnsureRefundablePaymentAsync). This is
                // best effort - when no capture can be found the request is still written down and the
                // shop team refunds it by hand, which is exactly what the order screen is for.
                await EnsureRefundablePaymentAsync(order);

                var (refundOwed, requestMessage) = await RequestOrderRefundAsync(
                    order, historyComment, moneyWasTaken: isPaid);

                historyComment += $" {requestMessage}";

                successMessage = refundOwed
                    ? $"{orderNumber} has been cancelled. Your refund has been requested and will be " +
                      "reviewed by our team - the money goes back to the payment method you used, usually " +
                      "within 5-7 working days of the approval."
                    : $"{orderNumber} has been cancelled. {requestMessage}";
            }

            var now = DateTime.UtcNow;
            var releasedUnits = 0;

            // A cancellable order's units are still reserved as 'Ordered' (confirming an order does not
            // move them - only shipping does, and that is past the point where cancelling is allowed), so
            // each one goes straight back into the available pool.
            foreach (var item in order.OrderItems)
            {
                var sku = await _context.Skus.FirstOrDefaultAsync(s => s.Sku1 == item.Sku);
                if (sku == null || sku.SkustatusId != OrderedSkuStatusId)
                    continue;

                // The item is physically back on the shelf, so make the unit sellable again.
                sku.SkustatusId = AvailableSkuStatusId;

                _context.Skuhistories.Add(new Skuhistory
                {
                    Sku = sku.Sku1,
                    InventoryId = sku.InventoryId,
                    SkustatusId = AvailableSkuStatusId,
                    HistoryDate = now
                });

                releasedUnits++;
            }

            order.OrderStatusId = OrderStatusCancelled;

            _context.OrderHistories.Add(new OrderHistory
            {
                OrderId = order.OrderId,
                HistoryDate = now,
                OrderStatusId = OrderStatusCancelled,
                Comments = historyComment
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation(
                "Order {OrderId} was cancelled by customer {CustomerId} ({Payment}); {Units} unit(s) returned to " +
                "the available pool.",
                order.OrderId, customerId, isPaid ? "paid - refund requested, waiting for approval" : "unpaid",
                releasedUnits);

            return new ResultDto
            {
                Result = 1,
                Messages = new[] { successMessage }
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();

            _logger.LogError(ex, "Cancelling order {OrderId} failed.", orderId);

            return PaymentError("We could not cancel the order. Nothing was changed - please try again or contact support.");
        }
    }

    /// <summary>
    /// Re-checks an order's payment at Razorpay and confirms the order when the money was taken. This
    /// is the safety net for a customer whose browser never reported the payment result (closed
    /// window, lost connection) and for a shop that has no Razorpay webhook configured yet.
    /// </summary>
    public async Task<ResultDto> SyncOrderPaymentAsync(long customerId, long orderId)
    {
        if (!IsRazorpayConfigured)
        {
            _logger.LogError("Checking an order payment failed: Razorpay credentials are not configured.");
            return PaymentError("Payments are not available right now. Please contact support.");
        }

        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.OrderStatus)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order == null || order.CustomerId != customerId)
        {
            _logger.LogWarning(
                "Checking the payment of order {OrderId} was refused: not found for customer {CustomerId}.",
                orderId, customerId);

            return PaymentError("Order not found.");
        }

        if (order.OrderStatusId == OrderStatusCancelled)
            return PaymentError($"{OrderNumber(order.OrderId)} was cancelled, so there is no payment to check.");

        // Already resolved - report the current state instead of asking Razorpay again.
        if (order.OrderStatusId != OrderStatusPending)
        {
            return new ResultDto
            {
                Result = 1,
                Messages = new[]
                {
                    $"{OrderNumber(order.OrderId)} is {order.OrderStatus?.Status ?? "confirmed"} - your payment has been received."
                }
            };
        }

        var orderTotalInPaise = (int)(order.OrderItems.Sum(oi => oi.UnitPrice) * 100);
        var (lookup, message) = await FindAndApplyOrderPaymentAsync(order, orderTotalInPaise);

        return lookup == PaymentLookup.Captured
            ? new ResultDto { Result = 1, Messages = new[] { message } }
            : PaymentError(message);
    }

    /// <summary>
    /// Finds the payments Razorpay recorded around the checkout of <paramref name="order"/> and, when
    /// the money is captured (or only authorised), captures and confirms the order.
    ///
    /// The match works through the notes: <c>hc_order_id</c> is set when the Razorpay order is created
    /// and Razorpay copies the notes to every payment of that order, so the payment of THIS order can
    /// be told apart from every other payment in the same window. Configuring the webhook
    /// (POST /api/Order/Webhook) is still the better safety net - orders are then confirmed straight
    /// away and this lookup only serves the customer's "check status" button.
    /// </summary>
    private async Task<(PaymentLookup Outcome, string Message)> FindAndApplyOrderPaymentAsync(
        Order order,
        int orderTotalInPaise)
    {
        var from = ToUnixSeconds(order.OrderDate - PaymentSearchWindowBefore);
        var to = ToUnixSeconds(order.OrderDate + PaymentSearchWindowAfter);

        var (payments, error) = await FetchRazorpayAsync($"payments?count=100&from={from}&to={to}");
        if (payments == null)
        {
            _logger.LogError("Checking the payment of order {OrderId} failed: {Error}", order.OrderId, error);

            return (PaymentLookup.Unavailable,
                "We could not reach the payment gateway to check the status. Please try again in a moment.");
        }

        var orderIdText = order.OrderId.ToString();
        var candidates = EnumerateItems(payments.Value)
            .Where(payment => string.Equals(ReadNoteIdText(payment), orderIdText, StringComparison.Ordinal))
            .ToList();

        var payment = candidates.FirstOrDefault(candidate => IsStatus(candidate, RazorpayStatusCaptured));
        var alreadyCaptured = payment.ValueKind != JsonValueKind.Undefined;

        if (!alreadyCaptured)
        {
            payment = candidates.FirstOrDefault(candidate => IsStatus(candidate, RazorpayStatusAuthorized));

            if (payment.ValueKind == JsonValueKind.Undefined)
            {
                if (candidates.Count == 0)
                {
                    _logger.LogInformation("No payment was found for order {OrderId}.", order.OrderId);

                    return (PaymentLookup.NotPaid,
                        "We have not received a payment for this order yet. If money was debited it usually shows up " +
                        "within a few minutes - please check again, or pay for the order again. Contact support with " +
                        "your order number if you need help.");
                }

                var failedStatus = ReadString(candidates[0], "status") ?? "unknown";

                _logger.LogInformation(
                    "The payment(s) of order {OrderId} are in status '{Status}' - the order stays pending.",
                    order.OrderId, failedStatus);

                // This is where a payment that never reached the browser gets its reason written down:
                // 'Pay now' can be offered again and support has the gateway's own words at hand.
                await SavePaymentOutcomeAsync(
                    order,
                    ReadString(candidates[0], "order_id"),
                    ReadString(candidates[0], "id"),
                    OrderPaymentStatus.FromRazorpay(failedStatus),
                    ReadAmount(candidates[0]),
                    failureCode: ReadString(candidates[0], "error_code"),
                    failureReason: ReadString(candidates[0], "error_description"));

                return (PaymentLookup.NotPaid,
                    $"The payment for this order was not completed (status: {failedStatus}), so nothing has been " +
                    "charged. You can pay for it again - the items are still reserved - or cancel the order.");
            }
        }

        var paymentId = ReadString(payment, "id") ?? "unknown";
        var paidAmountInPaise = ReadAmount(payment);

        // Razorpay auto-refunds an authorised payment that is never captured, so the order may only be
        // confirmed - and the money only kept - once the captured amount covers the order total.
        if (paidAmountInPaise < orderTotalInPaise)
        {
            _logger.LogWarning(
                "The payment {PaymentId} of order {OrderId} covers {Paid} paise but the order needs {Expected} paise.",
                paymentId, order.OrderId, paidAmountInPaise, orderTotalInPaise);

            // The amount that was really taken is recorded (a refund has to be for that figure, not for
            // the order total) together with the mismatch the shop team has to look at.
            await SavePaymentOutcomeAsync(
                order,
                ReadString(payment, "order_id"),
                paymentId,
                // A short payment that has already been captured is money in our account (the cancel
                // path can give it back); one that is only authorised is money the bank is holding.
                alreadyCaptured ? OrderPaymentStatus.Captured : OrderPaymentStatus.Authorized,
                paidAmountInPaise,
                failureCode: "amount_mismatch",
                failureReason: $"Razorpay took {paidAmountInPaise} paise but the order needs {orderTotalInPaise} paise.",
                // A short payment that was already captured still carries what the gateway charged for
                // taking the money, so the row records that as well (see OrderPaymentCharges).
                gatewayEntity: payment);

            return (PaymentLookup.AwaitingCapture,
                $"We found a payment for {OrderNumber(order.OrderId)}, but its amount does not match the order " +
                "total. Please contact support - you have not been charged twice.");
        }

        // What the capture answered with (null unless this call is what captured the money). The capture's
        // own answer is the freshest account of the payment - it is where the gateway reports what it
        // charged for taking the money - so it is what the row below is written from.
        JsonElement? capturedPayment = null;

        if (!alreadyCaptured)
        {
            var (captured, captureAnswer, captureError) =
                await CaptureRazorpayPaymentAsync(paymentId, orderTotalInPaise);
            capturedPayment = captureAnswer;
            if (!captured)
            {
                _logger.LogError(
                    "Capturing payment {PaymentId} of order {OrderId} failed: {Error}",
                    paymentId, order.OrderId, captureError);

                // The bank is holding the money while the capture keeps failing, so the attempt is kept
                // as 'Authorized' with the reason - the shop team decides between another capture and a refund.
                await SavePaymentOutcomeAsync(
                    order,
                    ReadString(payment, "order_id"),
                    paymentId,
                    OrderPaymentStatus.Authorized,
                    paidAmountInPaise,
                    failureCode: "capture_failed",
                    failureReason: captureError);

                return (PaymentLookup.AwaitingCapture,
                    "Your bank has authorised the payment but it is not captured yet. Please contact support with " +
                    "your order number - you have not been charged twice.");
            }
        }

        // The money is captured: the payment is written down before the order is confirmed, so an
        // order can never be Confirmed without a payment row to refund it against.
        await SavePaymentOutcomeAsync(
            order,
            ReadString(payment, "order_id"),
            paymentId,
            OrderPaymentStatus.Captured,
            paidAmountInPaise,
            gatewayEntity: capturedPayment ?? payment);

        // The payment id is on the payment row and in the log below - not in the customer-facing history.
        await ConfirmOrderAsync(
            order, "Payment captured (confirmed by a payment status check).");

        _logger.LogInformation(
            "Order {OrderId} was confirmed by a payment status check (payment {PaymentId}).", order.OrderId, paymentId);

        return (PaymentLookup.Captured,
            $"We found your payment - {OrderNumber(order.OrderId)} is confirmed. Thank you!");
    }

    /// <summary>
    /// Makes sure a paid order has a payment to refund, so that cancelling it can really give the money
    /// back. An order paid through our checkout always has a payment row (it is written when the Razorpay
    /// order is created and updated by every report from the gateway), but an older order - or one whose
    /// browser callback never reached us - may have none. Then Razorpay itself is asked which capture
    /// belongs to this order, exactly like the customer's "check payment" does: our order id travels in
    /// the <c>hc_order_id</c> note, so a capture of another order in the same window cannot be mistaken
    /// for this one. The row is written before the refund is attempted, and it carries the amount
    /// Razorpay actually took.
    ///
    /// Nothing is invented: when the gateway cannot be reached, or took nothing under this order id, no
    /// row is written and the caller reports that there was nothing to give back. Read-only towards
    /// Razorpay, and it runs inside the caller's transaction like every other write here.
    /// </summary>
    private async Task EnsureRefundablePaymentAsync(Order order)
    {
        if (!IsRazorpayConfigured)
            return;

        var hasCapturedPayment = await _context.OrderPayments.AnyAsync(payment =>
            payment.OrderId == order.OrderId &&
            payment.RazorpayPaymentId != null &&
            payment.RazorpayPaymentId != string.Empty &&
            payment.Status == OrderPaymentStatus.Captured);

        if (hasCapturedPayment)
            return;

        var captured = await FindCapturedPaymentAsync(order);
        if (captured == null)
            return;

        _logger.LogInformation(
            "Order {OrderId} had no captured payment on record; the gateway's payment {PaymentId} was found " +
            "while the order was being cancelled, so the refund has something to be issued against.",
            order.OrderId, ReadString(captured.Value, "id") ?? "unknown");

        await SavePaymentOutcomeAsync(
            order,
            ReadString(captured.Value, "order_id"),
            ReadString(captured.Value, "id"),
            OrderPaymentStatus.Captured,
            ReadAmount(captured.Value),
            // The gateway's payment that was found here carries its charges too, so the row this late
            // payment writes knows what the money cost to take (see OrderPaymentCharges).
            gatewayEntity: captured);
    }

    /// <summary>
    /// The captured payment Razorpay recorded around the checkout of <paramref name="order"/>, matched on
    /// the <c>hc_order_id</c> note. Null when the gateway cannot be reached or took nothing for this
    /// order - the caller then has nothing to refund.
    /// </summary>
    private async Task<JsonElement?> FindCapturedPaymentAsync(Order order)
    {
        var from = ToUnixSeconds(order.OrderDate - PaymentSearchWindowBefore);
        var to = ToUnixSeconds(order.OrderDate + PaymentSearchWindowAfter);

        var (payments, error) = await FetchRazorpayAsync($"payments?count=100&from={from}&to={to}");
        if (payments == null)
        {
            _logger.LogWarning(
                "Looking the payment of order {OrderId} up for a refund failed: {Error}", order.OrderId, error);

            return null;
        }

        var orderIdText = order.OrderId.ToString();

        foreach (var payment in EnumerateItems(payments.Value))
        {
            if (string.Equals(ReadNoteIdText(payment), orderIdText, StringComparison.Ordinal) &&
                IsStatus(payment, RazorpayStatusCaptured))
            {
                return payment;
            }
        }

        return null;
    }

    /// <summary>Our own order id from the Razorpay notes (<c>hc_order_id</c>), as string or number.</summary>
    private static string? ReadNoteIdText(JsonElement entity)
    {
        if (entity.ValueKind != JsonValueKind.Object ||
            !entity.TryGetProperty("notes", out var notes) ||
            notes.ValueKind != JsonValueKind.Object ||
            !notes.TryGetProperty("hc_order_id", out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static bool IsStatus(JsonElement entity, string status)
        => string.Equals(ReadString(entity, "status"), status, StringComparison.Ordinal);

    /// <summary>The <c>items</c> array of a Razorpay list response (empty when there is none).</summary>
    private static IEnumerable<JsonElement> EnumerateItems(JsonElement root)
        => root.ValueKind == JsonValueKind.Object &&
           root.TryGetProperty("items", out var items) &&
           items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray()
            : Enumerable.Empty<JsonElement>();

    private static long ToUnixSeconds(DateTime utc)
        => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();

    private static string OrderNumber(long orderId) => $"HC{orderId:D6}";
}
