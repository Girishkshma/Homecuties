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

    public virtual OrderStatus OrderStatus { get; set; } = null!;

    public virtual Partner Seller { get; set; } = null!;

    public virtual CustomerAddress ShippingAddress { get; set; } = null!;
}
