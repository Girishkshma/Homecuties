namespace HC.Business.Dtos;

public class ProductDto
{
    public int ProductID { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductTitle { get; set; } = "";
    public string ProductDescription { get; set; } = "";
    public string PromoImage { get; set; } = "";
    public List<string> ProductImages { get; set; } = new();
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
    /// <summary>Number of sellable SKUs (Available status and not already reserved by an order).</summary>
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
