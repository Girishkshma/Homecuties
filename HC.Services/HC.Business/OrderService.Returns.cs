// ============================================================
// OrderService.Returns.cs
// Partial class: OrderService - 'My Orders' returns
// (ask to send a delivered order back, take the ask back)
// ============================================================

using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HC.Business;

public partial class OrderService : IOrderService
{
    /// <summary>
    /// Asks for a return of a delivered order from 'My Orders' - the customer's half of the return
    /// lifecycle, whose rules live in <see cref="OrderReturnFlow"/>.
    ///
    /// What is checked here, and why each one matters:
    ///   * the order must be the caller's own (a guessed id can never return somebody else's parcel);
    ///   * it must have been DELIVERED (<see cref="OrderStatusFlow.CanCustomerReturn"/>): before that
    ///     nothing is the customer's to send back, and after it the order is either already returned or was
    ///     cancelled;
    ///   * the return window must still be open (<c>Returns:WindowDays</c>, counted from the day the parcel
    ///     reached them - see <see cref="OrderReturnFlow.WindowEndsOn"/>);
    ///   * no return may already be open on the order (the database refuses a second one as well);
    ///   * the reason must be one of the codes the picker offers (<see cref="OrderReturnReason"/>), because
    ///     that is what the shop counts returns by.
    ///
    /// The ask is written down and nothing else happens: the shop team answers it from the admin order
    /// screen, and the order only becomes 'Returned' - its money owed back and its units back on the shelf -
    /// when the return is closed with the parcel in the shop. No explicit transaction here: the ask is one
    /// write, which is already one transaction, and nothing else is touched.
    /// </summary>
    public async Task<ResultDto> RequestReturnAsync(long customerId, RequestReturnRequest request)
    {
        var order = await _context.Orders
            .Include(o => o.OrderStatus)
            .Include(o => o.OrderShipments)
            .Include(o => o.OrderHistories)
            .FirstOrDefaultAsync(o => o.OrderId == request.OrderId);

        if (order == null || order.CustomerId != customerId)
        {
            _logger.LogWarning(
                "A return was refused: order {OrderId} was not found for customer {CustomerId}.",
                request.OrderId, customerId);

            return Refusal("Order not found.");
        }

        var orderNumber = OrderNumber(order.OrderId);

        // The reason is the picker's, never free text: the code is what the shop counts returns by, and the
        // customer's own words are kept beside it (the label stands in when they typed nothing).
        var reasonCode = (request.ReasonCode ?? string.Empty).Trim();
        if (!OrderReturnReason.IsCustomerReason(reasonCode))
        {
            return Refusal("Please choose why you are returning the order, so we can help you properly.");
        }

        var now = DateTime.UtcNow;

        if (await OrderReturnFlow.FindOpenAsync(_context, order.OrderId) != null)
        {
            return Refusal(
                $"You have already asked to return {orderNumber} - we are looking into it. You can take that " +
                "request back from this page while it is still waiting for our answer.");
        }

        if (!OrderStatusFlow.CanCustomerReturn(order.OrderStatusId))
        {
            var status = order.OrderStatus?.Status ?? "on its way";

            return Refusal(
                $"{orderNumber} is {status}, so it cannot be returned from here. " +
                "Please contact support@homecuties.com and we will help you.");
        }

        var windowEndsOn = OrderReturnFlow.WindowEndsOn(order, _returnWindowDays);
        if (!OrderReturnFlow.IsWithinWindow(windowEndsOn, now))
        {
            return Refusal(
                $"The return window for {orderNumber} closed on {windowEndsOn!.Value:dd MMM yyyy} " +
                $"({_returnWindowDays} days after it was delivered). Please contact " +
                "support@homecuties.com - we may still be able to help.");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? OrderReturnReason.Label(reasonCode)
            : request.Reason.Trim();

        var asked = await OrderReturnFlow.RequestAsync(
            _context,
            order,
            OrderReturnOrigin.Customer,
            reasonCode,
            reason,
            await GetCustomerEmailAsync(customerId),
            now);

        _logger.LogInformation(
            "Customer {CustomerId} asked to return order {OrderId} ({ReasonCode}); return {ReturnId} is waiting " +
            "for the shop team.",
            customerId, order.OrderId, reasonCode, asked.ReturnId);

        return new ResultDto
        {
            Result = 1,
            Messages = new[]
            {
                $"We have your return request for {orderNumber}. Our team will review it and tell you what " +
                "happens next - you can take the request back from this page while it is still waiting.",
                "Once the parcel is back with us and the return is closed, the money goes back to the payment " +
                "method you used, usually within 5-7 working days of the refund being approved."
            }
        };
    }

    /// <summary>
    /// Takes a return request back, while the shop team has not answered it yet - the customer's way out of
    /// asking by mistake. Nothing else moves: no parcel was booked, nothing was refunded and the order is
    /// exactly as it was, so the customer is free to ask again while the window is open.
    /// </summary>
    public async Task<ResultDto> WithdrawReturnAsync(long customerId, long orderId)
    {
        var order = await _context.Orders
            .Include(o => o.OrderStatus)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order == null || order.CustomerId != customerId)
        {
            _logger.LogWarning(
                "Taking back a return was refused: order {OrderId} was not found for customer {CustomerId}.",
                orderId, customerId);

            return Refusal("Order not found.");
        }

        var openReturn = await OrderReturnFlow.FindOpenAsync(_context, order.OrderId);

        if (openReturn == null)
        {
            return Refusal($"There is no return request waiting on {OrderNumber(order.OrderId)}.");
        }

        var (ok, message) = await OrderReturnFlow.WithdrawAsync(
            _context, order, openReturn, await GetCustomerEmailAsync(customerId), DateTime.UtcNow);

        if (!ok)
            return Refusal(message);

        _logger.LogInformation(
            "Customer {CustomerId} took back return request {ReturnId} of order {OrderId}.",
            customerId, openReturn.ReturnId, order.OrderId);

        return new ResultDto { Result = 1, Messages = new[] { message } };
    }

    /// <summary>
    /// The customer's e-mail - what a customer action is written down as on the return row and in the order
    /// history ('Requested by ...'): a customer has no login id here, and their e-mail is what the shop team
    /// can find them by.
    /// </summary>
    private async Task<string?> GetCustomerEmailAsync(long customerId) =>
        await _context.Customers
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .Select(c => c.EmailId)
            .FirstOrDefaultAsync();

    /// <summary>Result = 0 with one sentence for the customer - what 'My Orders' shows as an error.</summary>
    private static ResultDto Refusal(string message) => new() { Result = 0, Messages = new[] { message } };
}
