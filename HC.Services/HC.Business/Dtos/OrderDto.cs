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

    /// <summary>
    /// Address book id of the address the customer picked in checkout. When it is set (and belongs to
    /// the customer) that saved address is used as it stands, instead of storing the flat shipping
    /// fields below once more - which is what used to leave a fresh duplicate address per order.
    /// </summary>
    public long ShippingAddressId { get; set; }

    /// <summary>Label for a newly typed shipping address ("Home", "Office", ...). Optional.</summary>
    public string ShippingAddressTitle { get; set; } = "";

    /// <summary>Second line / landmark of a newly typed shipping address. Optional.</summary>
    public string ShippingAddressLine2 { get; set; } = "";

    /// <summary>Who receives the parcel. Optional - the customer's own name is used when left empty.</summary>
    public string ShippingContactName { get; set; } = "";

    /// <summary>
    /// True (the default) bills the order to the shipping address. Set false to bill it to the
    /// address in <see cref="BillingAddressId"/> or to the billing fields below.
    /// </summary>
    public bool BillingSameAsShipping { get; set; } = true;

    /// <summary>Address book id of the billing address picked in checkout (see <see cref="ShippingAddressId"/>).</summary>
    public long BillingAddressId { get; set; }

    public string BillingContactName { get; set; } = "";
    public string BillingAddressLine1 { get; set; } = "";
    public string BillingAddressLine2 { get; set; } = "";
    public string BillingCity { get; set; } = "";
    public string BillingState { get; set; } = "";
    public string BillingZipCode { get; set; } = "";
    public string BillingPhoneNumber { get; set; } = "";
    public string BillingEmail { get; set; } = "";
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

    /// <summary>
    /// Set only by Order/RetryPayment: the earlier attempt turned out to be paid after all, so the
    /// order was confirmed instead of being given a new payment. <see cref="Messages"/> holds the good
    /// news in that case, which is why the browser must not show it as an error and must not open a
    /// payment window (there is nothing left to pay).
    /// </summary>
    public bool AlreadySettled { get; set; }
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

    /// <summary>
    /// True while the money for this order is owed back to the customer: a paid order that was cancelled, or one
    /// that was returned once its parcel came back, so the refund has been asked for and is waiting for the shop
    /// team to approve it (see HC.Business.RazorpayRefunds and OrderPaymentStatus.IsRefundOwed). 'My Orders' shows
    /// this as 'Refund pending' - the order screen and the admin order screen tell the rest of the story.
    /// </summary>
    public bool RefundPending { get; set; }

    /// <summary>
    /// True while the customer can still cancel the order from 'My Orders': while it is in the shop's
    /// hands (Pending or Confirmed - see OrderStatusFlow.CanCustomerCancel). Cancelling an order that
    /// has already been paid asks for the money back; the shop team approves the refund.
    /// </summary>
    public bool CanCancel { get; set; }

    /// <summary>
    /// True while the customer may ask for a return from 'My Orders' - the order has been delivered, no
    /// return is open on it and the window is still open (see OrderReturnFlow.CanCustomerAsk, which is the
    /// same rule the server applies to the ask itself). 'My Orders' shows the return form on exactly this.
    /// </summary>
    public bool CanReturn { get; set; }

    /// <summary>
    /// True while the customer may take a return request back (nothing has been answered yet, see
    /// OrderReturnStatus.CanWithdraw) - the 'Take request back' button.
    /// </summary>
    public bool CanWithdrawReturn { get; set; }

    /// <summary>
    /// Where the order's return has got to (OrderReturnStatus.*: Requested / Arranged / Received / Closed /
    /// Rejected / Withdrawn), or null when the order has never had one. The latest return counts - a refused
    /// or closed one is still what the customer should read next to the order.
    /// </summary>
    public string? ReturnStatus { get; set; }

    /// <summary>Why the return was asked for, in the asker's own words ('wrong size'). Null when none.</summary>
    public string? ReturnReason { get; set; }

    /// <summary>When the return was asked for. Null when the order has never had one.</summary>
    public DateTime? ReturnRequestedOn { get; set; }

    /// <summary>
    /// Who asked for the return (OrderReturnOrigin.Customer / .Courier). 'My Orders' words the two apart, because a
    /// return the courier reported - a parcel refused at the door, or one that came back undelivered - is not
    /// something the customer asked for. Null when the order has never had a return.
    /// </summary>
    public string? ReturnOrigin { get; set; }

    /// <summary>
    /// The last day the customer may ask for a return - the day the parcel reached them plus
    /// <c>Returns:WindowDays</c> (see OrderReturnFlow.WindowEndsOn). Null when the order was never
    /// delivered, so there is no window at all.
    /// </summary>
    public DateTime? ReturnWindowEndsOn { get; set; }

    /// <summary>True while the order is Pending - 'My Orders' offers 'Pay now' to retry the payment.</summary>
    public bool CanPay { get; set; }

    /// <summary>Number of units in the order (OrderItems holds one row per physical unit).</summary>
    public int ItemCount { get; set; }

    /// <summary>Where the order is being shipped - shown in the order history.</summary>
    public OrderAddressDto ShippingAddress { get; set; } = new();

    /// <summary>
    /// Where the order is billed. It is the shipping address unless the customer asked for a
    /// different one in checkout, so 'My Orders' only shows it when the two differ.
    /// </summary>
    public OrderAddressDto BillingAddress { get; set; } = new();

    /// <summary>
    /// The parcel of this order as it was last written down, or null when the shop has not recorded one
    /// yet. 'My Orders' shows the courier's latest wording (or the shop's own: <c>Status</c>) and the
    /// tracking link from it; the fresh look is the throttled pull the page makes when it opens /
    /// the customer taps 'Track parcel' (see <see cref="HC.Business.IOrderService.RefreshShipmentsAsync"/>),
    /// so opening the history is never itself a call to the courier.
    /// </summary>
    public OrderShipmentDto? Shipment { get; set; }

    /// <summary>
    /// The parcel coming back - the return's own leg - or null when there is none. It is recorded once a return
    /// has been approved (the pickup the shop team booked) or when the courier reports the parcel on its way back,
    /// and 'My Orders' shows it exactly like <see cref="Shipment"/> so the customer can follow what they sent
    /// back. The two are always the two different legs, never one parcel shown twice.
    /// </summary>
    public OrderShipmentDto? ReturnShipment { get; set; }

    public List<OrderItemDto> Items { get; set; } = new();

    /// <summary>Order placed / payment captured / cancelled - 'My Orders' shows this as a timeline.</summary>
    public List<OrderHistoryDto> History { get; set; } = new();
}

public class OrderAddressDto
{
    /// <summary>CustomerAddresses.AddressID - lets the storefront tell shipping and billing apart.</summary>
    public long AddressId { get; set; }

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

/// <summary>
/// Body of 'ask for a return' in 'My Orders' (see OrderService.RequestReturnAsync). The reason is a code
/// from <see cref="HC.Business.OrderReturnReason"/> - what the picker offers - and the customer's own words
/// are optional: they are what makes a return readable to the shop team, while the code is what returns are
/// counted by.
/// </summary>
public class RequestReturnRequest
{
    public long OrderId { get; set; }

    /// <summary>Why they are returning it (OrderReturnReason.*) - checked again on the server.</summary>
    public string ReasonCode { get; set; } = "";

    /// <summary>What the customer typed, when they typed anything. Optional.</summary>
    public string Reason { get; set; } = "";
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
