using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class OrderItem
{
    public long OrderId { get; set; }

    public string Sku { get; set; } = null!;

    public string ProductName { get; set; } = null!;

    public string ProductTitle { get; set; } = null!;

    public string ProductDescription { get; set; } = null!;

    public short ProductCategoryId { get; set; }

    public decimal UnitPrice { get; set; }

    public string? Hsncode { get; set; }

    public decimal PackagingCharge { get; set; }

    public decimal StorageCharge { get; set; }

    public decimal DiscountPercent { get; set; }

    public decimal AdditionalDiscountPercent { get; set; }

    public decimal DeliveryCharge { get; set; }

    public decimal ProfitMarginPercent { get; set; }

    public decimal Cgstpercent { get; set; }

    public decimal Sgstpercent { get; set; }

    public decimal Igstpercent { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual Category ProductCategory { get; set; } = null!;

    public virtual Sku SkuNavigation { get; set; } = null!;
}
