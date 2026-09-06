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
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }
    [JsonPropertyName("itemCount")]
    public int ItemCount { get; set; }
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
    [JsonPropertyName("customerEmail")]
    public string CustomerEmail { get; set; } = "";
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
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
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
    [JsonPropertyName("comments")]
    public string Comments { get; set; } = "";
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
