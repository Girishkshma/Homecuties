using System.Text.Json.Serialization;

namespace HC.Business.Dtos;

// Login
public class AdminLoginRequest
{
    public string LoginId { get; set; } = "";
    public string Password { get; set; } = "";
}

public class AdminLoginResponse
{
    [JsonPropertyName("result")]
    public int Result { get; set; }
    [JsonPropertyName("messages")]
    public string[] Messages { get; set; } = Array.Empty<string>();
    [JsonPropertyName("user")]
    public AdminUserDto? User { get; set; }
    [JsonPropertyName("token")]
    public string? Token { get; set; }
    [JsonPropertyName("expiresOn")]
    public DateTime? ExpiresOn { get; set; }
}

public class AdminUserDto
{
    [JsonPropertyName("userId")]
    public long UserId { get; set; }
    [JsonPropertyName("loginId")]
    public string LoginId { get; set; } = "";
    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = "";
    [JsonPropertyName("middleName")]
    public string? MiddleName { get; set; }
    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }
    [JsonPropertyName("emailId")]
    public string? EmailId { get; set; }
    [JsonPropertyName("mobileNumber")]
    public string? MobileNumber { get; set; }
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
    [JsonPropertyName("roles")]
    public List<AdminRoleDto> Roles { get; set; } = new();
}

public class AdminRoleDto
{
    [JsonPropertyName("roleId")]
    public short RoleId { get; set; }
    [JsonPropertyName("roleName")]
    public string RoleName { get; set; } = "";
    [JsonPropertyName("roleDescription")]
    public string? RoleDescription { get; set; }
}

// JWT
public class AdminJwtRequest
{
    public long UserId { get; set; }
    public string LoginId { get; set; } = "";
    public string IPAddress { get; set; } = "";
}

public class AdminValidateJwtRequest
{
    public string JWT { get; set; } = "";
    public string IPAddress { get; set; } = "";
}

// Menu & Activity
public class AdminMenuDto
{
    [JsonPropertyName("menuId")]
    public short MenuId { get; set; }
    [JsonPropertyName("menuTitle")]
    public string MenuTitle { get; set; } = "";
    [JsonPropertyName("menuDescription")]
    public string? MenuDescription { get; set; }
    [JsonPropertyName("menuUrl")]
    public string MenuUrl { get; set; } = "";
    [JsonPropertyName("parentMenuId")]
    public short? ParentMenuId { get; set; }
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
    [JsonPropertyName("children")]
    public List<AdminMenuDto> Children { get; set; } = new();
    [JsonPropertyName("activities")]
    public List<AdminActivityDto> Activities { get; set; } = new();
}

public class AdminActivityDto
{
    [JsonPropertyName("activityId")]
    public short ActivityId { get; set; }
    [JsonPropertyName("activityTitle")]
    public string ActivityTitle { get; set; } = "";
    [JsonPropertyName("menuId")]
    public short MenuId { get; set; }
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
}

// Dashboard Stats
public class DashboardStatsDto
{
    [JsonPropertyName("totalProducts")]
    public int TotalProducts { get; set; }
    [JsonPropertyName("totalOrders")]
    public int TotalOrders { get; set; }
    [JsonPropertyName("totalCustomers")]
    public int TotalCustomers { get; set; }
    [JsonPropertyName("totalPartners")]
    public int TotalPartners { get; set; }
    [JsonPropertyName("totalVendors")]
    public int TotalVendors { get; set; }
    [JsonPropertyName("pendingOrders")]
    public int PendingOrders { get; set; }
    [JsonPropertyName("todayRevenue")]
    public decimal TodayRevenue { get; set; }
    [JsonPropertyName("monthlyRevenue")]
    public decimal MonthlyRevenue { get; set; }

    /// <summary>Cancelled orders, counted on their own tile instead of in <see cref="TotalOrders"/>.</summary>
    [JsonPropertyName("cancelledOrders")]
    public int CancelledOrders { get; set; }

    // The operational tiles below are the 'what needs doing now' half of the dashboard: they follow the
    // order lifecycle (OrderStatusFlow) and the parcel (ShipmentStatusFlow), the same two maps the order
    // screens read, so the dashboard can never describe an order differently than its own screen does.

    /// <summary>
    /// Paid orders (Confirmed) the shop has not dispatched yet - the packing/booking backlog. An order
    /// in this status has been paid but is still with us, whether or not a parcel has been booked for it.
    /// </summary>
    [JsonPropertyName("ordersToDispatch")]
    public int OrdersToDispatch { get; set; }

    /// <summary>
    /// Parcels on the way: the courier's own status reads InTransit or OutForDelivery
    /// (see HC.Business.ShipmentStatusFlow). A parcel booked but not collected, delivered, coming back
    /// or called off is not counted here.
    /// </summary>
    [JsonPropertyName("shipmentsInProgress")]
    public int ShipmentsInProgress { get; set; }

    /// <summary>Parcels out for delivery today - the live parcels the customer expects this very day.</summary>
    [JsonPropertyName("outForDelivery")]
    public int OutForDelivery { get; set; }

    /// <summary>Orders handed to the customer (Delivered) - the end of the order lifecycle.</summary>
    [JsonPropertyName("deliveredOrders")]
    public int DeliveredOrders { get; set; }

    /// <summary>
    /// Parcels that need the shop team's eye: a failed delivery the courier will retry (Undelivered), one the
    /// customer refused at the door (Refused) or one already returning to the shop (RTO). A courier status never
    /// cancels an order - that stays a human decision - so these are flagged here rather than acted on. A refusal
    /// and an RTO also leave a return waiting for the shop team's answer (see
    /// HC.Business.ShipmentStatusFlow.ReturnReasonFor).
    /// </summary>
    [JsonPropertyName("shipmentsNeedingAttention")]
    public int ShipmentsNeedingAttention { get; set; }

    /// <summary>
    /// Orders whose money the shop still holds and owes back: a refund that has been asked for
    /// (RefundRequested), one Razorpay refused (RefundFailed), or the capture of an order that gave its units
    /// back - cancelled, or returned once its parcel came back - whose refund was never asked for. The same rule
    /// the order list flags as 'Refund due' (see OrderPaymentStatus.IsRefundOwed).
    /// </summary>
    [JsonPropertyName("refundsDue")]
    public int RefundsDue { get; set; }

