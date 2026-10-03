using System;

namespace HC.Data.Entities;

/// <summary>
/// What one parcel carries: the order line (by SKU) and how many units of it went in that parcel.
///
/// A parcel used to be recorded without saying what was in it, which was fine while an order could only go out in
/// one parcel: the courier's bill was the order's, and there was nothing to charge it to. An order can now go out
/// in several parcels - a heavy thing in one, the rest in another - and each parcel's bill belongs to the goods
/// that were really in it, so the shop team picks those units when they record the parcel on the admin order
/// screen (see HC.Business.Shipping.ShipmentTrackingService.SaveAsync) and the bill is split across exactly them
/// (OrderItemMoney.FreightShare).
///
/// One row per SKU of a parcel - '3 x HC-1042' is one row with <see cref="Quantity"/> 3 - so:
///
///   * the same SKU may appear in more than one parcel of the same order (four units shipped two and two are
///     two rows of two, one per parcel), which is why the table's unique index is on
///     (<see cref="ShipmentId"/>, <see cref="Sku"/>) and not on the order;
///   * the parcel quantities of an order must never add up to more units than the order actually holds, which
///     is checked when the parcel is saved rather than by the database (SQL Server cannot state it as a
///     constraint here without a trigger).
///
/// <see cref="Direction"/> is the parcel's own leg copied onto the row, so a reader can tell a forward parcel's
/// contents from a return's without joining to <see cref="OrderShipment"/> - which is what the per-unit money
/// read does. It is written from the parcel and never edited on its own.
/// </summary>
public partial class OrderShipmentItem
{
    public long ShipmentItemId { get; set; }

    /// <summary>
    /// The parcel these goods travelled in. Deleting a parcel takes its contents with it (the foreign key is a
    /// cascade), because a row that names no parcel would describe goods that are nowhere.
    /// </summary>
    public long ShipmentId { get; set; }

    /// <summary>
    /// The order the parcel belongs to, carried on the row as well so every parcel's contents are read with one
    /// seek and a stray row is findable without its parcel. Always the parcel's own order.
    /// </summary>
    public long OrderId { get; set; }

    /// <summary>
    /// Which leg this row's parcel is (<see cref="OrderShipment.DirectionForward"/> or
    /// <see cref="OrderShipment.DirectionReverse"/>) - the parcel's own direction, copied here so the per-unit
    /// money read can tell which units went OUT and which came back without reading the parcels.
    /// </summary>
    public string Direction { get; set; } = OrderShipment.DirectionForward;

    /// <summary>
    /// The order line's SKU (<c>OrderItems.SKU</c> for this order). The SKU a parcel carries always names a line
    /// of the order it went with - the save refuses anything else, because a parcel only ever carries what the
    /// order was for.
    /// </summary>
    public string Sku { get; set; } = null!;

    /// <summary>
    /// How many units of <see cref="Sku"/> went in this parcel: 1 or more, because a parcel carrying nothing is a
    /// parcel nobody can bill for.
    /// </summary>
    public short Quantity { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual OrderShipment Shipment { get; set; } = null!;
}
