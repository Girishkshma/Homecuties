using HC.Business;
using HC.Data.Entities;

namespace HC.Tests;

/// <summary>
/// Orders built by hand for the rules that only need an order - the returns flow's window and eligibility.
///
/// No database is touched: these rules are read off the order's own legs and history, which is exactly what
/// the services load before they ask. Keeping the fixtures here means a change to what an order carries
/// breaks one file rather than five.
/// </summary>
internal static class TestOrders
{
    /// <summary>The day and time the parcel of the fixture orders reached the customer.</summary>
    public static readonly DateTime DeliveredOn = new(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// An order at the given status, delivered on the date the caller gives it - as the courier reported it
    /// (<paramref name="courierSays"/>), as the order history recorded it (<paramref name="historySays"/>),
    /// both, or neither. Nothing is added when a date is not given.
    /// </summary>
    public static Order Delivered(
        short statusId = OrderStatusFlow.Delivered,
        DateTime? courierSays = null,
        DateTime? historySays = null,
        bool asTheReturnLegOnly = false,
        DateTime? secondHistoryRowOn = null)
    {
        var order = new Order { OrderId = 1001, OrderStatusId = statusId };

        if (courierSays is { } courierDate)
        {
            order.OrderShipments.Add(new OrderShipment
            {
                OrderId = order.OrderId,
                Direction = asTheReturnLegOnly ? OrderShipment.DirectionReverse : OrderShipment.DirectionForward,
                DeliveredOn = courierDate
            });
        }

        if (historySays is { } historyDate)
        {
            order.OrderHistories.Add(DeliveredHistory(order.OrderId, historyDate));
        }

        if (secondHistoryRowOn is { } secondDate)
        {
            order.OrderHistories.Add(DeliveredHistory(order.OrderId, secondDate));
        }

        return order;
    }

    private static OrderHistory DeliveredHistory(long orderId, DateTime when) => new()
    {
        OrderId = orderId,
        HistoryDate = when,
        OrderStatusId = OrderStatusFlow.Delivered,
        Comments = "The parcel was delivered."
    };
}
