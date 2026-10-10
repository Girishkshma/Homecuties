namespace HC.Business.Dtos;

public class ProductDto
{
    public int ProductID { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductTitle { get; set; } = "";
    public string ProductDescription { get; set; } = "";
    public string PromoImage { get; set; } = "";

    /// <summary>
    /// The product's image rows as they are stored (see <see cref="ProductImageRefDto"/>): which size each row holds
    /// and the file it names. A screen draws <see cref="Pictures"/> instead - a row is written down whether or not its
    /// file was ever produced, and the shop's older products have rows for sizes their files were never made at.
    /// </summary>
    public List<ProductImageRefDto> ProductImages { get; set; } = new();

    /// <summary>
    /// The product's photos as a screen draws them, in the order they were uploaded: for each one the file a big
    /// frame takes and the file the strip of thumbnails takes, resolved against the shop's image folder (see
    /// <see cref="ProductGallery"/>). The two are the same file when the smaller size was never produced, and a photo
    /// with no file at all is left out - which is what keeps a page from drawing a broken picture.
    /// </summary>
    public List<ProductGallery.Picture> Pictures { get; set; } = new();
    /// <summary>
    /// The pricing fields of the product itself, carried on the listing (see <see cref="ProductPricing"/>): what the
    /// price below is made up of, so a screen can show the saving or the tax without a second read.
    /// </summary>
    public decimal UnitPrice { get; set; }
    public decimal ProfitMarginPercent { get; set; }
    public decimal PackagingCharge { get; set; }
    public decimal StorageCharge { get; set; }
    public decimal DeliveryCharge { get; set; }

    /// <summary>
    /// What one unit costs the customer: the listing price, everything the product form's 'Pricing &amp; Charges' and
    /// 'Taxes' sections say included (see <see cref="ProductPricing.ListingPrice"/>). This is the figure a listing
    /// shows, the cart adds up and the checkout charges - worked out by <c>ProductService</c> after the row is read,
    /// because the rule lives in C# and not in SQL.
    /// </summary>
    public decimal ListingPrice { get; set; }

    /// <summary>The same price with both discounts still on it: what a listing strikes through.</summary>
    public decimal PreDiscountListingPrice { get; set; }

    /// <summary>
    /// The same price with only the 'Discount %' read: what one unit costs the customer once that discount is off
    /// it, the additional discount still to come. It is the middle figure of the three a product page walks a shopper
    /// down - <see cref="PreDiscountListingPrice"/>, this one, and <see cref="ListingPrice"/> - so the price coming
    /// down from the marked figure to what they pay can be read a step at a time.
    /// </summary>
    public decimal PostDiscountListingPrice { get; set; }

    /// <summary>The shop's declared margin inside <see cref="ListingPrice"/> (OrderMoney.DeclaredProfit).</summary>
    public decimal MarginAmount { get; set; }

    /// <summary>The packaging, storage and delivery charges inside <see cref="ListingPrice"/>, added up.</summary>
    public decimal ChargesAmount { get; set; }

    /// <summary>The value the GST inside <see cref="ListingPrice"/> was charged on.</summary>
    public decimal TaxableValue { get; set; }

    /// <summary>The rate that tax was charged at: CGST + SGST, or IGST where that is the only one set.</summary>
    public decimal GstRatePercent { get; set; }

    /// <summary>The GST inside <see cref="ListingPrice"/>.</summary>
    public decimal GstAmount { get; set; }

    /// <summary>
    /// What the two discounts do to the goods ALONE - the unit price, and what each of them leaves of it, the
    /// additional discount coming off what the first one left. Kept beside the figures above for the screens that
    /// speak about the discount itself, which is a different question from what a customer pays: that price has the
    /// shop's margin, its own handling charges and the tax in it, and is <see cref="ListingPrice"/>.
    /// </summary>
    public decimal SalesPrice { get; set; }
    public decimal PreDiscountSalesPrice { get; set; }
    public decimal PostDiscountSalesPrice { get; set; }
    public decimal PostAdditionalDiscountSalesPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal AdditionalDiscountPercent { get; set; }
    public decimal CGSTPercent { get; set; }
    public decimal SGSTPercent { get; set; }
    public decimal IGSTPercent { get; set; }
    public bool IsInStock { get; set; }
    /// <summary>Number of sellable units: "Available" SKUs that no live (non-cancelled) order holds.</summary>
    public int AvailableQty { get; set; }
    public List<ProductFeatureDto> Features { get; set; } = new();
    public List<CategoryDto> Categories { get; set; } = new();
}

public class ProductFeatureDto
{
    public long FeatureID { get; set; }
    public string Feature { get; set; } = "";
    public bool IsActive { get; set; }
}

public class CategoryDto
{
    public short CategoryID { get; set; }
    public string CategoryName { get; set; } = "";
    public short? ParentCategoryID { get; set; }
}

/// <summary>Live counts shown in the storefront hero section.</summary>
public class HomeStatsDto
{
    /// <summary>Products that are not Suspended.</summary>
    public int ProductCount { get; set; }
    /// <summary>Categories that have at least one active product.</summary>
    public int CategoryCount { get; set; }
    /// <summary>Registered customers.</summary>
    public int CustomerCount { get; set; }
}
