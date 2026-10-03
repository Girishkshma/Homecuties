using System.Linq.Expressions;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

/// <summary>
/// The single definition of "this physical unit can be sold" used by the storefront
/// (product list, cart, wish list and the checkout stock lock) - and of which pool the unit sits in
/// once an order moves on.
///
/// A unit is sellable while it sits in the "Available" SKU pool AND it is not attached to an order
/// that is still live: placing an order moves the unit to "Ordered", cancelling the order puts it
/// back to "Available" (see OrderService.CancelOrderAsync), the same happens when a return closes
/// (<see cref="OrderStatusFlow.Returned"/>) and the OrderItems row the unit left behind stops counting
/// against it. The order status - not the mere existence of an OrderItems row - therefore decides
/// whether a unit is gone for good, so a cancelled or returned order's unit shows up in the stock
/// counters (and can be bought) again.
///
/// The rule lives here once (<see cref="IsAvailableQuery"/>) and is applied by
/// <see cref="CountSellableByProductAsync"/>. OrderService's checkout lock repeats it as raw SQL
/// (LockAvailableSkusAsync) because it has to lock the rows while it reads them.
///
/// The other half - which pool an order's units move to when the order moves on - is here too
/// (<see cref="PoolsForOrderStatus"/> and <see cref="MoveUnitsForOrderStatusAsync"/>), so the stock
/// counters and the order status can never describe two different things.
/// </summary>
public static class SkuAvailability
{
    /// <summary>SKUStatuses.SKUStatusID = 1 => "Available".</summary>
    public const short AvailableSkuStatusId = 1;

    /// <summary>SKUStatuses.SKUStatusID = 4 => "Ordered".</summary>
    public const short OrderedSkuStatusId = 4;

    /// <summary>SKUStatuses.SKUStatusID = 5 => "Dispatched" (the parcel is with the courier).</summary>
    public const short DispatchedSkuStatusId = 5;

    /// <summary>SKUStatuses.SKUStatusID = 7 => "Delivered".</summary>
    public const short DeliveredSkuStatusId = 7;

    /// <summary>Orders.OrderStatusID = 5 => "Cancelled" (see HC.Data/Scripts/SeedOrderStatuses.sql).</summary>
    public const short CancelledOrderStatusId = 5;

    /// <summary>
    /// Orders.OrderStatusID = 6 => "Returned" - a delivered (or refused) order whose parcel came back.
    /// Like a cancellation this is the point where the units stop belonging to the order, which is why it
    /// is released in <see cref="IsAvailableQuery"/> exactly like <see cref="CancelledOrderStatusId"/>.
    /// </summary>
    public const short ReturnedOrderStatusId = 6;

    /// <summary>
    /// SKUStatuses.SKUStatusID = 6 => "Returned": the unit is physically back with the shop after a
    /// return (it is the pool <c>MoveUnitsForOrderStatusAsync</c> puts a received return's units in, and
    /// the one they leave for <see cref="AvailableSkuStatusId"/> when the return closes - or for
    /// <see cref="DamageSkuStatusId"/> when the inspection finds a unit broken).
    /// </summary>
    public const short ReturnedSkuStatusId = 6;

    /// <summary>
    /// SKUStatuses.SKUStatusID = 2 => "Damage": a unit that came back broken (or was broken in the
    /// shop). It is out of every sellable pool for good - only the shop team moving it back can sell it
    /// again (see <c>MarkUnitsDamagedAsync</c>).
    /// </summary>
    public const short DamageSkuStatusId = 2;

    /// <summary>
    /// True when an order with this status has given its units back: a cancelled order (its units went
    /// back on the shelf there and then) or a returned one (the same thing, after the parcel came back).
    /// These are the two ends of an order an item is no longer held by - and they are why a cancelled or
    /// returned order's unit shows up in the stock counters and can be bought again.
    /// </summary>
    public static bool ReleasesUnits(short orderStatusId) =>
        orderStatusId is CancelledOrderStatusId or ReturnedOrderStatusId;

    /// <summary>
    /// The rule above, written for the database. <c>OrderItems.OrderID</c> is a required foreign key,
    /// so an order item always belongs to an order, and only an order that gave its unit back
    /// (<see cref="ReleasesUnits"/>) stops holding it.
    ///
    /// Keep this in step with OrderService.LockAvailableSkusAsync, the raw-SQL twin of this rule (it
    /// repeats it because it has to lock the rows while it reads them).
    /// </summary>
    public static readonly Expression<Func<Sku, bool>> IsAvailableQuery = sku =>
        sku.SkustatusId == AvailableSkuStatusId &&
        !sku.OrderItems.Any(oi =>
            oi.Order.OrderStatusId != CancelledOrderStatusId &&
            oi.Order.OrderStatusId != ReturnedOrderStatusId);