    // The returns side of the same 'what needs doing now' work, in the vocabulary the Return card uses
    // (OrderReturnStatus). Each of the three is a different job for the shop team: answer an ask, watch for a
    // parcel coming back, or look over and close one that is back.

    /// <summary>
    /// Returns waiting for the shop team's answer (OrderReturnStatus.Requested) - a customer's ask, or a
    /// refusal/return-to-origin the courier reported. Nothing moves until they answer it.
    /// </summary>
    [JsonPropertyName("returnsAwaitingDecision")]
    public int ReturnsAwaitingDecision { get; set; }

    /// <summary>Returns whose parcel is on its way back (OrderReturnStatus.Arranged), the pickup booked.</summary>
    [JsonPropertyName("returnsComingBack")]
    public int ReturnsComingBack { get; set; }

    /// <summary>
    /// Returns whose parcel is back with the shop (OrderReturnStatus.Received) - each one is an inspection and a
    /// close away from being done, with the customer's money waiting on it.
    /// </summary>
    [JsonPropertyName("returnsReceived")]
    public int ReturnsReceived { get; set; }

    /// <summary>
    /// Orders whose parcel came back and whose return was closed (Orders.OrderStatusID = 6): the sale reversed,
    /// the units back on the shelf and the money owed back. Counted on its own tile, like
    /// <see cref="CancelledOrders"/> - neither is an order that is still going anywhere.
    /// </summary>
    [JsonPropertyName("returnedOrders")]
    public int ReturnedOrders { get; set; }
}

// Products
public class AdminProductListDto
{
    [JsonPropertyName("productId")]
    public int ProductId { get; set; }
    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = "";
    [JsonPropertyName("productTitle")]
    public string ProductTitle { get; set; } = "";
    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
    [JsonPropertyName("displayOnHomePage")]
    public bool DisplayOnHomePage { get; set; }
    [JsonPropertyName("createdOn")]
    public DateTime CreatedOn { get; set; }
    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; set; } = "";
}

public class AdminProductDetailDto
{
    [JsonPropertyName("productId")]
    public int ProductId { get; set; }
    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = "";
    [JsonPropertyName("productTitle")]
    public string ProductTitle { get; set; } = "";
    [JsonPropertyName("productDescription")]
    public string ProductDescription { get; set; } = "";
    [JsonPropertyName("displayOnHomePage")]
    public bool DisplayOnHomePage { get; set; }
    [JsonPropertyName("productStatusId")]
    public short ProductStatusId { get; set; }
    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }
    [JsonPropertyName("hsncode")]
    public string? Hsncode { get; set; }
    [JsonPropertyName("packagingCharge")]
    public decimal PackagingCharge { get; set; }
    [JsonPropertyName("storageCharge")]
    public decimal StorageCharge { get; set; }
    [JsonPropertyName("discountPercent")]
    public decimal DiscountPercent { get; set; }
    [JsonPropertyName("additionalDiscountPercent")]
    public decimal AdditionalDiscountPercent { get; set; }
    [JsonPropertyName("deliveryCharge")]
    public decimal DeliveryCharge { get; set; }
    [JsonPropertyName("profitMarginPercent")]
    public decimal ProfitMarginPercent { get; set; }
    [JsonPropertyName("cgstpercent")]
    public decimal Cgstpercent { get; set; }
    [JsonPropertyName("sgstpercent")]
    public decimal Sgstpercent { get; set; }
    [JsonPropertyName("igstpercent")]
    public decimal Igstpercent { get; set; }
    [JsonPropertyName("categoryIds")]
    public List<short> CategoryIds { get; set; } = new();
    [JsonPropertyName("features")]
    public List<AdminProductFeatureDto> Features { get; set; } = new();
    [JsonPropertyName("images")]
    public List<AdminProductImageDto> Images { get; set; } = new();
}

public class AdminProductFeatureDto
{
    [JsonPropertyName("productFeatureId")]
    public long? ProductFeatureId { get; set; }
    [JsonPropertyName("feature")]
    public string Feature { get; set; } = "";
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
}

public class AdminProductImageDto
{
    [JsonPropertyName("productImageId")]
    public long? ProductImageId { get; set; }
    [JsonPropertyName("imageUrl")]
    public string ImageUrl { get; set; } = "";
    [JsonPropertyName("imageTypeId")]
    public short ImageTypeId { get; set; }
    [JsonPropertyName("imageIndex")]
    public int ImageIndex { get; set; }
    [JsonPropertyName("isPromoImage")]
    public bool IsPromoImage { get; set; }
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
}

public class CreateProductRequest
{
    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = "";
    [JsonPropertyName("productTitle")]
    public string ProductTitle { get; set; } = "";
    [JsonPropertyName("productDescription")]
    public string ProductDescription { get; set; } = "";
    [JsonPropertyName("displayOnHomePage")]
    public bool DisplayOnHomePage { get; set; }
    [JsonPropertyName("productStatusId")]
    public short ProductStatusId { get; set; }
    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }
    [JsonPropertyName("hsncode")]
    public string? Hsncode { get; set; }
    [JsonPropertyName("packagingCharge")]
    public decimal PackagingCharge { get; set; }
    [JsonPropertyName("storageCharge")]
    public decimal StorageCharge { get; set; }
    [JsonPropertyName("discountPercent")]
    public decimal DiscountPercent { get; set; }
    [JsonPropertyName("additionalDiscountPercent")]
    public decimal AdditionalDiscountPercent { get; set; }
    [JsonPropertyName("deliveryCharge")]
    public decimal DeliveryCharge { get; set; }
    [JsonPropertyName("profitMarginPercent")]
    public decimal ProfitMarginPercent { get; set; }
    [JsonPropertyName("cgstpercent")]
    public decimal Cgstpercent { get; set; }
    [JsonPropertyName("sgstpercent")]
    public decimal Sgstpercent { get; set; }
    [JsonPropertyName("igstpercent")]
    public decimal Igstpercent { get; set; }
    [JsonPropertyName("categoryIds")]
    public List<short> CategoryIds { get; set; } = new();
    [JsonPropertyName("features")]
    public List<AdminProductFeatureDto> Features { get; set; } = new();
    [JsonPropertyName("images")]
    public List<AdminProductImageDto> Images { get; set; } = new();
}

public class ProductStatusOptionDto
{
    [JsonPropertyName("productStatusId")]
    public short ProductStatusId { get; set; }
    [JsonPropertyName("productStatusName")]
    public string ProductStatusName { get; set; } = "";
}

