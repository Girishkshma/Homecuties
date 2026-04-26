using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class Product
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public string ProductTitle { get; set; } = null!;

    public string ProductDescription { get; set; } = null!;

    public bool DisplayOnHomePage { get; set; }

    public short ProductStatusId { get; set; }

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

    public long CreatedBy { get; set; }

    public DateTime CreatedOn { get; set; }

    public long ModifiedBy { get; set; }

    public DateTime ModifiedOn { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<GuestCartItem> GuestCartItems { get; set; } = new List<GuestCartItem>();

    public virtual User ModifiedByNavigation { get; set; } = null!;

    public virtual ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();

    public virtual ICollection<ProductFeature> ProductFeatures { get; set; } = new List<ProductFeature>();

    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    public virtual ProductStatus ProductStatus { get; set; } = null!;

    public virtual ICollection<PurchaseDetail> PurchaseDetails { get; set; } = new List<PurchaseDetail>();

    public virtual ICollection<WishList> WishLists { get; set; } = new List<WishList>();
}
