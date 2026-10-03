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

    public virtual ICollection<OrderPayment> OrderPayments { get; set; } = new List<OrderPayment>();

    /// <summary>
    /// The returns asked for on this order (at most one open at a time - see <see cref="OrderReturn"/>):
    /// a delivered order can be returned once, and a refused parcel raises the ask on its own.
    /// </summary>
    public virtual ICollection<OrderReturn> OrderReturns { get; set; } = new List<OrderReturn>();

    /// <summary>
    /// The parcels of this order, one per leg (see <see cref="OrderShipment"/>): the one that went out,
    /// and the one coming back once a return is arranged.
    /// </summary>
    public virtual ICollection<OrderShipment> OrderShipments { get; set; } = new List<OrderShipment>();

    public virtual OrderStatus OrderStatus { get; set; } = null!;

    public virtual Partner Seller { get; set; } = null!;

    public virtual CustomerAddress ShippingAddress { get; set; } = null!;
}
