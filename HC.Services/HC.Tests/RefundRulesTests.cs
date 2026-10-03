using HC.Business;
using Xunit;

namespace HC.Tests;

/// <summary>
/// When a payment's money is owed back to the customer - the one rule the order list flags 'Refund due' with, the
/// Refund card shows and the dashboard counts (see OrderPaymentStatus.IsRefundOwed).
/// </summary>
public class RefundRulesTests
{
    /// <summary>
    /// An order that gave its units back - cancelled, or returned once its parcel came back - owes the money it
    /// took, whether or not anybody has asked for it yet. The returned half is the one this predicate used to
    /// miss: a returned order's capture was never flagged, so the customer's money could be kept with nothing on
    /// the order saying so.
    /// </summary>
    [Theory]
    [InlineData(OrderStatusFlow.Cancelled)]
    [InlineData(OrderStatusFlow.Returned)]
    public void AnOrderThatGaveItsUnitsBackOwesItsCapture(short orderStatusId)
    {
        Assert.True(OrderPaymentStatus.IsRefundOwed(orderStatusId, OrderPaymentStatus.Captured));
    }

    /// <summary>
    /// An order that still holds its unit owes nothing on a capture: a paid order that was delivered is money we
    /// keep, and a pending one has not been charged at all.
    /// </summary>
    [Theory]
    [InlineData(OrderStatusFlow.Pending)]
    [InlineData(OrderStatusFlow.Confirmed)]
    [InlineData(OrderStatusFlow.Shipped)]
    [InlineData(OrderStatusFlow.Delivered)]
    public void AnOrderThatStillHoldsItsUnitOwesNothingOnACapture(short orderStatusId)
    {
        Assert.False(OrderPaymentStatus.IsRefundOwed(orderStatusId, OrderPaymentStatus.Captured));
    }

    /// <summary>
    /// Once a refund has been asked for - or one failed and is being retried - the money is owed whatever the
    /// order says: the request on the payment row is the shop's own record that somebody has to give it back.
    /// </summary>
    [Theory]
    [InlineData(OrderStatusFlow.Delivered)]
    [InlineData(OrderStatusFlow.Returned)]
    [InlineData(OrderStatusFlow.Cancelled)]
    public void ARequestedOrFailedRefundIsAlwaysOwed(short orderStatusId)
    {
        Assert.True(OrderPaymentStatus.IsRefundOwed(orderStatusId, OrderPaymentStatus.RefundRequested));
        Assert.True(OrderPaymentStatus.IsRefundOwed(orderStatusId, OrderPaymentStatus.RefundFailed));
    }

    /// <summary>
    /// Money that has already gone back is never owed again, and a payment that was never taken is not a refund
    /// either - so neither can leave an order flagged forever.
    /// </summary>
    [Theory]
    [InlineData(OrderStatusFlow.Cancelled)]
    [InlineData(OrderStatusFlow.Returned)]
    public void MoneyThatWentBackIsNeverOwedTwice(short orderStatusId)
    {
        Assert.False(OrderPaymentStatus.IsRefundOwed(orderStatusId, OrderPaymentStatus.Refunded));
        Assert.False(OrderPaymentStatus.IsRefundOwed(orderStatusId, OrderPaymentStatus.Created));
        Assert.False(OrderPaymentStatus.IsRefundOwed(orderStatusId, OrderPaymentStatus.Failed));
        Assert.False(OrderPaymentStatus.IsRefundOwed(orderStatusId, null));
    }
}
