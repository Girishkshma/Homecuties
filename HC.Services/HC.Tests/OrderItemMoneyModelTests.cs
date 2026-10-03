using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HC.Tests;

/// <summary>
/// The two tables the per-unit books are written in, pinned where they can be checked without a database: the
/// schema scripts (HC.Services/HC.Data/Scripts) create them by hand, and the context maps them by hand, so nothing
/// but a test stops the two from drifting apart. A wrong column, a key on the wrong pair or a cascade that was not
/// meant is otherwise only found by a shop keeper whose order screen has stopped loading.
///
/// The model is built here, never used against a server: a connection string is needed to name a provider, and
/// nothing in this test opens a connection with it.
/// </summary>
public class OrderItemMoneyModelTests
{
    private static HomecutiesDbContext Context() =>
        new(new DbContextOptionsBuilder<HomecutiesDbContext>()
            .UseSqlServer("Server=none;Database=none;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);

    /// <summary>
    /// What each order line carried, money-wise, is one row per line - the order's id and the SKU its line names -
    /// in a table whose money columns are wide enough for an order-level figure (decimal(18,2), the same width the
    /// payment row uses). A key on anything else would let one line have two rows, or two lines share one.
    /// </summary>
    [Fact]
    public void EachOrderLineHasOnePerUnitMoneyRow()
    {
        using var context = Context();
        var money = context.Model.FindEntityType(typeof(OrderItemMoney));

        Assert.NotNull(money);
        Assert.Equal("OrderItemMoney", money!.GetTableName());
        Assert.Equal(
            new[] { "OrderId", "Sku" },
            money.FindPrimaryKey()!.Properties.Select(property => property.Name));

        Assert.Equal("decimal(18,2)", money.FindProperty(nameof(OrderItemMoney.OutputGst))!.GetColumnType());
        Assert.Equal("decimal(18,2)", money.FindProperty(nameof(OrderItemMoney.GatewayFee))!.GetColumnType());
        Assert.Equal("decimal(18,2)", money.FindProperty(nameof(OrderItemMoney.GatewayTax))!.GetColumnType());
        Assert.Equal("decimal(18,2)", money.FindProperty(nameof(OrderItemMoney.FreightShare))!.GetColumnType());
        Assert.Equal(20, money.FindProperty(nameof(OrderItemMoney.ChargesSource))!.GetMaxLength());
    }

    /// <summary>
    /// What one parcel carries: one row per SKU of it, keyed by its own id, in a table that hangs off the parcel -
    /// deleting a parcel takes its contents with it, because a row naming no parcel describes goods that are
    /// nowhere. Its columns are the widths the code writes (a SKU of 20, a leg of 10), so the database cannot
    /// silently shorten a value the parcel was recorded with.
    /// </summary>
    [Fact]
    public void OneParcelsGoodsAreItsOwnRows()
    {
        using var context = Context();
        var items = context.Model.FindEntityType(typeof(OrderShipmentItem));

        Assert.NotNull(items);
        Assert.Equal("OrderShipmentItems", items!.GetTableName());
        Assert.Equal(
            new[] { "ShipmentItemId" },
            items.FindPrimaryKey()!.Properties.Select(property => property.Name));

        Assert.Equal(20, items.FindProperty(nameof(OrderShipmentItem.Sku))!.GetMaxLength());
        Assert.Equal(10, items.FindProperty(nameof(OrderShipmentItem.Direction))!.GetMaxLength());

        var toParcel = items.GetForeignKeys()
            .Single(key => key.PrincipalEntityType.ClrType == typeof(OrderShipment));

        Assert.Equal(DeleteBehavior.Cascade, toParcel.DeleteBehavior);
    }

    /// <summary>
    /// A parcel is a row of its own that an order may have several of, and what it carries is read through its own
    /// collection (<c>OrderShipment.Items</c>): the whole of this change is that one order can go out in more than
    /// one parcel, and a navigation that could not hold two would undo it silently.
    /// </summary>
    [Fact]
    public void AnOrderCanHaveMoreThanOneParcelWithGoodsInIt()
    {
        using var context = Context();
        var shipment = context.Model.FindEntityType(typeof(OrderShipment))!;

        var toOrder = shipment.GetForeignKeys()
            .Single(key => key.PrincipalEntityType.ClrType == typeof(Order));

        // One order, many parcels - and the collection the goods are read through.
        Assert.Equal(typeof(ICollection<OrderShipment>), toOrder.PrincipalToDependent?.ClrType);

        var items = context.Model.FindEntityType(typeof(OrderShipmentItem))!;
        var parcelItems = items.GetNavigations()
            .Single(navigation => navigation.Name == nameof(OrderShipmentItem.Shipment));

        Assert.Equal(typeof(ICollection<OrderShipmentItem>), parcelItems.Inverse?.ClrType);
    }
}
