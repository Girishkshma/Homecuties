using HC.Business.Dtos;

namespace HC.Business;

public interface IAdminDashboardService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync();
    Task<List<AdminProductListDto>> GetProductsAsync();
    Task<AdminProductDetailDto?> GetProductDetailAsync(int productId);
    Task<AdminResultDto> CreateProductAsync(CreateProductRequest request, long userId);
    Task<AdminResultDto> UpdateProductAsync(int productId, CreateProductRequest request, long userId);
    Task<AdminResultDto> DeleteProductAsync(int productId);
    Task<List<AdminOrderListDto>> GetOrdersAsync();
    Task<AdminOrderDetailDto?> GetOrderDetailAsync(long orderId);
    Task<List<AdminCustomerListDto>> GetCustomersAsync();
    Task<AdminCustomerDetailDto?> GetCustomerDetailAsync(long customerId);
    Task<AdminResultDto> UpdateCustomerStatusAsync(long customerId, short customerStatusId);
    Task<List<AdminCustomerListDto>> SearchCustomersAsync(string searchTerm);
    Task<List<AdminPartnerListDto>> GetPartnersAsync();
    Task<AdminPartnerDetailDto?> GetPartnerDetailAsync(int partnerId);
    Task<List<AdminVendorListDto>> GetVendorsAsync();
    Task<AdminVendorDetailDto?> GetVendorDetailAsync(short vendorId);
    Task<List<AdminUserListDto>> GetAdminUsersAsync();
    Task<List<AdminCategoryDto>> GetCategoriesAsync();
    Task<List<AdminCategoryDto>> GetCategoryTreeAsync();
}