public class ImageTypeOptionDto
{
    [JsonPropertyName("imageTypeId")]
    public short ImageTypeId { get; set; }
    [JsonPropertyName("imageTypeName")]
    public string ImageTypeName { get; set; } = "";
    [JsonPropertyName("shortCode")]
    public string ShortCode { get; set; } = "";
}

public class ProductFormOptionsDto
{
    [JsonPropertyName("statuses")]
    public List<ProductStatusOptionDto> Statuses { get; set; } = new();
    [JsonPropertyName("imageTypes")]
    public List<ImageTypeOptionDto> ImageTypes { get; set; } = new();
}

// Orders
public class AdminOrderListDto
{
    [JsonPropertyName("orderId")]
    public long OrderId { get; set; }
    [JsonPropertyName("orderNumber")]
    public string? OrderNumber { get; set; }
    [JsonPropertyName("orderDate")]
    public DateTime OrderDate { get; set; }
    [JsonPropertyName("customerName")]
    public string CustomerName { get; set; } = "";

    /// <summary>
    /// Customers.CustomerID of the customer the order belongs to - the id the shop team quotes when
    /// a customer calls about an order, and the only way to tell two customers with the same name
    /// apart.
    /// </summary>
    [JsonPropertyName("customerId")]
    public long CustomerId { get; set; }

    /// <summary>Customers.EmailID - the customer's email, shown next to the id in the order list.</summary>
    [JsonPropertyName("customerEmail")]
    public string CustomerEmail { get; set; } = "";

    [JsonPropertyName("statusId")]
    public short StatusId { get; set; }
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
    [JsonPropertyName("isPaid")]
    public bool IsPaid { get; set; }
    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }
    [JsonPropertyName("itemCount")]
    public int ItemCount { get; set; }

    /// <summary>
    /// True while the order's money is owed back: a refund that has been asked for and is waiting for the shop
    /// team's approval (see HC.Business.RazorpayRefunds), one Razorpay refused, or the capture of an order that
    /// gave its units back - cancelled, or returned once its parcel came back - whose refund was never asked for.
    /// These are the orders the list flags as 'Refund due'.
    /// </summary>
    [JsonPropertyName("refundPending")]
    public bool RefundPending { get; set; }

    /// <summary>
    /// Where the order's return has got to (OrderReturnStatus.*), or null when the order never had one. The
    /// newest return counts. The list flags a live one, so 'what is waiting for an answer' and 'what is on
    /// its way back' can be seen without opening every order.
    /// </summary>
    [JsonPropertyName("returnStatus")]
    public string? ReturnStatus { get; set; }

    /// <summary>True while the order's return is live - waiting for an answer, or with its parcel on the way back.</summary>
    [JsonPropertyName("returnPending")]
    public bool ReturnPending { get; set; }
}

public class AdminOrderDetailDto
{
    [JsonPropertyName("orderId")]
    public long OrderId { get; set; }
    [JsonPropertyName("orderNumber")]
    public string? OrderNumber { get; set; }
    [JsonPropertyName("orderDate")]
    public DateTime OrderDate { get; set; }
    [JsonPropertyName("customerName")]
    public string CustomerName { get; set; } = "";

    /// <summary>Customers.CustomerID, so the order is tied to one customer and not just a name.</summary>
    [JsonPropertyName("customerId")]
    public long CustomerId { get; set; }

    [JsonPropertyName("customerEmail")]
    public string CustomerEmail { get; set; } = "";
    [JsonPropertyName("customerMobile")]
    public string CustomerMobile { get; set; } = "";
    [JsonPropertyName("statusId")]
    public short StatusId { get; set; }
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
    [JsonPropertyName("isPaid")]
    public bool IsPaid { get; set; }

    /// <summary>What the checkout charged for the order (the sum of the unit prices).</summary>
    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("sellerName")]
    public string SellerName { get; set; } = "";
    [JsonPropertyName("billingAddress")]
    public AdminAddressDto BillingAddress { get; set; } = new();
    [JsonPropertyName("shippingAddress")]
    public AdminAddressDto ShippingAddress { get; set; } = new();
    [JsonPropertyName("items")]
    public List<AdminOrderItemDto> Items { get; set; } = new();
    [JsonPropertyName("history")]
    public List<AdminOrderHistoryDto> History { get; set; } = new();

    /// <summary>
    /// The statuses this order may be moved to next (see HC.Business.OrderStatusFlow).
    /// The admin screen only offers these steps, so an impossible jump cannot be picked at all.
    /// </summary>
    [JsonPropertyName("availableStatuses")]
    public List<AdminOrderStatusDto> AvailableStatuses { get; set; } = new();

    /// <summary>
    /// The state of the money when the order was cancelled after payment. <c>RefundPending</c> is true
    /// while the refund has been asked for (by the customer's cancellation) and not yet approved -
    /// that is when the order screen offers 'Approve refund' (the app sends it to Razorpay) and
    /// 'Mark refunded' (the shop team refunded it in the Razorpay dashboard). The payment itself is
    /// the row the refund is sent against: <c>RazorpayPaymentId</c> is null for the orders that have
    /// no captured payment on record, which are the ones that must be refunded by hand.
    /// </summary>
    [JsonPropertyName("refundPending")]
    public bool RefundPending { get; set; }

    /// <summary>When the refund was asked for (the customer cancelling the paid order).</summary>
    [JsonPropertyName("refundRequestedOn")]
    public DateTime? RefundRequestedOn { get; set; }

    /// <summary>Why the refund was asked for, as written by the cancellation.</summary>
    [JsonPropertyName("refundRequestedComment")]
    public string? RefundRequestedComment { get; set; }

    /// <summary>The amount that will be given back: what the gateway took, else the order total.</summary>
    [JsonPropertyName("refundAmount")]
    public decimal? RefundAmount { get; set; }

    /// <summary>Razorpay's refund id (RazorpayRefunds), empty for a refund made by hand.</summary>
    [JsonPropertyName("refundId")]
    public string? RefundId { get; set; }

    /// <summary>Razorpay's refund status (processed / pending / failed) or 'manual'.</summary>
    [JsonPropertyName("refundStatus")]
    public string? RefundStatus { get; set; }

    [JsonPropertyName("refundedOn")]
    public DateTime? RefundedOn { get; set; }

    /// <summary>
    /// Why Razorpay refused the refund, so the team can retry with the reason in front of them - or the
    /// note the team left when they recorded a refund they made by hand.
    /// </summary>
    [JsonPropertyName("refundFailureReason")]
    public string? RefundFailureReason { get; set; }

    /// <summary>
    /// The captured Razorpay payment a refund is sent against, or null when the order has no captured
    /// payment on record (an order paid before payments were recorded here) - such a refund has to be
    /// made in the Razorpay dashboard and then marked refunded here.
    /// </summary>
    [JsonPropertyName("razorpayPaymentId")]
    public string? RazorpayPaymentId { get; set; }

    /// <summary>
    /// The parcel of this order as it stands: the AWB, the courier, the tracking link, and the courier's
    /// latest wording next to the shop's own status derived from it (see HC.Business.ShipmentStatusFlow).
    /// Null while the shop has not recorded one yet - which is exactly when the Shipment card offers to
    /// record it.
    ///
    /// This is what was written down the last time the parcel was looked at, so opening an order never
    /// waits for a courier and never fails because one is unreachable. The fresh look is the Shipment
    /// card's 'Track now' (POST api/admin/orders/{id}/track-shipment), which asks and writes down the
    /// answer - and is also what can move the order on, never this field on its own.
    /// </summary>
    [JsonPropertyName("shipment")]
    public OrderShipmentDto? Shipment { get; set; }

    /// <summary>
    /// The return of this order, or null when it has never had one (see <see cref="AdminOrderReturnDto"/>).
    /// The Return card is built from this: the ask, the shop team's answer, the pickup once one is booked and
    /// - later - the parcel being back and the return being closed.
    /// </summary>
    [JsonPropertyName("return")]
    public AdminOrderReturnDto? Return { get; set; }
}

