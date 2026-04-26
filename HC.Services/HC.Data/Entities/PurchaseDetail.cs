using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class PurchaseDetail
{
    public long PurchaseDetailId { get; set; }

    public long PurchaseId { get; set; }

    public int ProductId { get; set; }

    public short Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Gst { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual Purchase Purchase { get; set; } = null!;

    public virtual ICollection<Sku> Skus { get; set; } = new List<Sku>();
}
