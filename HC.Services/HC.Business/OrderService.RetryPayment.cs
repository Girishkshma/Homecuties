// ============================================================
// OrderService.RetryPayment.cs
// Partial class: OrderService - paying for an order again
// (the 'Pay now' button in 'My Orders')
// ============================================================

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
    /// Starts a fresh payment attempt for an order that is still waiting for its money, which is what
    /// 'Pay now' in 'My Orders' calls. This is what turns a declined card into a second chance instead
    /// of a phone call: the customer keeps the order (and its reserved units) and pays for it.
    ///
    /// Razorpay is asked first whether the previous attempt actually went through - a customer whose
    /// browser never reported back may already have paid - so a retry can never take the money twice.
    /// The amount comes from the order in our own database, never from the browser.
    /// </summary>
    public async Task<CreateOrderResponse> RetryOrderPaymentAsync(long customerId, long orderId)
    {
        if (!IsRazorpayConfigured)
        {
            _logger.LogError(
                "Paying for order {OrderId} again was refused: Razorpay credentials are not configured.", orderId);

            return PaymentRetryError("Payments are not available right now. Please try again later.");
        }

        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.OrderStatus)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order == null || order.CustomerId != customerId)
        {
            _logger.LogWarning(
                "Paying for order {OrderId} again was refused: not found for customer {CustomerId}.",
                orderId, customerId);

            return PaymentRetryError("Order not found.");
        }

        if (order.OrderStatusId == OrderStatusConfirmed)
            return PaymentRetryError($"{OrderNumber(order.OrderId)} has already been paid - nothing is due on it.");

        if (order.OrderStatusId != OrderStatusPending)
        {
            return PaymentRetryError(
                $"{OrderNumber(order.OrderId)} is {order.OrderStatus?.Status ?? "no longer waiting for payment"}, so it " +
                "cannot be paid here. Please contact support@homecuties.com so we can help you further.");
        }

        var receipt = OrderNumber(order.OrderId);
        var amountInPaise = (int)(ProductPricing.ChargedTotal(order.OrderItems) * 100);

        if (amountInPaise <= 0)
        {
            _logger.LogError("Paying for order {OrderId} again was refused: the order has nothing payable on it.", orderId);

            return PaymentRetryError(
                "The amount of this order could not be determined. Please contact support@homecuties.com.");
        }

        // 'Pending' only means the browser never told us the result - it does not mean no money was
        // taken - so the previous attempt is checked at Razorpay before a new one is started.
        var (lookup, message) = await FindAndApplyOrderPaymentAsync(order, amountInPaise);

        if (lookup != PaymentLookup.NotPaid)
        {
            // AwaitingCapture / Unavailable: the money may be at the customer's bank, and taking a second
            // payment would charge them twice - so the attempt is refused with an error.
            _logger.LogInformation(
                "Paying for order {OrderId} again was refused: checking the earlier payment returned {Outcome}.",
                orderId, lookup);

            // Captured is the one good outcome of this check: the lookup has just confirmed the order,
            // so the customer is told that the money was found instead of being sent to Razorpay again.
            return lookup == PaymentLookup.Captured
                ? new CreateOrderResponse
                {
                    Result = 0,
                    AlreadySettled = true,
                    Messages = new[] { message },
                    OrderId = order.OrderId,
                    OrderNumber = receipt,
                    Amount = amountInPaise / 100m
                }
                : PaymentRetryError(message);
        }

        var (razorpayOrderId, razorpayError) = await CreateRazorpayOrder(
            _razorpayKeyId, _razorpayKeySecret, amountInPaise, receipt, order.OrderId);

        if (string.IsNullOrEmpty(razorpayOrderId))
        {
            _logger.LogError(
                "Creating the Razorpay order for the retry of order {OrderId} failed: {Error}", orderId, razorpayError);

            return PaymentRetryError(
                "We could not start the payment. You have not been charged - please try again in a moment.");
        }

        // A retry is a payment attempt of its own: a new row keeps the story of the order complete
        // (which attempt failed, which one succeeded and which one the refund belongs to).
        await RecordPaymentAttemptAsync(order, razorpayOrderId, amountInPaise, DateTime.UtcNow);

        _logger.LogInformation(
            "Order {OrderId} was given a new payment attempt (razorpay order {RazorpayOrderId}, {Amount} paise).",
            orderId, razorpayOrderId, amountInPaise);

        return new CreateOrderResponse
        {
            Result = 1,
            Messages = new[] { $"Please complete the payment for {receipt}." },
            OrderId = order.OrderId,
            OrderNumber = receipt,
            Amount = amountInPaise / 100m,
            RazorpayOrderId = razorpayOrderId,
            RazorpayKey = _razorpayKeyId
        };
    }

    /// <summary>
    /// Answers in the shape the checkout already understands (<c>Result = 0</c> with a message), so the
    /// browser can handle a retry exactly like a first payment.
    /// </summary>
    private static CreateOrderResponse PaymentRetryError(string message)
        => new() { Result = 0, Messages = new[] { message } };
}
