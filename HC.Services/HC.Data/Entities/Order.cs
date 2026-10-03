using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class Order
{
    public long OrderId { get; set; }

    public long CustomerId { get; set; }

    public int SellerId { get; set; }

    public DateTime OrderDate { get; set; }

    public long BillingAddressId { get; set; }

    public long ShippingAddressId { get; set; }

    public short OrderStatusId { get; set; }

    public virtual CustomerAddress BillingAddress { get; set; } = null!;

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<OrderHistory> OrderHistories { get; set; } = new List<OrderHistory>();

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    /// <summary>
    /// What each of this order's lines really carried, money-wise: its own part of the gateway's charge for
    /// taking the order's money (and the GST on it), its own part of what the couriers billed for the parcels
    /// that carried it, and the output GST inside what the customer paid for it (see
    /// <see cref="OrderItemMoney"/>). Derived - written by HC.Business.OrderItemMoneyWriter, read by the order
    /// screen's per-unit card and the Finance screen's per-SKU breakdown - so it is never edited by hand.
    /// </summary>
    public virtual ICollection<OrderItemMoney> OrderItemMoney { get; set; } = new List<OrderItemMoney>();

    public virtual ICollection<OrderPayment> OrderPayments { get; set; } = new List<OrderPayment>();

    /// <summary>
    /// The returns asked for on this order (at most one open at a time - see <see cref="OrderReturn"/>):
    /// a delivered order can be returned once, and a refused parcel raises the ask on its own.
    /// </summary>
    public virtual ICollection<OrderReturn> OrderReturns { get; set; } = new List<OrderReturn>();

    /// <summary>
    /// The parcels of this order (see <see cref="OrderShipment"/>): the ones that went out - an order can go out
    /// in more than one - and the one coming back once a return is arranged. The first parcel out is what 'the
    /// parcel' means wherever a single one is read.
    /// </summary>
    public virtual ICollection<OrderShipment> OrderShipments { get; set; } = new List<OrderShipment>();

    /// <summary>
    /// What went in those parcels: one row per SKU of each parcel (see <see cref="OrderShipmentItem"/>), which is
    /// what a parcel's own courier bill is charged to.
    /// </summary>
    public virtual ICollection<OrderShipmentItem> OrderShipmentItems { get; set; } = new List<OrderShipmentItem>();

    public virtual OrderStatus OrderStatus { get; set; } = null!;

    public virtual Partner Seller { get; set; } = null!;

    public virtual CustomerAddress ShippingAddress { get; set; } = null!;
}
