namespace HC.Business.Dtos;

public class WishListDto
{
    public long CustomerId { get; set; }
    public int ProductId { get; set; }
    public DateTime AddedOn { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductTitle { get; set; } = "";
    public string ProductDescription { get; set; } = "";
    public string PromoImage { get; set; } = "";
    /// <summary>
    /// What the two discounts do to the goods alone (the unit price), the additional discount coming off what the
    /// first one left - read with the listing's own fields of the same names, so the wish list shows what the
    /// listing beside it shows.
    /// </summary>
    public decimal SalesPrice { get; set; }
    public decimal PostDiscountSalesPrice { get; set; }
    public decimal PostAdditionalDiscountSalesPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal AdditionalDiscountPercent { get; set; }
    public bool IsInStock { get; set; }

    /// <summary>
    /// What one unit costs the customer (see <see cref="ProductPricing.ListingPrice"/>) and the figure a wish list is
    /// read with: a wish list shows the same price the listing beside it shows, so the two are worked out by the one
    /// rule from the product's own 'Pricing &amp; Charges' and 'Taxes' fields. Worked out by <c>WishListService</c>
    /// after the rows are read, because the rule is C# and not SQL.
    /// </summary>
    public decimal ListingPrice { get; set; }

    /// <summary>The same price with both discounts still on it: what the wish list strikes through.</summary>
    public decimal PreDiscountListingPrice { get; set; }

    /// <summary>
    /// The product's own 'Pricing &amp; Charges' fields, carried so the price above can be worked out from the rows
    /// that were read rather than by a second query (the same shapes the product listing carries).
    /// </summary>
    public decimal UnitPrice { get; set; }
    public decimal ProfitMarginPercent { get; set; }
    public decimal PackagingCharge { get; set; }
    public decimal StorageCharge { get; set; }
    public decimal DeliveryCharge { get; set; }

    /// <summary>The product's own 'Taxes' fields, carried for the same reason as the charges above.</summary>
    public decimal CGSTPercent { get; set; }
    public decimal SGSTPercent { get; set; }
    public decimal IGSTPercent { get; set; }
}

public class AddToWishListRequest
{
    public long CustomerId { get; set; }
    public int ProductId { get; set; }
    public bool IsGuest { get; set; }
}

public class RemoveFromWishListRequest
{
    public long CustomerId { get; set; }
    public int ProductId { get; set; }
    public bool IsGuest { get; set; }
}

public class GetWishListRequest
{
    public long CustomerId { get; set; }
    public bool IsGuest { get; set; }
}

public class CheckWishListItemRequest
{
    public long CustomerId { get; set; }
    public int ProductId { get; set; }
    public bool IsGuest { get; set; }
}

public class TransferGuestWishListRequest
{
    public long GuestCustomerId { get; set; }
    public long CustomerId { get; set; }
}