    /// <summary>
    /// How many units of each of the given products can still be sold, counted in the database with
    /// <see cref="IsAvailableQuery"/>. Every requested product id is present in the result (0 when
    /// the product has no sellable unit at all), so a caller can index the result without checking.
    /// </summary>
    public static async Task<Dictionary<int, int>> CountSellableByProductAsync(
        HomecutiesDbContext context,
        IEnumerable<int> productIds,
        CancellationToken cancellationToken = default)
    {
        var ids = productIds.Distinct().ToList();
        var counts = ids.ToDictionary(productId => productId, _ => 0);

        if (ids.Count == 0)
            return counts;

        // One row per sellable physical unit, gathered in a single query and counted here - the list
        // is short (a handful of rows per product) and this avoids a second GROUP BY round trip.
        var sellableProductIds = await context.Skus
            .Where(IsAvailableQuery)
            .Where(s => ids.Contains(s.PurchaseDetail.ProductId))
            .Select(s => s.PurchaseDetail.ProductId)
            .ToListAsync(cancellationToken);

        foreach (var productId in sellableProductIds)
        {
            counts[productId]++;
        }

        return counts;
    }

    /// <summary>
    /// Where an order status change moves the order's units: the pools a unit may come FROM and the one
    /// it goes TO. Confirming an order (the money arrived) leaves them 'Ordered' - no move at all.
    ///
    /// A returned order puts its units back in the sellable pool: the parcel came back, so the unit is
    /// the shop's again. Which is NOT the same as saying it can be sold - an inspection that finds a unit
    /// broken moves it out again (<c>MarkUnitsDamagedAsync</c>), so the two steps are deliberately apart.
    /// </summary>
    public static (short[] From, short To) PoolsForOrderStatus(short orderStatusId) => orderStatusId switch
    {
        OrderStatusFlow.Shipped => (new[] { OrderedSkuStatusId }, DispatchedSkuStatusId),
        OrderStatusFlow.Delivered => (new[] { OrderedSkuStatusId, DispatchedSkuStatusId }, DeliveredSkuStatusId),
        OrderStatusFlow.Cancelled => (new[] { OrderedSkuStatusId, DispatchedSkuStatusId }, AvailableSkuStatusId),

        // Where a return's units are when the order closes depends on how far the parcel got: a return the
        // shop booked back in put them in the 'Returned' pool, while an order closed straight from the admin
        // screen may leave them where the delivery left them - all of those came back with the customer (or
        // never left for a refusal), so they are all sellable again. The pool list itself lives once, in
        // ReturnedParcelPools, because the inspection has to look at exactly the same set of units.
        OrderStatusFlow.Returned => (ReturnedParcelPools, AvailableSkuStatusId),

        _ => (Array.Empty<short>(), (short)0)
    };

    /// <summary>
    /// Moves the units of an order into the pool its new status calls for (see
    /// <see cref="PoolsForOrderStatus"/>), writing a SKU history row for each move as the shop's stock
    /// trail, and returns how many units moved.
    ///
    /// This is the single definition of 'what happens to the stock when the order moves', used by the
    /// shop team changing a status (AdminDashboardService) and by the order following its parcel
    /// (ShipmentTrackingService) - so a courier scan cannot leave the stock counters describing a
    /// different order than the screen does.
    /// </summary>
    public static async Task<int> MoveUnitsForOrderStatusAsync(
        HomecutiesDbContext context,
        Order order,
        short orderStatusId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var (fromStatuses, toStatus) = PoolsForOrderStatus(orderStatusId);

        if (fromStatuses.Length == 0)
            return 0;

        return await MoveUnitsAsync(
            context,
            order.OrderItems.Select(item => item.Sku),
            fromStatuses,
            toStatus,
            now,
            cancellationToken);
    }

    /// <summary>
    /// Moves the named units out of any of the given pools and into <paramref name="toSkuStatusId"/>, writing
    /// a SKU history row for each move as the shop's stock trail, and answers with how many units really moved.
    ///
    /// This is the one primitive every stock move goes through (<see cref="MoveUnitsForOrderStatusAsync"/>,
    /// <see cref="MarkUnitsReturnedAsync"/>, <see cref="MarkUnitsDamagedAsync"/>,
    /// <see cref="ReleaseReturnedUnitsAsync"/>). A unit that is not in one of the named pools is left exactly
    /// where it is and not counted - so a unit that is already sold again, or already written off, can never be
    /// moved twice by one call.
    /// </summary>
    public static async Task<int> MoveUnitsAsync(
        HomecutiesDbContext context,
        IEnumerable<string> skus,
        short[] fromSkuStatusIds,
        short toSkuStatusId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var moved = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var skuNumber in skus)
        {
            // One row per physical unit, so the same SKU asked for twice is one unit - and a blank one is not
            // a unit at all (it comes from an order item that was never given one).
            if (string.IsNullOrWhiteSpace(skuNumber) || !seen.Add(skuNumber))
                continue;

            var sku = await context.Skus.FirstOrDefaultAsync(s => s.Sku1 == skuNumber, cancellationToken);
            if (sku == null || !fromSkuStatusIds.Contains(sku.SkustatusId))
                continue;

            sku.SkustatusId = toSkuStatusId;

            context.Skuhistories.Add(new Skuhistory
            {
                Sku = sku.Sku1,
                InventoryId = sku.InventoryId,
                SkustatusId = toSkuStatusId,
                HistoryDate = now
            });

            moved++;
        }

