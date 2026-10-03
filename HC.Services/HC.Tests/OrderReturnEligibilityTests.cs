using HC.Business;
using HC.Data.Entities;
using Xunit;

namespace HC.Tests;

/// <summary>
/// Who may ask for a return: the three conditions the Return button in 'My Orders' and the server's own
/// check share (delivered, nothing already waiting, inside the window), so the two can never disagree.
/// </summary>
public class OrderReturnEligibilityTests
{
    private const int WindowDays = 7;

    /// <summary>Each of the three conditions alone is enough to refuse the ask.</summary>
    [Fact]
    public void TheCustomerMayAskOnlyForADeliveredOrderInsideTheWindowWithNothingWaiting()
    {
        var askedOn = TestOrders.DeliveredOn.AddDays(2);
        var delivered = TestOrders.Delivered(courierSays: TestOrders.DeliveredOn);

        Assert.True(OrderReturnFlow.CanCustomerAsk(delivered, null, WindowDays, askedOn));

        // Not delivered yet: nothing is the customer's to send back.
        var shipped = TestOrders.Delivered(OrderStatusFlow.Shipped, courierSays: TestOrders.DeliveredOn);
        Assert.False(OrderReturnFlow.CanCustomerAsk(shipped, null, WindowDays, askedOn));

        // An ask already waiting: a second one is refused (and the database refuses it too).
        var alreadyOpen = new OrderReturn
        {
            OrderId = delivered.OrderId,
            Status = OrderReturnStatus.Requested
        };
        Assert.False(OrderReturnFlow.CanCustomerAsk(delivered, alreadyOpen, WindowDays, askedOn));

        // Past the window.
        Assert.False(OrderReturnFlow.CanCustomerAsk(
            delivered, null, WindowDays, TestOrders.DeliveredOn.AddDays(WindowDays + 1)));
    }

    /// <summary>
    /// An order that has already been returned once (or cancelled) is not offered again, however the return
    /// ended - the status is what decides, exactly as the server does.
    /// </summary>
    [Theory]
    [InlineData(OrderStatusFlow.Pending)]
    [InlineData(OrderStatusFlow.Confirmed)]
    [InlineData(OrderStatusFlow.Shipped)]
    [InlineData(OrderStatusFlow.Cancelled)]
    [InlineData(OrderStatusFlow.Returned)]
    public void AnyOtherStatusRefusesTheAsk(short statusId)
    {
        var order = TestOrders.Delivered(statusId, courierSays: TestOrders.DeliveredOn);

        Assert.False(OrderReturnFlow.CanCustomerAsk(
            order, null, WindowDays, TestOrders.DeliveredOn.AddDays(1)));
    }

    /// <summary>A window of no days at all still covers the day the parcel arrived, and not the one after.</summary>
    [Fact]
    public void AWindowOfNoDaysStillCoversTheDayItArrived()
    {
        var delivered = TestOrders.Delivered(courierSays: TestOrders.DeliveredOn);

        Assert.True(OrderReturnFlow.CanCustomerAsk(delivered, null, 0, TestOrders.DeliveredOn));
        Assert.False(OrderReturnFlow.CanCustomerAsk(delivered, null, 0, TestOrders.DeliveredOn.AddDays(1)));
    }

    /// <summary>
    /// The default window is the one documented for 'Returns:WindowDays': a shop that never configured it
    /// still promises its customers a window rather than refusing every return.
    /// </summary>
    [Fact]
    public void TheDefaultWindowIsTheDocumentedOne()
    {
        Assert.Equal(7, OrderReturnFlow.DefaultWindowDays);
        Assert.True(OrderReturnFlow.DefaultWindowDays > 0);
    }
}
