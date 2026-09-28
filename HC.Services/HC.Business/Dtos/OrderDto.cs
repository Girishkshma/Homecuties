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

    /// <summary>Orders.OrderStatusID: 1 = Pending, 2 = Confirmed, 3 = Shipped, 4 = Delivered, 5 = Cancelled.</summary>
    public short StatusId { get; set; }

    public string Status { get; set; } = "";
    public string PaymentStatus { get; set; } = "";

    /// <summary>True once the payment for the order has been captured.</summary>
    public bool IsPaid { get; set; }

    /// <summary>True while the customer can still cancel the order from 'My Orders'.</summary>
    public bool CanCancel { get; set; }

    /// <summary>Number of units in the order (OrderItems holds one row per physical unit).</summary>
    public int ItemCount { get; set; }

    /// <summary>Where the order is being shipped - shown in the order history.</summary>
    public OrderAddressDto ShippingAddress { get; set; } = new();

    public List<OrderItemDto> Items { get; set; } = new();

    /// <summary>Order placed / payment captured / cancelled - 'My Orders' shows this as a timeline.</summary>
    public List<OrderHistoryDto> History { get; set; } = new();
}

public class OrderAddressDto
{
    public string ContactName { get; set; } = "";
    public string AddressLine1 { get; set; } = "";
    public string AddressLine2 { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Zipcode { get; set; } = "";
    public string MobileNumber { get; set; } = "";
    public string? EmailId { get; set; }
}

public class OrderHistoryDto
{
    public DateTime Date { get; set; }
    public string Status { get; set; } = "";
    public string Comments { get; set; } = "";
}

/// <summary>Body of the 'manage my order' actions (cancel an order / re-check its payment).</summary>
public class OrderActionRequest
{
    public long OrderId { get; set; }
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
