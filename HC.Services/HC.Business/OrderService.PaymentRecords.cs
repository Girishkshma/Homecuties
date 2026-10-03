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
    /// Asks for the refund of a paid order: the customer cancelled it, so the money is owed back, but
    /// nothing is sent to Razorpay yet. The request is written on the order's payment row as
    /// <see cref="OrderPaymentStatus.RefundRequested"/> (with the date and the reason), and the shop
    /// team approves it from the admin order screen - that approval is what actually calls the
    /// gateway (see RazorpayRefunds). Until then the customer is told the refund is under review.
    ///
    /// A row is added when the order has none at all (paid before payments were recorded here, or paid
    /// through a checkout whose result never reached us): the refund is still owed, so the shop team
    /// must see it - the row simply carries no gateway payment id, and approving it tells them to
    /// refund by hand.
    ///
    /// Fails closed on one thing: when the money has already been given back the request is not
    /// written again, so a cancelled order can never be refunded twice.
    /// </summary>
    private async Task<(bool RefundOwed, string Message)> RequestOrderRefundAsync(Order order, string comment)
    {
        var orderNumber = OrderNumber(order.OrderId);
        var now = DateTime.UtcNow;

        var payments = await _context.OrderPayments
            .Where(p => p.OrderId == order.OrderId)
            .OrderByDescending(p => p.PaymentId)
            .ToListAsync();

        var alreadyRefunded = payments.FirstOrDefault(p =>
            p.Status == OrderPaymentStatus.Refunded || !string.IsNullOrEmpty(p.RefundId));

        if (alreadyRefunded != null)
        {
            return (false,
                $"The payment of {orderNumber} has already been refunded" +
                (string.IsNullOrEmpty(alreadyRefunded.RefundId) ? "." : $" (Razorpay refund {alreadyRefunded.RefundId})."));
        }

        // The attempt the refund will be approved against: the one the gateway confirmed (that is the
        // row that knows the payment id and the amount), else whatever the order has.
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

            _context.OrderPayments.Add(payment);

            if (order.OrderItems.Count > 0)
            {
                payment.AmountInPaise = (int)(order.OrderItems.Sum(oi => oi.UnitPrice) * 100);
                payment.Amount = payment.AmountInPaise / 100m;
            }
        }

        payment.Status = OrderPaymentStatus.RefundRequested;
        payment.RefundRequestedOn = now;
        payment.RefundRequestedComment = TruncateForColumn(comment, 500);
        payment.UpdatedOn = now;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "A refund of {Amount} was requested for order {OrderId} after it was cancelled by the customer " +
            "- it is waiting for the shop team to approve it ({Comment}).",
            payment.Amount, order.OrderId, comment);

        return (true,
            $"The refund of {orderNumber} is waiting for the Homecuties team to approve it.");
    }

    private static string TruncateForColumn(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