public class AdminAddressDto
{
    [JsonPropertyName("addressTitle")]
    public string AddressTitle { get; set; } = "";
    [JsonPropertyName("contactName")]
    public string ContactName { get; set; } = "";
    [JsonPropertyName("addressLine1")]
    public string AddressLine1 { get; set; } = "";
    [JsonPropertyName("addressLine2")]
    public string? AddressLine2 { get; set; }
    [JsonPropertyName("city")]
    public string City { get; set; } = "";
    [JsonPropertyName("state")]
    public string State { get; set; } = "";
    [JsonPropertyName("zipcode")]
    public string Zipcode { get; set; } = "";
    [JsonPropertyName("mobileNumber")]
    public string MobileNumber { get; set; } = "";
}

public class AdminOrderItemDto
{
    [JsonPropertyName("sku")]
    public string Sku { get; set; } = "";
    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = "";
    [JsonPropertyName("productTitle")]
    public string ProductTitle { get; set; } = "";
    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }
    [JsonPropertyName("discountPercent")]
    public decimal DiscountPercent { get; set; }
    [JsonPropertyName("additionalDiscountPercent")]
    public decimal AdditionalDiscountPercent { get; set; }
    [JsonPropertyName("deliveryCharge")]
    public decimal DeliveryCharge { get; set; }
    [JsonPropertyName("packagingCharge")]
    public decimal PackagingCharge { get; set; }
    [JsonPropertyName("storageCharge")]
    public decimal StorageCharge { get; set; }
    [JsonPropertyName("profitMarginPercent")]
    public decimal ProfitMarginPercent { get; set; }
    [JsonPropertyName("cgstpercent")]
    public decimal Cgstpercent { get; set; }
    [JsonPropertyName("sgstpercent")]
    public decimal Sgstpercent { get; set; }
    [JsonPropertyName("igstpercent")]
    public decimal Igstpercent { get; set; }
}

public class AdminOrderHistoryDto
{
    [JsonPropertyName("historyDate")]
    public DateTime HistoryDate { get; set; }

    /// <summary>Orders.OrderStatusID this step recorded (OrderHistory keeps its own status id).</summary>
    [JsonPropertyName("statusId")]
    public short StatusId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
    [JsonPropertyName("comments")]
    public string Comments { get; set; } = "";
}

/// <summary>One step of the order lifecycle (Orders.OrderStatusID + its name).</summary>
public class AdminOrderStatusDto
{
    [JsonPropertyName("statusId")]
    public short StatusId { get; set; }
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
}

/// <summary>
/// Body of "move this order to another status" in the admin order screen.
///
/// Moving an order to Shipped asks about the parcel in the same move, because that is the moment the
/// parcel becomes real: the shop team names the provider it was booked with (<see cref="Provider"/>) and
/// types the consignment number that provider gave it (<see cref="AwbNumber"/>), and the two are
/// recorded together with the move (see AdminDashboardService.UpdateOrderStatusAsync). None of it is
/// required, though - an order that went out without a courier (handed over in person, or given to a
/// delivery service this shop has no integration with) is simply dispatched, with no parcel recorded and
/// nothing to track.
///
/// A parcel is recorded when there is one to record, and the provider decides what that means: a
/// consignment number the team typed, or - against a provider that gives the parcel a reference of its
/// own (the shop's own delivery service, <see cref="AwbNumber"/> blank) - the reference the system mints.
/// When there is a parcel, every later courier call is made through the provider named here: the tracking
/// service reads it back off the parcel (OrderShipments.Provider) and asks that adapter's own API, never a
/// hard-coded one. The other steps need none of these fields.
/// </summary>
public class AdminOrderStatusUpdateRequest
{
    [JsonPropertyName("statusId")]
    public short StatusId { get; set; }

    /// <summary>
    /// Free text kept in OrderHistory (required for a cancellation - the customer sees it in
    /// 'My Orders').
    /// </summary>
    [JsonPropertyName("comments")]
    public string? Comments { get; set; }

    /// <summary>
    /// Which shipping provider the parcel was booked with (<c>ShipmentProviderInfo.Name</c>, e.g.
    /// 'Shiprocket'), used when the order is moved to Shipped with a parcel. Blank means the provider the
    /// shop defaults to ('Shipping:DefaultProvider'), which is what a shop booking with a single aggregator
    /// sends; a name this shop is not set up with is refused. The name also decides whether a blank
    /// <see cref="AwbNumber"/> is a parcel this side mints a reference for.
    /// </summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    /// <summary>
    /// The consignment number (AWB) the provider gave this parcel, sent when the order is moved to
    /// Shipped. It is what every later courier lookup is made with, so it is what makes the move a parcel
    /// at all: blank means either that the order went out without a parcel (no consignment number and a
    /// provider that gives none), or that the provider named gives the parcel a reference of its own - the
    /// shop's own delivery service - in which case the system mints one (see
    /// IShipmentTrackingService.RecordsParcel).
    /// </summary>
    [JsonPropertyName("awbNumber")]
    public string? AwbNumber { get; set; }

