namespace HC.Business.Dtos;

public class CreateOrderRequest
{
    public long CustomerID { get; set; }
    public bool IsGuest { get; set; }
    public string ShippingAddress { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string ZipCode { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Email { get; set; } = "";
    public string PaymentMethod { get; set; } = "razorpay";
}

public class CreateOrderResponse
{
    public int Result { get; set; }
    public string[] Messages { get; set; } = Array.Empty<string>();
    public long OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public decimal Amount { get; set; }
    public string? RazorpayOrderId { get; set; }
    public string? RazorpayKey { get; set; }
    public string[] RemovedItems { get; set; } = Array.Empty<string>();
    public string[] AdjustedItems { get; set; } = Array.Empty<string>();
}

public class VerifyPaymentRequest
{
    public long OrderId { get; set; }
    public string RazorpayPaymentId { get; set; } = "";
    public string RazorpayOrderId { get; set; } = "";
    public string RazorpaySignature { get; set; } = "";
}

public class OrderListDto
{
    public long OrderId { get; set; }
    public string OrderNumber { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "";
    public string PaymentStatus { get; set; } = "";
    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductTitle { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string Image { get; set; } = "";
}
