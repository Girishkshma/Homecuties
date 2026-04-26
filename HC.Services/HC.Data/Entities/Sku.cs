using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class Sku
{
    public string Sku1 { get; set; } = null!;

    public long PurchaseDetailId { get; set; }

    public int InventoryId { get; set; }

    public short SkustatusId { get; set; }

    public virtual Inventory Inventory { get; set; } = null!;

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual PurchaseDetail PurchaseDetail { get; set; } = null!;

    public virtual ICollection<Skuhistory> Skuhistories { get; set; } = new List<Skuhistory>();

    public virtual Skustatus Skustatus { get; set; } = null!;
}