    /// <summary>The courier the provider handed the parcel to, when the team knows it (optional).</summary>
    [JsonPropertyName("courierName")]
    public string? CourierName { get; set; }

    /// <summary>
    /// What the courier billed the shop for this parcel (the provider's own freight charge), when the
    /// team has it - recorded with the parcel for the books, and never shown to the customer. Optional, and
    /// only kept when the move records a parcel (see IShipmentTrackingService.RecordsParcel), because a
    /// charge belongs to the parcel it was billed for: blank leaves whatever the parcel already holds, so
    /// dispatching can never wipe a figure the Shipment card recorded earlier.
    /// </summary>
    [JsonPropertyName("freightCharge")]
    public decimal? FreightCharge { get; set; }
}

/// <summary>
/// Body of 'record that this refund was made by hand' in the admin order screen. Used when the refund
/// went out in the Razorpay dashboard instead (an order with no captured payment on record, or one
/// Razorpay refused): the payment row is marked Refunded, so the order stops being flagged as owing
/// money. Approving a refund in the app needs no body.
/// </summary>
public class AdminOrderRefundRequest
{
    /// <summary>
    /// Optional note about the refund that was made - kept on the payment row and in OrderHistory so
    /// the order says how the money went back.
    /// </summary>
    [JsonPropertyName("comment")]
    public string? Comment { get; set; }
}

/// <summary>
/// The return of an order, as the admin Return card shows it: what was asked for and why, where the return
/// has got to, what the shop team answered, and the parcel coming back once a pickup has been booked (see
/// HC.Business.OrderReturnFlow for the lifecycle and CreateOrderReturnsTable.sql for the record itself).
///
/// An ask can come from the customer ('My Orders', on a delivered order) or from the courier's own tracking
/// report (a refusal, or a parcel that could not be delivered) - <see cref="Origin"/> and
/// <see cref="OriginLabel"/> say which, because an ask nobody made in words has to read differently on the
/// screen. Nothing here carries money: the refund lives on the payment (<c>RefundPending</c> and friends on
/// the order detail), so it can never be described in two places.
/// </summary>
public class AdminOrderReturnDto
{
    [JsonPropertyName("returnId")]
    public long ReturnId { get; set; }

    /// <summary>OrderReturnOrigin.*: 'Customer' or 'Courier'.</summary>
    [JsonPropertyName("origin")]
    public string Origin { get; set; } = "";

    /// <summary>What the screen says about the origin ('Requested by the customer' / 'Reported by the courier').</summary>
    [JsonPropertyName("originLabel")]
    public string OriginLabel { get; set; } = "";

    /// <summary>OrderReturnStatus.*: Requested / Arranged / Received / Closed / Rejected / Withdrawn.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    /// <summary>OrderReturnReason.* - the coded reason returns are counted by.</summary>
    [JsonPropertyName("reasonCode")]
    public string ReasonCode { get; set; } = "";

    /// <summary>The wording for <see cref="ReasonCode"/> - the screen never invents its own name for a reason.</summary>
    [JsonPropertyName("reasonLabel")]
    public string ReasonLabel { get; set; } = "";

    /// <summary>Why, in the asker's own words (the courier's wording for a courier-raised ask).</summary>
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";

    [JsonPropertyName("requestedOn")]
    public DateTime RequestedOn { get; set; }

    /// <summary>The customer's e-mail for an ask they made, null for a courier's own report.</summary>
    [JsonPropertyName("requestedBy")]
    public string? RequestedBy { get; set; }

    [JsonPropertyName("decisionOn")]
    public DateTime? DecisionOn { get; set; }

    /// <summary>The admin login id that approved or refused the ask.</summary>
    [JsonPropertyName("decisionBy")]
    public string? DecisionBy { get; set; }

    /// <summary>The note they gave with their answer - what the customer is told, which is why it is required.</summary>
    [JsonPropertyName("decisionComment")]
    public string? DecisionComment { get; set; }

    /// <summary>When the parcel was physically back with the shop.</summary>
    [JsonPropertyName("receivedOn")]
    public DateTime? ReceivedOn { get; set; }

    /// <summary>When the return was closed - the order 'Returned', its units on the shelf, its money owed back.</summary>
    [JsonPropertyName("closedOn")]
    public DateTime? ClosedOn { get; set; }

    /// <summary>When the returned parcel was looked over (see <see cref="InspectionComment"/>).</summary>
    [JsonPropertyName("inspectionOn")]
    public DateTime? InspectionOn { get; set; }

    [JsonPropertyName("inspectionBy")]
    public string? InspectionBy { get; set; }

    /// <summary>
    /// What the inspection found and why. Which units came back broken is the stock trail's business
    /// (SKUHistory rows: those SKUs are moved to the 'Damage' pool, see SkuAvailability), this is the
    /// sentence a human wrote with it.
    /// </summary>
    [JsonPropertyName("inspectionComment")]
    public string? InspectionComment { get; set; }

    /// <summary>
    /// True while the shop team can still answer it (OrderReturnStatus.CanDecide) - exactly when the card
    /// shows 'Approve' and 'Refuse'.
    /// </summary>
    [JsonPropertyName("canDecide")]
    public bool CanDecide { get; set; }

    /// <summary>
    /// True while the return is live - waiting for an answer, or with its parcel on the way back
    /// (OrderReturnStatus.IsOpen) - which is what the order list flags.
    /// </summary>
    [JsonPropertyName("isOpen")]
    public bool IsOpen { get; set; }

    /// <summary>
    /// True while the shop team can book the parcel back in (OrderReturnStatus.CanMarkReceived) - exactly when
    /// the card offers 'Parcel received'.
    /// </summary>
    [JsonPropertyName("canMarkReceived")]
    public bool CanMarkReceived { get; set; }

    /// <summary>
    /// True while the parcel may be looked over (OrderReturnStatus.CanInspect): it is back with the shop, so a
    /// unit that came back broken can be written off before the return is closed.
    /// </summary>
    [JsonPropertyName("canInspect")]
    public bool CanInspect { get; set; }

