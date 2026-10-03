using HC.Business;
using Xunit;

namespace HC.Tests;

/// <summary>
/// Where the return window starts and ends. It runs from the day the parcel reached the customer - what the
/// courier reported, or the order history when only a person knows - and an order nobody can date is not
/// refused on a date we do not have.
/// </summary>
public class OrderReturnWindowTests
{
    private const int WindowDays = 7;

    /// <summary>The window runs from the day the parcel reached the customer, not from the order date.</summary>
    [Fact]
    public void TheWindowIsCountedFromTheDayTheParcelArrived()
    {
        var order = TestOrders.Delivered(courierSays: TestOrders.DeliveredOn);

        Assert.Equal(TestOrders.DeliveredOn, OrderReturnFlow.DeliveredOn(order));
        Assert.Equal(TestOrders.DeliveredOn.AddDays(WindowDays), OrderReturnFlow.WindowEndsOn(order, WindowDays));
    }

    /// <summary>
    /// The last day is still inside the window ('send it back by the 8th' means the 8th counts) and the
    /// moment after it is not - the boundary is the one the page shows the customer.
    /// </summary>
    [Fact]
    public void TheLastDayOfTheWindowIsStillInsideIt()
    {
        var endsOn = TestOrders.DeliveredOn.AddDays(WindowDays);

        Assert.True(OrderReturnFlow.IsWithinWindow(endsOn, endsOn));
        Assert.False(OrderReturnFlow.IsWithinWindow(endsOn, endsOn.AddSeconds(1)));
        Assert.True(OrderReturnFlow.IsWithinWindow(endsOn, endsOn.AddDays(-1)));
    }

    /// <summary>
    /// What the courier reported is the truth when it is known; the order history is only the stand-in for
    /// an order a person moved to Delivered by hand.
    /// </summary>
    [Fact]
    public void TheCouriersDateBeatsTheOrderHistory()
    {
        var order = TestOrders.Delivered(
            courierSays: TestOrders.DeliveredOn,
            historySays: TestOrders.DeliveredOn.AddDays(3));

        Assert.Equal(TestOrders.DeliveredOn, OrderReturnFlow.DeliveredOn(order));
    }

    /// <summary>
    /// An order moved to Delivered by hand has no parcel date, and the history row written with that move is
    /// what says when it arrived - the newest one, because a corrected move writes a second row.
    /// </summary>
    [Fact]
    public void WithoutACourierDateTheOrderHistorySaysWhenItArrived()
    {
        var order = TestOrders.Delivered(
            historySays: TestOrders.DeliveredOn,
            secondHistoryRowOn: TestOrders.DeliveredOn.AddDays(2));

        Assert.Equal(TestOrders.DeliveredOn.AddDays(2), OrderReturnFlow.DeliveredOn(order));
    }

    /// <summary>A parcel that came back says nothing about when the order reached the customer.</summary>
    [Fact]
    public void TheReturnLegIsNeverReadAsTheDelivery()
    {
        var order = TestOrders.Delivered(courierSays: TestOrders.DeliveredOn, asTheReturnLegOnly: true);

        Assert.Null(OrderReturnFlow.DeliveredOn(order));
        Assert.Null(OrderReturnFlow.WindowEndsOn(order, WindowDays));
    }

    /// <summary>
    /// When nothing anywhere says when the parcel arrived there is no date to refuse the customer on, so the
    /// window is left open rather than closing a return on a date we do not have.
    /// </summary>
    [Fact]
    public void AnUnknownDeliveryDateLeavesTheWindowOpen()
    {
        var order = TestOrders.Delivered();

        Assert.Null(OrderReturnFlow.DeliveredOn(order));
        Assert.Null(OrderReturnFlow.WindowEndsOn(order, WindowDays));
        Assert.True(OrderReturnFlow.IsWithinWindow(null, TestOrders.DeliveredOn));
        Assert.True(OrderReturnFlow.IsWithinWindow(null, TestOrders.DeliveredOn.AddYears(5)));
    }
}
