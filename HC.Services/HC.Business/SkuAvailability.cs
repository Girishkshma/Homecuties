using System.Linq.Expressions;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

/// <summary>
/// The single definition of "this physical unit can be sold" used by the storefront
/// (product list, cart, wish list and the checkout stock lock).
///
/// A unit is sellable while it sits in the "Available" SKU pool AND it is not attached to an order
/// that is still live: placing an order moves the unit to "Ordered", cancelling the order puts it
/// back to "Available" (see OrderService.CancelOrderAsync) and the OrderItems row the unit left
/// behind stops counting against it. The order status - not the mere existence of an OrderItems row -
/// therefore decides whether a unit is gone for good, so a cancelled order's unit shows up in the
/// stock counters (and can be bought) again.
///
/// The rule lives here once (<see cref="IsAvailableQuery"/>) and is applied by
/// <see cref="CountSellableByProductAsync"/>. OrderService's checkout lock repeats it as raw SQL
/// (LockAvailableSkusAsync) because it has to lock the rows while it reads them.
/// </summary>
public static class SkuAvailability
{
    /// <summary>SKUStatuses.SKUStatusID = 1 => "Available".</summary>
    public const short AvailableSkuStatusId = 1;

    /// <summary>SKUStatuses.SKUStatusID = 4 => "Ordered".</summary>
    public const short OrderedSkuStatusId = 4;

    /// <summary>Orders.OrderStatusID = 5 => "Cancelled" (see HC.Data/Scripts/SeedOrderStatuses.sql).</summary>
    public const short CancelledOrderStatusId = 5;

    /// <summary>
    /// The rule above, written for the database. <c>OrderItems.OrderID</c> is a required foreign key,
    /// so an order item always belongs to an order and only a cancelled order gives its unit back.
    /// </summary>
    public static readonly Expression<Func<Sku, bool>> IsAvailableQuery = sku =>
        sku.SkustatusId == AvailableSkuStatusId &&
        !sku.OrderItems.Any(oi => oi.Order.OrderStatusId != CancelledOrderStatusId);

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
}