    /// <summary>
    /// True while the return can be closed (OrderReturnStatus.CanClose) - exactly when the card offers
    /// 'Close return': the order becomes 'Returned', the units go back on sale and the refund is asked for.
    /// </summary>
    [JsonPropertyName("canClose")]
    public bool CanClose { get; set; }

    /// <summary>
    /// The parcel coming back - the pickup the shop team booked on this card, or the courier's own
    /// return-to-origin. Null until one is recorded, which is exactly what the card offers once the return
    /// has been approved.
    /// </summary>
    [JsonPropertyName("shipment")]
    public OrderShipmentDto? Shipment { get; set; }
}

/// <summary>
/// Body of the shop team's answer to a return (POST api/admin/orders/{id}/returns/decision):
/// <see cref="Approved"/> says which answer it is and <see cref="Comment"/> is the note the customer is
/// told - the screen insists on one, because 'refused' with no reason is nothing a customer can act on (it
/// is kept in the order history as well).
/// </summary>
public class AdminOrderReturnDecisionRequest
{
    [JsonPropertyName("approved")]
    public bool Approved { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }
}

/// <summary>
/// Body of the 'the parcel is back' step (POST api/admin/orders/{id}/returns/received): the shop team has the
/// parcel in front of them. The note is optional and goes on the order's own trail, which the customer reads in
/// 'My Orders'.
/// </summary>
public class AdminOrderReturnReceivedRequest
{
    [JsonPropertyName("comment")]
    public string? Comment { get; set; }
}

/// <summary>
/// Body of the inspection (POST api/admin/orders/{id}/returns/inspection): the units that came back broken -
/// the SKUs of this order's own items, because one of three identical tops can be torn while the other two are
/// fine - and what the shop team wrote about the parcel.
///
/// An inspection with no units named is still recorded ("we looked and found nothing"), which is deliberately a
/// different thing from nobody having looked (see <see cref="AdminOrderReturnDto.InspectionOn"/>). Naming a SKU
/// that is not this order's is ignored rather than written off (see SkuAvailability.MarkUnitsDamagedAsync).
/// </summary>
public class AdminOrderReturnInspectionRequest
{
    [JsonPropertyName("damagedSkus")]
    public string[] DamagedSkus { get; set; } = Array.Empty<string>();

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }
}

/// <summary>
/// Body of the close (POST api/admin/orders/{id}/returns/close): an optional note for the order's own trail.
/// Closing has nothing left to decide - the order becomes 'Returned', its units go back on sale and the refund
/// is asked for - so this carries nothing but the note.
/// </summary>
public class AdminOrderReturnCloseRequest
{
    [JsonPropertyName("comment")]
    public string? Comment { get; set; }
}

// Customers
public class AdminCustomerListDto
{
    [JsonPropertyName("customerId")]
    public long CustomerId { get; set; }
    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = "";
    [JsonPropertyName("middleName")]
    public string? MiddleName { get; set; }
    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }
    [JsonPropertyName("emailId")]
    public string EmailId { get; set; } = "";
    [JsonPropertyName("mobileNumber")]
    public string? MobileNumber { get; set; }
    [JsonPropertyName("mobileVerified")]
    public bool? MobileVerified { get; set; }
    [JsonPropertyName("emailVerified")]
    public bool EmailVerified { get; set; }
    [JsonPropertyName("createdOn")]
    public DateTime CreatedOn { get; set; }
    [JsonPropertyName("modifiedOn")]
    public DateTime ModifiedOn { get; set; }
    [JsonPropertyName("customerStatusId")]
    public short CustomerStatusId { get; set; }
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
}

public class AdminCustomerDetailDto
{
    [JsonPropertyName("customerId")]
    public long CustomerId { get; set; }
    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = "";
    [JsonPropertyName("middleName")]
    public string? MiddleName { get; set; }
    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }
    [JsonPropertyName("emailId")]
    public string EmailId { get; set; } = "";
    [JsonPropertyName("mobileNumber")]
    public string? MobileNumber { get; set; }
    [JsonPropertyName("mobileVerified")]
    public bool? MobileVerified { get; set; }
    [JsonPropertyName("emailVerified")]
    public bool EmailVerified { get; set; }
    [JsonPropertyName("createdOn")]
    public DateTime CreatedOn { get; set; }
    [JsonPropertyName("modifiedOn")]
    public DateTime ModifiedOn { get; set; }
    [JsonPropertyName("customerStatusId")]
    public short CustomerStatusId { get; set; }
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
    [JsonPropertyName("addresses")]
    public List<AdminCustomerAddressDto> Addresses { get; set; } = new();
    [JsonPropertyName("orderCount")]
    public int OrderCount { get; set; }
    [JsonPropertyName("totalSpent")]
    public decimal TotalSpent { get; set; }
}

public class AdminCustomerAddressDto
{
    [JsonPropertyName("addressId")]
    public long AddressId { get; set; }
    [JsonPropertyName("addressTitle")]
    public string AddressTitle { get; set; } = "";
    [JsonPropertyName("contactName")]
    public string ContactName { get; set; } = "";
    [JsonPropertyName("addressLine1")]
    public string AddressLine1 { get; set; } = "";
    [JsonPropertyName("addressLine2")]
    public string? AddressLine2 { get; set; }
    [JsonPropertyName("city")]
    public string City { get; set; } = "";
    [JsonPropertyName("state")]
    public string State { get; set; } = "";
    [JsonPropertyName("country")]
    public string Country { get; set; } = "";
    [JsonPropertyName("zipcode")]
    public string Zipcode { get; set; } = "";
    [JsonPropertyName("mobileNumber")]
    public string MobileNumber { get; set; } = "";
}

public class UpdateCustomerStatusRequest
{
    [JsonPropertyName("customerId")]
    public long CustomerId { get; set; }
    [JsonPropertyName("customerStatusId")]
    public short CustomerStatusId { get; set; }
}


// Partners
public class AdminPartnerListDto
{
    [JsonPropertyName("partnerId")]
    public int PartnerId { get; set; }
    [JsonPropertyName("partnerName")]
    public string PartnerName { get; set; } = "";
    [JsonPropertyName("partnerStatusId")]
    public short PartnerStatusId { get; set; }
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
    [JsonPropertyName("lastModifiedOn")]
    public DateTime LastModifiedOn { get; set; }
}

