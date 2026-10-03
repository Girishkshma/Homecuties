using HC.Business;
using Xunit;

namespace HC.Tests;

/// <summary>
/// Where a unit sits and when it can be sold again - the one rule the storefront counters, the checkout
/// lock and the order statuses all share (see SkuAvailability).
/// </summary>
public class SkuAvailabilityTests
{
    /// <summary>
    /// The pool ids are what SKUStatuses and OrderStatuses hold in the live database. A wrong number here
    /// would move units into a pool nobody counts, or release none at all.
    /// </summary>
    [Fact]
    public void ThePoolIdsAreTheOnesTheDatabaseHolds()
    {
        Assert.Equal(1, SkuAvailability.AvailableSkuStatusId);
        Assert.Equal(2, SkuAvailability.DamageSkuStatusId);
        Assert.Equal(4, SkuAvailability.OrderedSkuStatusId);
        Assert.Equal(5, SkuAvailability.DispatchedSkuStatusId);
        Assert.Equal(6, SkuAvailability.ReturnedSkuStatusId);
        Assert.Equal(7, SkuAvailability.DeliveredSkuStatusId);

        Assert.Equal(5, SkuAvailability.CancelledOrderStatusId);
        Assert.Equal(6, SkuAvailability.ReturnedOrderStatusId);
    }

    /// <summary>
    /// The two ends of an order release their units: cancelling puts them back on the shelf there and then,
    /// and a return does the same once the parcel is back. Every other status still holds its unit - which
    /// is what keeps a delivered order's unit out of the sellable pool.
    /// </summary>
    [Theory]
    [InlineData(OrderStatusFlow.Pending, false)]
    [InlineData(OrderStatusFlow.Confirmed, false)]
    [InlineData(OrderStatusFlow.Shipped, false)]
    [InlineData(OrderStatusFlow.Delivered, false)]
    [InlineData(OrderStatusFlow.Cancelled, true)]
    [InlineData(OrderStatusFlow.Returned, true)]
    public void ACancelledOrReturnedOrderReleasesItsUnits(short statusId, bool expected)
    {
        Assert.Equal(expected, SkuAvailability.ReleasesUnits(statusId));
    }

    /// <summary>
    /// Closing a return puts its units back in the sellable pool. It has to accept every pool a return can
    /// start from: the 'Returned' one when the parcel was booked back in by hand, and the delivery pools
    /// when the order was closed straight from the admin screen.
    /// </summary>
    [Fact]
    public void ClosingAReturnPutsItsUnitsBackOnTheShelf()
    {
        var (from, to) = SkuAvailability.PoolsForOrderStatus(OrderStatusFlow.Returned);

        Assert.Equal(SkuAvailability.AvailableSkuStatusId, to);
        Assert.Contains(SkuAvailability.ReturnedSkuStatusId, from);
        Assert.Contains(SkuAvailability.DeliveredSkuStatusId, from);
        Assert.Contains(SkuAvailability.DispatchedSkuStatusId, from);
        Assert.Contains(SkuAvailability.OrderedSkuStatusId, from);
    }

    /// <summary>
    /// The steps that were there before the returns flow are untouched by it: dispatching, delivering and
    /// cancelling move units exactly where they did.
    /// </summary>
    [Fact]
    public void TheOtherStepsAreUnchanged()
    {
        var shipped = SkuAvailability.PoolsForOrderStatus(OrderStatusFlow.Shipped);
        Assert.Equal(new[] { SkuAvailability.OrderedSkuStatusId }, shipped.From);
        Assert.Equal(SkuAvailability.DispatchedSkuStatusId, shipped.To);

        var delivered = SkuAvailability.PoolsForOrderStatus(OrderStatusFlow.Delivered);
        Assert.Contains(SkuAvailability.OrderedSkuStatusId, delivered.From);
        Assert.Contains(SkuAvailability.DispatchedSkuStatusId, delivered.From);
        Assert.Equal(SkuAvailability.DeliveredSkuStatusId, delivered.To);

        var cancelled = SkuAvailability.PoolsForOrderStatus(OrderStatusFlow.Cancelled);
        Assert.Equal(SkuAvailability.AvailableSkuStatusId, cancelled.To);
        Assert.DoesNotContain(SkuAvailability.DeliveredSkuStatusId, cancelled.From);
    }

    /// <summary>
    /// Confirming an order (the money arrived) moves nothing, and a status nobody knows moves nothing
    /// either - a stock counter must never invent a pool.
    /// </summary>
    [Theory]
    [InlineData(OrderStatusFlow.Pending)]
    [InlineData(OrderStatusFlow.Confirmed)]
    [InlineData(99)]
    public void AStepThatHasNothingToSayAboutStockMovesNothing(short statusId)
    {
        var (from, to) = SkuAvailability.PoolsForOrderStatus(statusId);

        Assert.Empty(from);
        Assert.Equal(0, to);
    }

    /// <summary>
    /// The units of a returned parcel may be in any of the delivery pools or in the shop's own 'Returned' one -
    /// one list, read by the receipt, the inspection and the close, so the inspection can never miss a unit that
    /// is physically in the shop and the close can never put a written-off unit back on sale. The 'Damage' pool is
    /// deliberately not in it: that is exactly where a written-off unit goes to be out of the way for good.
    /// </summary>
    [Fact]
    public void AReturnedParcelIsLookedAtInEveryPoolItCanComeBackIn()
    {
        Assert.Equal(
            new[]
            {
                SkuAvailability.OrderedSkuStatusId, SkuAvailability.DispatchedSkuStatusId,
                SkuAvailability.DeliveredSkuStatusId, SkuAvailability.ReturnedSkuStatusId
            },
            SkuAvailability.ReturnedParcelPools);

        var (from, to) = SkuAvailability.PoolsForOrderStatus(OrderStatusFlow.Returned);

        Assert.Equal(SkuAvailability.ReturnedParcelPools, from);
        Assert.Equal(SkuAvailability.AvailableSkuStatusId, to);

        Assert.DoesNotContain(SkuAvailability.DamageSkuStatusId, SkuAvailability.ReturnedParcelPools);
    }

    /// <summary>
    /// The sellable rule is a database query, so it has to be an expression that can be built at all - and
    /// the two statuses it releases a unit from are the two the order lifecycle ends at.
    /// </summary>
    [Fact]
    public void TheSellableRuleIsBuilt()
    {
        Assert.NotNull(SkuAvailability.IsAvailableQuery);
        Assert.Equal(typeof(Data.Entities.Sku), SkuAvailability.IsAvailableQuery.Parameters.Single().Type);
    }
}
