// ============================================================
// OrderService.PaymentRecords.cs
// Partial class: OrderService - the payment record (OrderPayments)
// One row per payment attempt: written when the Razorpay order is
// created, updated by every report that comes back from the
// gateway, and refunded when the order is cancelled.
// ============================================================

using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HC.Business;

public partial class OrderService : IOrderService
{
    /// <summary>
    /// Writes down that a Razorpay order was created for <paramref name="order"/> - the row every
    /// later step finds the payment by (the browser callback, the webhook, a status check and the
    /// shop team's cancellation). Runs inside the caller's transaction, so the order and its first
    /// payment attempt are committed together and an order is never left without its payment.
    /// </summary>
    private async Task<OrderPayment> RecordPaymentAttemptAsync(
        Order order,
        string razorpayOrderId,
        int amountInPaise,
        DateTime createdOn)
    {
        var payment = new OrderPayment
        {
            OrderId = order.OrderId,
            Provider = OrderPayment.RazorpayProvider,
            RazorpayOrderId = razorpayOrderId,
            Status = OrderPaymentStatus.Created,
            Amount = amountInPaise / 100m,
            AmountInPaise = amountInPaise,
            CreatedOn = createdOn
        };

        _context.OrderPayments.Add(payment);
        await _context.SaveChangesAsync();

        return payment;
    }

    /// <summary>
    /// The payment attempt a report from the gateway belongs to: the row with that Razorpay payment
    /// id, else the row with that Razorpay order id, else the order's newest attempt (an order gets
    /// one attempt per 'Pay now' and the newest is the one being paid).
    /// </summary>
    private async Task<OrderPayment?> FindPaymentAttemptAsync(
        Order order,
        string? razorpayOrderId,
        string? razorpayPaymentId)
    {
        var attempts = await _context.OrderPayments
            .Where(p => p.OrderId == order.OrderId)
            .OrderByDescending(p => p.PaymentId)
            .ToListAsync();

        if (attempts.Count == 0)
            return null;

        if (!string.IsNullOrEmpty(razorpayPaymentId))
        {
            var byPaymentId = attempts.FirstOrDefault(p =>
                string.Equals(p.RazorpayPaymentId, razorpayPaymentId, StringComparison.Ordinal));

            if (byPaymentId != null)
                return byPaymentId;
        }

        if (!string.IsNullOrEmpty(razorpayOrderId))
        {
            var byRazorpayOrderId = attempts.FirstOrDefault(p =>
                string.Equals(p.RazorpayOrderId, razorpayOrderId, StringComparison.Ordinal));

            if (byRazorpayOrderId != null)
                return byRazorpayOrderId;
        }

        return attempts[0];
    }

    /// <summary>
    /// Records what the gateway reported about a payment on the order's payment row, creating the row
    /// when an earlier step never did (an order placed before this table existed, or one whose
    /// Razorpay order was created outside our checkout).
    ///
    /// <paramref name="amountInPaise"/> of 0 means "the gateway did not say" - a browser-side failure -
    /// and never overwrites a known amount.
    /// </summary>
    private async Task<OrderPayment> SavePaymentOutcomeAsync(
        Order order,
        string? razorpayOrderId,
        string? razorpayPaymentId,
        string status,
        int amountInPaise,
        string? failureCode = null,
        string? failureReason = null)
    {
        var now = DateTime.UtcNow;
        var payment = await FindPaymentAttemptAsync(order, razorpayOrderId, razorpayPaymentId);

        if (payment == null)
        {
            payment = new OrderPayment
            {
                OrderId = order.OrderId,
                Provider = OrderPayment.RazorpayProvider,
                CreatedOn = now,
                Status = OrderPaymentStatus.Created
            };

            _context.OrderPayments.Add(payment);
        }

        // Filled in, never overwritten: the row is the order's memory of what the gateway told it.
        payment.RazorpayOrderId ??= razorpayOrderId;
        payment.RazorpayPaymentId ??= razorpayPaymentId;
        payment.Status = status;
        payment.UpdatedOn = now;

        // A reason written down once is kept - it is the story of that attempt, and it is what the
        // shop team reads when the customer asks what went wrong.
        if (!string.IsNullOrWhiteSpace(failureCode))
            payment.FailureCode = TruncateForColumn(failureCode, 50);

        if (!string.IsNullOrWhiteSpace(failureReason))
            payment.FailureReason = TruncateForColumn(failureReason, 500);

        if (amountInPaise > 0)
        {
            payment.AmountInPaise = amountInPaise;
            payment.Amount = amountInPaise / 100m;
        }
        else if (payment.AmountInPaise == 0 && order.OrderItems.Count > 0)
        {
            // Nothing ever said how much was taken: the order total is the best figure we have, and a
            // refund needs one.
            payment.AmountInPaise = (int)(order.OrderItems.Sum(oi => oi.UnitPrice) * 100);
            payment.Amount = payment.AmountInPaise / 100m;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Payment of order {OrderId} is now '{Status}' (razorpay order {RazorpayOrderId}, payment {RazorpayPaymentId}){Failure}.",
            order.OrderId,
            status,
            payment.RazorpayOrderId ?? "n/a",
            payment.RazorpayPaymentId ?? "n/a",
            payment.FailureReason == null ? string.Empty : $" - {payment.FailureReason}");

        return payment;
    }

    /// <summary>
    /// Asks for the refund of a paid order the customer has cancelled: the money is owed back, but nothing is
    /// sent to Razorpay yet - the request is written on the payment row
    /// (<see cref="RazorpayRefunds.RequestRefundAsync"/>, which owns that rule and is where a closed return asks
    /// too) and the shop team's approval is what actually calls the gateway.
    ///
    /// <paramref name="moneyWasTaken"/> is what this caller knows about the order it is holding - a Confirmed
    /// order has been paid for - read before the cancellation moves the order on.
    /// </summary>
    private async Task<(bool RefundOwed, string Message)> RequestOrderRefundAsync(
        Order order, string comment, bool moneyWasTaken)
    {
        var (refundOwed, message) = await RazorpayRefunds.RequestRefundAsync(
            _context, order, comment, moneyWasTaken);

        if (refundOwed)
        {
            _logger.LogInformation(
                "A refund was requested for order {OrderId} after it was cancelled by the customer ({Comment}).",
                order.OrderId, comment);
        }

        return (refundOwed, message);
    }

    private static string TruncateForColumn(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