public class AdminPartnerDetailDto
{
    [JsonPropertyName("partnerId")]
    public int PartnerId { get; set; }
    [JsonPropertyName("partnerName")]
    public string PartnerName { get; set; } = "";
    [JsonPropertyName("partnerStatusId")]
    public short PartnerStatusId { get; set; }
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
    [JsonPropertyName("lastModifiedOn")]
    public DateTime LastModifiedOn { get; set; }
    [JsonPropertyName("users")]
    public List<AdminPartnerUserDto> Users { get; set; } = new();
    [JsonPropertyName("inventoryCount")]
    public int InventoryCount { get; set; }
    [JsonPropertyName("orderCount")]
    public int OrderCount { get; set; }
}

public class AdminPartnerUserDto
{
    [JsonPropertyName("userId")]
    public long UserId { get; set; }
    [JsonPropertyName("userName")]
    public string UserName { get; set; } = "";
    [JsonPropertyName("loginId")]
    public string LoginId { get; set; } = "";
    [JsonPropertyName("emailId")]
    public string? EmailId { get; set; }
    [JsonPropertyName("mobileNumber")]
    public string? MobileNumber { get; set; }
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = new();
}

public class PartnerStatusOptionDto
{
    [JsonPropertyName("partnerStatusId")]
    public short PartnerStatusId { get; set; }
    [JsonPropertyName("partnerStatus")]
    public string PartnerStatus { get; set; } = "";
}

public class PartnerFormRequest
{
    [JsonPropertyName("partnerName")]
    public string PartnerName { get; set; } = "";
    [JsonPropertyName("partnerStatusId")]
    public short PartnerStatusId { get; set; }
}

// Vendors
public class AdminVendorListDto
{
    [JsonPropertyName("vendorId")]
    public short VendorId { get; set; }
    [JsonPropertyName("vendorName")]
    public string VendorName { get; set; } = "";
    [JsonPropertyName("vendorAddress")]
    public string? VendorAddress { get; set; }
    [JsonPropertyName("mobile")]
    public string Mobile { get; set; } = "";
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
}

public class AdminVendorDetailDto
{
    [JsonPropertyName("vendorId")]
    public short VendorId { get; set; }
    [JsonPropertyName("vendorName")]
    public string VendorName { get; set; } = "";
    [JsonPropertyName("vendorAddress")]
    public string? VendorAddress { get; set; }
    [JsonPropertyName("mobile")]
    public string Mobile { get; set; } = "";
    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
    [JsonPropertyName("users")]
    public List<AdminVendorUserDto> Users { get; set; } = new();
    [JsonPropertyName("purchaseCount")]
    public int PurchaseCount { get; set; }
}

public class AdminVendorUserDto
{
    [JsonPropertyName("userId")]
    public long UserId { get; set; }
    [JsonPropertyName("userName")]
    public string UserName { get; set; } = "";
    [JsonPropertyName("loginId")]
    public string LoginId { get; set; } = "";
    [JsonPropertyName("emailId")]
    public string? EmailId { get; set; }
    [JsonPropertyName("mobileNumber")]
    public string? MobileNumber { get; set; }
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = new();
}

public class VendorFormRequest
{
    [JsonPropertyName("vendorName")]
    public string VendorName { get; set; } = "";
    [JsonPropertyName("vendorAddress")]
    public string? VendorAddress { get; set; }
    [JsonPropertyName("mobile")]
    public string Mobile { get; set; } = "";
    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
}

// Purchases
public class AdminPurchaseListDto
{
    [JsonPropertyName("purchaseId")]
    public long PurchaseId { get; set; }
    [JsonPropertyName("purchaseNumber")]
    public string? PurchaseNumber { get; set; }
    [JsonPropertyName("vendorId")]
    public short VendorId { get; set; }
    [JsonPropertyName("vendorName")]
    public string VendorName { get; set; } = "";
    [JsonPropertyName("purchaserName")]
    public string PurchaserName { get; set; } = "";
    [JsonPropertyName("purchaseDate")]
    public DateTime PurchaseDate { get; set; }
    [JsonPropertyName("purchaseStatusId")]
    public short PurchaseStatusId { get; set; }
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
    [JsonPropertyName("itemCount")]
    public int ItemCount { get; set; }
    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }
}

public class AdminPurchaseDetailDto
{
    [JsonPropertyName("purchaseId")]
    public long PurchaseId { get; set; }
    [JsonPropertyName("purchaseNumber")]
    public string? PurchaseNumber { get; set; }
    [JsonPropertyName("vendorId")]
    public short VendorId { get; set; }
    [JsonPropertyName("vendorName")]
    public string VendorName { get; set; } = "";
    [JsonPropertyName("purchaserId")]
    public int PurchaserId { get; set; }
    [JsonPropertyName("purchaserName")]
    public string PurchaserName { get; set; } = "";
    [JsonPropertyName("purchaseDate")]
    public DateTime PurchaseDate { get; set; }
    [JsonPropertyName("purchaseStatusId")]
    public short PurchaseStatusId { get; set; }
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
    [JsonPropertyName("invoicePath")]
    public string? InvoicePath { get; set; }
    [JsonPropertyName("addedByName")]
    public string AddedByName { get; set; } = "";
    [JsonPropertyName("addedOn")]
    public DateTime AddedOn { get; set; }
    [JsonPropertyName("lastModifiedByName")]
    public string LastModifiedByName { get; set; } = "";
    [JsonPropertyName("lastModifiedOn")]
    public DateTime LastModifiedOn { get; set; }
    [JsonPropertyName("items")]
    public List<AdminPurchaseItemDto> Items { get; set; } = new();
    [JsonPropertyName("comments")]
    public List<AdminPurchaseCommentDto> Comments { get; set; } = new();
}

public class AdminPurchaseItemDto
{
    [JsonPropertyName("purchaseDetailId")]
    public long PurchaseDetailId { get; set; }
    [JsonPropertyName("productId")]
    public int ProductId { get; set; }
    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = "";
    [JsonPropertyName("quantity")]
    public short Quantity { get; set; }
    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }
    [JsonPropertyName("gst")]
    public decimal Gst { get; set; }
    [JsonPropertyName("lineTotal")]
    public decimal LineTotal { get; set; }
}

