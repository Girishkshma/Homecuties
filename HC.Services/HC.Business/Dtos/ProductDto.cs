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