        return moved;
    }

    /// <summary>Where an order's units are once it has left the shop, until a return brings them back.</summary>
    private static readonly short[] AwayFromTheShopPools =
    {
        OrderedSkuStatusId, DispatchedSkuStatusId, DeliveredSkuStatusId
    };

    /// <summary>
    /// The pools a returned parcel's units may be in when it comes back: the delivery pools (the parcel came
    /// back with the customer, or never left for a refusal) and <see cref="ReturnedSkuStatusId"/> (a unit
    /// already booked back in by hand). Named once, and used by every move that acts on a return, so an
    /// inspection can never miss a unit that is physically in the shop - see
    /// <see cref="PoolsForOrderStatus"/> for the close, which is the same set.
    /// </summary>
    public static readonly short[] ReturnedParcelPools =
    {
        OrderedSkuStatusId, DispatchedSkuStatusId, DeliveredSkuStatusId, ReturnedSkuStatusId
    };

    /// <summary>
    /// The parcel of a returned order is physically back with the shop: its units leave the delivery pools for
    /// <see cref="ReturnedSkuStatusId"/>, which is what the shop team's 'parcel received' step does.
    ///
    /// It is deliberately NOT the sellable pool, and deliberately not the last word either: a unit that came
    /// back is not on sale again until the return is closed (<see cref="ReleaseReturnedUnitsAsync"/>), and the
    /// inspection of what came back in has its say in between (<see cref="MarkUnitsDamagedAsync"/>). Returns
    /// how many units came back.
    /// </summary>
    public static Task<int> MarkUnitsReturnedAsync(
        HomecutiesDbContext context,
        Order order,
        DateTime now,
        CancellationToken cancellationToken = default) =>
        MoveUnitsAsync(
            context,
            order.OrderItems.Select(item => item.Sku),
            AwayFromTheShopPools,
            ReturnedSkuStatusId,
            now,
            cancellationToken);

    /// <summary>
    /// Puts a returned order's units back on the shelf - the step that goes with the order becoming
    /// <see cref="OrderStatusFlow.Returned"/> (see <see cref="PoolsForOrderStatus"/>): every unit the return
    /// brought back can be sold again, including one that was booked back in by hand
    /// (<see cref="ReturnedSkuStatusId"/>).
    ///
    /// A unit the inspection wrote off is in none of those pools any more, so it is left exactly where it is
    /// (<see cref="MarkUnitsDamagedAsync"/>): closing a return must never put a broken unit back on sale.
    /// Returns how many units went back on the shelf.
    /// </summary>
    public static Task<int> ReleaseReturnedUnitsAsync(
        HomecutiesDbContext context,
        Order order,
        DateTime now,
        CancellationToken cancellationToken = default) =>
        MoveUnitsForOrderStatusAsync(context, order, OrderStatusFlow.Returned, now, cancellationToken);

    /// <summary>
    /// Writes off the units of a returned parcel the inspection found broken: each named SKU goes to
    /// <see cref="DamageSkuStatusId"/> - out of every sellable pool for good - with a SKU history row that says
    /// so, and the caller writes the sentence on the order's own trail.
    ///
    /// It takes the units by name rather than 'all of the order's units', because an inspection is per unit:
    /// one of three identical tops can come back torn while the other two go on sale again. Only this order's
    /// own units are considered - the requested names are intersected with its items first, so a SKU that
    /// belongs to somebody else's order (the delivery pools are full of them) can never be written off here.
    /// Returns how many units were written off.
    /// </summary>
    public static Task<int> MarkUnitsDamagedAsync(
        HomecutiesDbContext context,
        Order order,
        IEnumerable<string> skus,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var wanted = new HashSet<string>(skus, StringComparer.Ordinal);

        var ownUnits = order.OrderItems
            .Where(item => wanted.Contains(item.Sku))
            .Select(item => item.Sku);

        return MoveUnitsAsync(
            context, ownUnits, ReturnedParcelPools, DamageSkuStatusId, now, cancellationToken);
    }
}