public class AdminPurchaseCommentDto
{
    [JsonPropertyName("purchaseCommentId")]
    public long PurchaseCommentId { get; set; }
    [JsonPropertyName("comments")]
    public string Comments { get; set; } = "";
    [JsonPropertyName("addedByName")]
    public string AddedByName { get; set; } = "";
    [JsonPropertyName("addedOn")]
    public DateTime AddedOn { get; set; }
}

// Purchases (create)
public class AdminPurchaseCreateRequest
{
    [JsonPropertyName("vendorId")]
    public short VendorId { get; set; }
    [JsonPropertyName("purchaserId")]
    public int PurchaserId { get; set; }
    [JsonPropertyName("purchaseDate")]
    public DateTime PurchaseDate { get; set; }
    [JsonPropertyName("invoicePath")]
    public string? InvoicePath { get; set; }
    [JsonPropertyName("purchaseStatusId")]
    public short PurchaseStatusId { get; set; } = 1; // 1 = ADDED
    [JsonPropertyName("items")]
    public List<AdminPurchaseCreateItemDto> Items { get; set; } = new();
}

public class AdminPurchaseCreateItemDto
{
    [JsonPropertyName("productId")]
    public int ProductId { get; set; }
    [JsonPropertyName("quantity")]
    public short Quantity { get; set; }
    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }
    [JsonPropertyName("gst")]
    public decimal Gst { get; set; }
}

public class AdminPurchaserDto
{
    [JsonPropertyName("purchaserId")]
    public int PurchaserId { get; set; }
    [JsonPropertyName("purchaserName")]
    public string PurchaserName { get; set; } = "";
    [JsonPropertyName("partnerName")]
    public string PartnerName { get; set; } = "";
}

// Purchases (status workflow & edit)
public class AdminPurchaseStatusDto
{
    [JsonPropertyName("purchaseStatusId")]
    public short PurchaseStatusId { get; set; }
    [JsonPropertyName("purchaseStatusName")]
    public string PurchaseStatusName { get; set; } = "";
}

public class AdminPurchaseStatusUpdateRequest
{
    [JsonPropertyName("statusId")]
    public short StatusId { get; set; }
    [JsonPropertyName("comments")]
    public string? Comments { get; set; }
}

public class AdminPurchaseUpdateRequest
{
    [JsonPropertyName("vendorId")]
    public short VendorId { get; set; }
    [JsonPropertyName("purchaserId")]
    public int PurchaserId { get; set; }
    [JsonPropertyName("purchaseDate")]
    public DateTime PurchaseDate { get; set; }
    [JsonPropertyName("invoicePath")]
    public string? InvoicePath { get; set; }
}

public class AdminPurchaseItemSaveDto
{
    [JsonPropertyName("purchaseDetailId")]
    public long PurchaseDetailId { get; set; }
    [JsonPropertyName("productId")]
    public int ProductId { get; set; }
    [JsonPropertyName("quantity")]
    public short Quantity { get; set; }
    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }
    [JsonPropertyName("gst")]
    public decimal Gst { get; set; }
}

public class AdminPurchaseItemsRequest
{
    [JsonPropertyName("items")]
    public List<AdminPurchaseItemSaveDto> Items { get; set; } = new();
}

public class AdminPurchaseCommentRequest
{
    [JsonPropertyName("comments")]
    public string Comments { get; set; } = "";
}

// Users (Admin Users)
public class AdminUserListDto
{
    [JsonPropertyName("userId")]
    public long UserId { get; set; }
    [JsonPropertyName("loginId")]
    public string LoginId { get; set; } = "";
    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = "";
    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }
    [JsonPropertyName("emailId")]
    public string? EmailId { get; set; }
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = new();
}

public class AdminUserDetailDto
{
    [JsonPropertyName("userId")]
    public long UserId { get; set; }
    [JsonPropertyName("loginId")]
    public string LoginId { get; set; } = "";
    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = "";
    [JsonPropertyName("middleName")]
    public string? MiddleName { get; set; }
    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }
    [JsonPropertyName("emailId")]
    public string? EmailId { get; set; }
    [JsonPropertyName("mobileNumber")]
    public string? MobileNumber { get; set; }
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
    [JsonPropertyName("mustChangePassword")]
    public bool MustChangePassword { get; set; }
    [JsonPropertyName("roles")]
    public List<AdminRoleDto> Roles { get; set; } = new();
}

public class AdminUserCreateRequest
{
    [JsonPropertyName("loginId")]
    public string LoginId { get; set; } = "";
    [JsonPropertyName("password")]
    public string Password { get; set; } = "";
    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = "";
    [JsonPropertyName("middleName")]
    public string? MiddleName { get; set; }
    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }
    [JsonPropertyName("emailId")]
    public string? EmailId { get; set; }
    [JsonPropertyName("mobileNumber")]
    public string? MobileNumber { get; set; }
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
    [JsonPropertyName("mustChangePassword")]
    public bool MustChangePassword { get; set; }
    [JsonPropertyName("roleIds")]
    public List<short> RoleIds { get; set; } = new();
}

public class AdminUserUpdateRequest
{
    [JsonPropertyName("loginId")]
    public string LoginId { get; set; } = "";
    [JsonPropertyName("password")]
    public string? Password { get; set; }
    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = "";
    [JsonPropertyName("middleName")]
    public string? MiddleName { get; set; }
    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }
    [JsonPropertyName("emailId")]
    public string? EmailId { get; set; }
    [JsonPropertyName("mobileNumber")]
    public string? MobileNumber { get; set; }
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
    [JsonPropertyName("mustChangePassword")]
    public bool MustChangePassword { get; set; }
    [JsonPropertyName("roleIds")]
    public List<short> RoleIds { get; set; } = new();
}

// Categories
public class AdminCategoryDto
{
    [JsonPropertyName("categoryId")]
    public short CategoryId { get; set; }
    [JsonPropertyName("categoryName")]
    public string CategoryName { get; set; } = "";
    [JsonPropertyName("parentCategoryId")]
    public short? ParentCategoryId { get; set; }
    [JsonPropertyName("parentCategoryName")]
    public string? ParentCategoryName { get; set; }
}

// Forgot Password
public class AdminForgotPasswordRequest
{
    public string LoginId { get; set; } = "";
}

public class AdminResetPasswordRequest
{
    public string Token { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

// Generic
public class AdminResultDto
{
    [JsonPropertyName("result")]
    public int Result { get; set; }
    [JsonPropertyName("messages")]
    public string[] Messages { get; set; } = Array.Empty<string>();
}
