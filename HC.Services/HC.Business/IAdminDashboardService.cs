using HC.Business.Dtos;

namespace HC.Business;

public interface IAdminDashboardService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync();
    Task<List<AdminProductListDto>> GetProductsAsync();
    Task<AdminProductDetailDto?> GetProductDetailAsync(int productId);
    Task<AdminResultDto> CreateProductAsync(CreateProductRequest request, long userId);
    Task<AdminResultDto> UpdateProductAsync(int productId, CreateProductRequest request, long userId);
    Task<AdminResultDto> DeactivateProductAsync(int productId, long userId);
    Task<ProductFormOptionsDto> GetProductFormOptionsAsync();
    Task<List<AdminOrderListDto>> GetOrdersAsync();
    Task<AdminOrderDetailDto?> GetOrderDetailAsync(long orderId);
    Task<List<AdminCustomerListDto>> GetCustomersAsync();
    Task<AdminCustomerDetailDto?> GetCustomerDetailAsync(long customerId);
    Task<AdminResultDto> UpdateCustomerStatusAsync(long customerId, short customerStatusId);
    Task<List<AdminCustomerListDto>> SearchCustomersAsync(string searchTerm);
    Task<List<AdminPartnerListDto>> GetPartnersAsync();
    Task<AdminPartnerDetailDto?> GetPartnerDetailAsync(int partnerId);
    Task<List<PartnerStatusOptionDto>> GetPartnerStatusesAsync();
    Task<AdminResultDto> CreatePartnerAsync(PartnerFormRequest request, long currentUserId);
    Task<AdminResultDto> UpdatePartnerAsync(int partnerId, PartnerFormRequest request, long currentUserId);
    Task<List<AdminVendorListDto>> GetVendorsAsync();
    Task<AdminVendorDetailDto?> GetVendorDetailAsync(short vendorId);
    Task<AdminResultDto> CreateVendorAsync(VendorFormRequest request, long currentUserId);
    Task<AdminResultDto> UpdateVendorAsync(short vendorId, VendorFormRequest request, long currentUserId);
    Task<List<AdminPurchaseListDto>> GetPurchasesAsync();
    Task<AdminPurchaseDetailDto?> GetPurchaseDetailAsync(long purchaseId);
    Task<List<AdminUserListDto>> GetAdminUsersAsync();
    Task<AdminUserDetailDto?> GetAdminUserAsync(long userId);
    Task<List<AdminRoleDto>> GetAdminRolesAsync();
    Task<AdminResultDto> CreateAdminUserAsync(AdminUserCreateRequest request, long currentUserId);
    Task<AdminResultDto> UpdateAdminUserAsync(long userId, AdminUserUpdateRequest request, long currentUserId);
    Task<List<AdminCategoryDto>> GetCategoriesAsync();
    Task<List<AdminCategoryDto>> GetCategoryTreeAsync();
}
