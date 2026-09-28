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
    /// Lets the customer cancel an order from 'My Orders'. Only an order that has not been paid yet
    /// can be cancelled: cancellation puts the reserved units straight back into the available pool,
    /// so the items can be bought by somebody else. Cancelling a PAID order would mean refunding it,
    /// which is done by the shop team - the customer is told to contact support instead.
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

            if (order.OrderStatusId == OrderStatusCancelled)
                return PaymentError($"{OrderNumber(order.OrderId)} is already cancelled.");

            if (order.OrderStatusId != OrderStatusPending)
            {
                return PaymentError(
                    $"{OrderNumber(order.OrderId)} is {order.OrderStatus?.Status ?? "already confirmed"}, so it can no " +
                    "longer be cancelled here. Please contact support@homecuties.com so we can help you further.");
            }

            // A 'Pending' order can still have been paid - the customer may have paid and closed the
            // browser before the result reached us. Never cancel a paid order silently: look the
            // payment up first and confirm the order when the money is ours.
            if (IsRazorpayConfigured)
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

            var now = DateTime.UtcNow;
            var releasedUnits = 0;

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
                Comments = "Order cancelled by the customer"
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation(
                "Order {OrderId} was cancelled by customer {CustomerId}; {Units} unit(s) returned to the available pool.",
                order.OrderId, customerId, releasedUnits);

            return new ResultDto
            {
                Result = 1,
                Messages = new[] { $"{OrderNumber(order.OrderId)} has been cancelled. No payment will be taken." }
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
                        "within a few minutes - please check again, or contact support with your order number.");
                }

                var failedStatus = ReadString(candidates[0], "status") ?? "unknown";

                _logger.LogInformation(
                    "The payment(s) of order {OrderId} are in status '{Status}' - the order stays pending.",
                    order.OrderId, failedStatus);

                return (PaymentLookup.NotPaid,
                    $"The payment for this order was not completed (status: {failedStatus}), so nothing has been " +
                    "charged. You can place the order again or cancel it.");
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

            return (PaymentLookup.AwaitingCapture,
                $"We found a payment for {OrderNumber(order.OrderId)}, but its amount does not match the order " +
                "total. Please contact support - you have not been charged twice.");
        }

        if (!alreadyCaptured)
        {
            var (captured, captureError) = await CaptureRazorpayPaymentAsync(paymentId, orderTotalInPaise);
            if (!captured)
            {
                _logger.LogError(
                    "Capturing payment {PaymentId} of order {OrderId} failed: {Error}",
                    paymentId, order.OrderId, captureError);

                return (PaymentLookup.AwaitingCapture,
                    "Your bank has authorised the payment but it is not captured yet. Please contact support with " +
                    "your order number - you have not been charged twice.");
            }
        }

        await ConfirmOrderAsync(
            order, $"Payment captured (confirmed by a payment status check). Razorpay Payment ID: {paymentId}");

        _logger.LogInformation(
            "Order {OrderId} was confirmed by a payment status check (payment {PaymentId}).", order.OrderId, paymentId);

        return (PaymentLookup.Captured,
            $"We found your payment - {OrderNumber(order.OrderId)} is confirmed. Thank you!");
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
