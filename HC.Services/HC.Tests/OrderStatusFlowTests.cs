using HC.Business;
using Xunit;

namespace HC.Tests;

/// <summary>
/// The order lifecycle, which both screens read: the steps the admin area may take, the buttons the
/// storefront offers, and what a returned order means for the money and the stock.
/// </summary>
public class OrderStatusFlowTests
{
    /// <summary>
    /// Orders.OrderStatusID values are what SeedOrderStatuses.sql writes and what every stored order
    /// carries, so the constants have to be exactly those numbers.
    /// </summary>
    [Fact]
    public void TheStatusIdsAreTheOnesTheDatabaseHolds()
    {
        Assert.Equal(1, OrderStatusFlow.Pending);
        Assert.Equal(2, OrderStatusFlow.Confirmed);
        Assert.Equal(3, OrderStatusFlow.Shipped);
        Assert.Equal(4, OrderStatusFlow.Delivered);
        Assert.Equal(5, OrderStatusFlow.Cancelled);
        Assert.Equal(6, OrderStatusFlow.Returned);
    }

    /// <summary>
    /// A delivered order has exactly one way out: the customer keeps it, or it comes back. Everything
    /// else (cancelling, dispatching again) is not a move the map allows.
    /// </summary>
    [Fact]
    public void ADeliveredOrderOnlyLeadsToReturned()
    {
        Assert.Equal(new[] { OrderStatusFlow.Returned }, OrderStatusFlow.NextFrom(OrderStatusFlow.Delivered));

        Assert.False(OrderStatusFlow.CanMove(OrderStatusFlow.Delivered, OrderStatusFlow.Shipped));
        Assert.False(OrderStatusFlow.CanMove(OrderStatusFlow.Delivered, OrderStatusFlow.Cancelled));
    }

    /// <summary>
    /// A parcel the courier reports as coming back is raised on a Shipped order, so 'Returned' has to be
    /// reachable from there - and cancelling stays possible until the parcel is really back.
    /// </summary>
    [Fact]
    public void AShippedOrderCanStillComeBack()
    {
        var next = OrderStatusFlow.NextFrom(OrderStatusFlow.Shipped);

        Assert.Contains(OrderStatusFlow.Delivered, next);
        Assert.Contains(OrderStatusFlow.Returned, next);
        Assert.Contains(OrderStatusFlow.Cancelled, next);
    }

    /// <summary>Returned and Cancelled are both the end of the line - nothing may follow either.</summary>
    [Fact]
    public void ReturnedAndCancelledAreTerminal()
    {
        Assert.Empty(OrderStatusFlow.NextFrom(OrderStatusFlow.Returned));
        Assert.Empty(OrderStatusFlow.NextFrom(OrderStatusFlow.Cancelled));
    }

    /// <summary>
    /// A returned order is not money we are holding any more: the sale has been reversed, so the money is
    /// owed back and the refund is what the order screen shows (OrderPaymentStatus.RefundRequested).
    /// </summary>
    [Fact]
    public void AReturnedOrderIsNotPaidAnyMore()
    {
        Assert.False(OrderStatusFlow.IsPaid(OrderStatusFlow.Returned));

        Assert.True(OrderStatusFlow.IsPaid(OrderStatusFlow.Confirmed));
        Assert.True(OrderStatusFlow.IsPaid(OrderStatusFlow.Shipped));
        Assert.True(OrderStatusFlow.IsPaid(OrderStatusFlow.Delivered));
        Assert.False(OrderStatusFlow.IsPaid(OrderStatusFlow.Pending));
        Assert.False(OrderStatusFlow.IsPaid(OrderStatusFlow.Cancelled));
    }

    /// <summary>
    /// Once a parcel has gone out the order is never cancelled from the storefront again - and a returned
    /// order is the far end of that same journey, not a cancellation.
    /// </summary>
    [Fact]
    public void AReturnedOrderHasShipped()
    {
        Assert.True(OrderStatusFlow.HasShipped(OrderStatusFlow.Returned));
        Assert.True(OrderStatusFlow.HasShipped(OrderStatusFlow.Shipped));
        Assert.True(OrderStatusFlow.HasShipped(OrderStatusFlow.Delivered));

        Assert.False(OrderStatusFlow.HasShipped(OrderStatusFlow.Pending));
        Assert.False(OrderStatusFlow.HasShipped(OrderStatusFlow.Confirmed));
        Assert.False(OrderStatusFlow.HasShipped(OrderStatusFlow.Cancelled));
    }

    /// <summary>
    /// Only a delivered order can be sent back by the customer: before that nothing is theirs to send, and
    /// after it the order is either already returned or was cancelled. This is the same rule 'My Orders'
    /// offers the Return button on.
    /// </summary>
    [Theory]
    [InlineData(OrderStatusFlow.Pending, false)]
    [InlineData(OrderStatusFlow.Confirmed, false)]
    [InlineData(OrderStatusFlow.Shipped, false)]
    [InlineData(OrderStatusFlow.Delivered, true)]
    [InlineData(OrderStatusFlow.Cancelled, false)]
    [InlineData(OrderStatusFlow.Returned, false)]
    public void OnlyADeliveredOrderCanBeReturned(short statusId, bool expected)
    {
        Assert.Equal(expected, OrderStatusFlow.CanCustomerReturn(statusId));
    }

    /// <summary>
    /// The storefront may only cancel while the order is still in the shop's hands - a returned order is
    /// past that as well, so the button and the server agree on it.
    /// </summary>
    [Theory]
    [InlineData(OrderStatusFlow.Pending, true)]
    [InlineData(OrderStatusFlow.Confirmed, true)]
    [InlineData(OrderStatusFlow.Shipped, false)]
    [InlineData(OrderStatusFlow.Delivered, false)]
    [InlineData(OrderStatusFlow.Returned, false)]
    public void OnlyAnOrderStillWithTheShopIsCancellable(short statusId, bool expected)
    {
        Assert.Equal(expected, OrderStatusFlow.CanCustomerCancel(statusId));
    }
}
