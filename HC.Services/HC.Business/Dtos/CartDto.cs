namespace HC.Business.Dtos;

public class CartItemDto
{
    public int ProductID { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductTitle { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string Image { get; set; } = "";
}

public class CartCalculationDto
{
    public int Count { get; set; }
    public decimal SalesPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal AddDiscount { get; set; }
    public string GST { get; set; } = "0";
    public decimal GSTCharge { get; set; }
    public decimal SubTotal { get; set; }
    public decimal GrandTotal { get; set; }
}

public class CartResponseDto
{
    public List<CartItemDto> Items { get; set; } = new();
    public CartCalculationDto Calculation { get; set; } = new();
}

public class CustomerDto
{
    public long CustomerID { get; set; }
    public string FirstName { get; set; } = "";
    public string MiddleName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string EmailId { get; set; } = "";
    public string MobileNumber { get; set; } = "";
    public string MobileIsd { get; set; } = "";
    public bool IsGuest { get; set; }
}

public class GuestCustomerDto
{
    public long CustomerID { get; set; }
    public string FirstName { get; set; } = "";
    public string MiddleName { get; set; } = "";
    public string LastName { get; set; } = "";
    public bool IsGuest { get; set; }
}

public class JwtResponseDto
{
    public string Token { get; set; } = "";
    public DateTime ExpiresOn { get; set; }
}

public class JwtValidationDto
{
    public bool Valid { get; set; }
    public string? Data { get; set; }
}

public class ResultDto
{
    public int Result { get; set; }
    public string[] Messages { get; set; } = Array.Empty<string>();
}

public class LoginCustomerResponseDto
{
    public int Result { get; set; }
    public string[] Messages { get; set; } = Array.Empty<string>();
    public CustomerDto? Customer { get; set; }
}
