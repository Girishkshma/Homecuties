namespace HC.Business.Dtos;

// Login
public class AdminLoginRequest
{
    public string LoginId { get; set; } = "";
    public string Password { get; set; } = "";
}

public class AdminLoginResponse
{
    public int Result { get; set; }
    public string[] Messages { get; set; } = Array.Empty<string>();
    public AdminUserDto? User { get; set; }
    public string? Token { get; set; }
    public DateTime? ExpiresOn { get; set; }
}

public class AdminUserDto
{
    public long UserId { get; set; }
    public string LoginId { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? EmailId { get; set; }
    public string? MobileNumber { get; set; }
    public bool IsActive { get; set; }
    public List<AdminRoleDto> Roles { get; set; } = new();
}

public class AdminRoleDto
{
    public short RoleId { get; set; }
    public string RoleName { get; set; } = "";
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
    public short MenuId { get; set; }
    public string MenuTitle { get; set; } = "";
    public string? MenuDescription { get; set; }
    public string MenuUrl { get; set; } = "";
    public short? ParentMenuId { get; set; }
    public bool IsActive { get; set; }
    public List<AdminMenuDto> Children { get; set; } = new();
    public List<AdminActivityDto> Activities { get; set; } = new();
}

public class AdminActivityDto
{
    public short ActivityId { get; set; }
    public string ActivityTitle { get; set; } = "";
    public short MenuId { get; set; }
    public bool IsActive { get; set; }
}

// Dashboard Stats
public class DashboardStatsDto
{
    public int TotalProducts { get; set; }
    public int TotalOrders { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalPartners { get; set; }
    public int TotalVendors { get; set; }
    public int PendingOrders { get; set; }
    public decimal TodayRevenue { get; set; }
    public decimal MonthlyRevenue { get; set; }
}

// Products
public class AdminProductListDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductTitle { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public string Status { get; set; } = "";
    public bool DisplayOnHomePage { get; set; }
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; } = "";
}

public class AdminProductDetailDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductTitle { get; set; } = "";
    public string ProductDescription { get; set; } = "";
    public bool DisplayOnHomePage { get; set; }
    public short ProductStatusId { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Hsncode { get; set; }
    public decimal PackagingCharge { get; set; }
    public decimal StorageCharge { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal AdditionalDiscountPercent { get; set; }
    public decimal DeliveryCharge { get; set; }
    public decimal ProfitMarginPercent { get; set; }
    public decimal Cgstpercent { get; set; }
    public decimal Sgstpercent { get; set; }
    public decimal Igstpercent { get; set; }
    public List<short> CategoryIds { get; set; } = new();
    public List<AdminProductFeatureDto> Features { get; set; } = new();
    public List<AdminProductImageDto> Images { get; set; } = new();
}

public class AdminProductFeatureDto
{
    public long? ProductFeatureId { get; set; }
    public string Feature { get; set; } = "";
    public bool IsActive { get; set; }
}

public class AdminProductImageDto
{
    public long? ProductImageId { get; set; }
    public string ImageUrl { get; set; } = "";
    public short ImageTypeId { get; set; }
    public int ImageIndex { get; set; }
    public bool IsPromoImage { get; set; }
    public bool IsActive { get; set; }
}

public class CreateProductRequest
{
    public string ProductName { get; set; } = "";
    public string ProductTitle { get; set; } = "";
    public string ProductDescription { get; set; } = "";
    public bool DisplayOnHomePage { get; set; }
    public short ProductStatusId { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Hsncode { get; set; }
    public decimal PackagingCharge { get; set; }
    public decimal StorageCharge { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal AdditionalDiscountPercent { get; set; }
    public decimal DeliveryCharge { get; set; }
    public decimal ProfitMarginPercent { get; set; }
    public decimal Cgstpercent { get; set; }
    public decimal Sgstpercent { get; set; }
    public decimal Igstpercent { get; set; }
    public List<short> CategoryIds { get; set; } = new();
    public List<AdminProductFeatureDto> Features { get; set; } = new();
    public List<AdminProductImageDto> Images { get; set; } = new();
}

// Orders
public class AdminOrderListDto
{
    public long OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public DateTime OrderDate { get; set; }
    public string CustomerName { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
}

public class AdminOrderDetailDto
{
    public long OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public DateTime OrderDate { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string Status { get; set; } = "";
    public string SellerName { get; set; } = "";
    public AdminAddressDto BillingAddress { get; set; } = new();
    public AdminAddressDto ShippingAddress { get; set; } = new();
    public List<AdminOrderItemDto> Items { get; set; } = new();
    public List<AdminOrderHistoryDto> History { get; set; } = new();
}

public class AdminAddressDto
{
    public string AddressTitle { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string AddressLine1 { get; set; } = "";
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Zipcode { get; set; } = "";
    public string MobileNumber { get; set; } = "";
}

public class AdminOrderItemDto
{
    public string Sku { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string ProductTitle { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal AdditionalDiscountPercent { get; set; }
    public decimal DeliveryCharge { get; set; }
    public decimal PackagingCharge { get; set; }
    public decimal StorageCharge { get; set; }
    public decimal ProfitMarginPercent { get; set; }
    public decimal Cgstpercent { get; set; }
    public decimal Sgstpercent { get; set; }
    public decimal Igstpercent { get; set; }
}

public class AdminOrderHistoryDto
{
    public DateTime HistoryDate { get; set; }
    public string Status { get; set; } = "";
    public string Comments { get; set; } = "";
}

// Customers
public class AdminCustomerListDto
{
    public long CustomerId { get; set; }
    public string FirstName { get; set; } = "";
    public string? LastName { get; set; }
    public string EmailId { get; set; } = "";
    public string? MobileNumber { get; set; }
    public DateTime CreatedOn { get; set; }
    public string Status { get; set; } = "";
}

// Partners
public class AdminPartnerListDto
{
    public int PartnerId { get; set; }
    public string PartnerName { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime LastModifiedOn { get; set; }
}

// Vendors
public class AdminVendorListDto
{
    public short VendorId { get; set; }
    public string VendorName { get; set; } = "";
    public string? VendorAddress { get; set; }
    public string Mobile { get; set; } = "";
    public bool IsActive { get; set; }
}

// Users (Admin Users)
public class AdminUserListDto
{
    public long UserId { get; set; }
    public string LoginId { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string? LastName { get; set; }
    public string? EmailId { get; set; }
    public bool IsActive { get; set; }
    public List<string> Roles { get; set; } = new();
}

// Categories
public class AdminCategoryDto
{
    public short CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public short? ParentCategoryId { get; set; }
    public string? ParentCategoryName { get; set; }
}

// Generic
public class AdminResultDto
{
    public int Result { get; set; }
    public string[] Messages { get; set; } = Array.Empty<string>();
}
