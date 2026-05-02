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
    public decimal SalesPrice { get; set; }
    public decimal PostDiscountSalesPrice { get; set; }
    public decimal PostAdditionalDiscountSalesPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal AdditionalDiscountPercent { get; set; }
}

public class AddToWishListRequest
{
    public long CustomerId { get; set; }
    public int ProductId { get; set; }
}

public class RemoveFromWishListRequest
{
    public long CustomerId { get; set; }
    public int ProductId { get; set; }
}

public class GetWishListRequest
{
    public long CustomerId { get; set; }
}

public class CheckWishListItemRequest
{
    public long CustomerId { get; set; }
    public int ProductId { get; set; }
}
