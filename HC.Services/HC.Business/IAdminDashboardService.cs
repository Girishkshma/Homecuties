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
    Task<List<AdminOrderStatusDto>> GetOrderStatusesAsync();
    Task<AdminResultDto> UpdateOrderStatusAsync(long orderId, AdminOrderStatusUpdateRequest request, long currentUserId);
    Task<AdminResultDto> ApproveOrderRefundAsync(long orderId, long currentUserId);
    Task<AdminResultDto> MarkOrderRefundedAsync(long orderId, string? comment, long currentUserId);

    /// <summary>
    /// The shop team's answer to a return: approve it (the parcel is coming back, and the pickup is booked
    /// from the same card) or refuse it (the order is left exactly as it was). The note is required, because
    /// it is what the customer is told. Nothing else moves on the answer - the order only becomes 'Returned',
    /// with its units back on the shelf and its money owed back, when the return is closed after the parcel
    /// is back (see HC.Business.OrderReturnFlow).
    /// </summary>
    Task<AdminResultDto> DecideOrderReturnAsync(long orderId, AdminOrderReturnDecisionRequest request, long currentUserId);

    /// <summary>
    /// 'Parcel received' on the Return card: the parcel is physically back with the shop. The return becomes
    /// 'Received' and its units leave the delivery pools for the shop's own 'Returned' one - off sale until the
    /// return is closed - and nothing else moves: the order is still Delivered and the money is untouched until
    /// that close (see HC.Business.OrderReturnFlow).
    /// </summary>
    Task<AdminResultDto> MarkOrderReturnReceivedAsync(long orderId, AdminOrderReturnReceivedRequest request, long currentUserId);

    /// <summary>
    /// The inspection of a parcel that came back: the units the shop team names are written off - out of every
    /// sellable pool for good - and their note is kept on the return. It is per unit, because one of three
    /// identical tops can be torn while the other two are fine, and it is optional: a return with nothing wrong
    /// with it is closed without one (see HC.Business.OrderReturnFlow).
    /// </summary>
    Task<AdminResultDto> MarkOrderReturnUnitsDamagedAsync(long orderId, AdminOrderReturnInspectionRequest request, long currentUserId);

    /// <summary>
    /// 'Close return' on the Return card: the return is done. The order becomes 'Returned', every unit the return
    /// brought back goes on sale again (the ones written off stay written off) and the refund of what the customer
    /// paid is asked for - the shop team approves that on the Refund card, which is what sends the money back (see
    /// HC.Business.RazorpayRefunds).
    /// </summary>
    Task<AdminResultDto> CloseOrderReturnAsync(long orderId, AdminOrderReturnCloseRequest request, long currentUserId);

    /// <summary>
    /// The shipping providers this shop is set up with (configured or not, with the default marked) -
    /// what the Shipment card of the admin order screen offers when the shop team records a parcel, so
    /// the provider is picked from what is really wired up rather than typed in by hand.
    /// </summary>
    Task<List<ShipmentProviderInfo>> GetShipmentProvidersAsync();

    /// <summary>
    /// Records the parcel the shop team booked in the provider's panel: the AWB, and the courier and
    /// tracking link when they have them. This never moves the order - a parcel booked is not a parcel
    /// dispatched, and the order follows the courier's own reports (see <see cref="TrackOrderShipmentAsync"/>).
    /// The AWB is written to the order's history, so the timeline says who recorded the parcel and when.
    /// </summary>
    Task<OrderShipmentDto> SaveOrderShipmentAsync(long orderId, SaveOrderShipmentRequest request, long currentUserId);

    /// <summary>
    /// Asks the courier about this order's parcel right now ('Track now' on the admin order screen) and
    /// writes down what it said, moving the order - and its units - along when the courier's own status
    /// allows it. The shop team asked on purpose, so the throttle that protects the customer-facing pull
    /// does not apply here.
    /// </summary>
    Task<OrderShipmentDto> TrackOrderShipmentAsync(long orderId, long currentUserId);
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
    Task<List<AdminPurchaserDto>> GetPurchasersAsync();
    Task<AdminResultDto> CreatePurchaseAsync(AdminPurchaseCreateRequest request, long currentUserId);
    Task<List<AdminPurchaseStatusDto>> GetPurchaseStatusesAsync(long purchaseId, long currentUserId);
    Task<AdminResultDto> UpdatePurchaseStatusAsync(long purchaseId, AdminPurchaseStatusUpdateRequest request, long currentUserId);
    Task<AdminResultDto> UpdatePurchaseAsync(long purchaseId, AdminPurchaseUpdateRequest request, long currentUserId);
    Task<AdminResultDto> SavePurchaseItemsAsync(long purchaseId, AdminPurchaseItemsRequest request, long currentUserId);
    Task<AdminResultDto> AddPurchaseCommentAsync(long purchaseId, AdminPurchaseCommentRequest request, long currentUserId);
    Task<List<AdminUserListDto>> GetAdminUsersAsync();
    Task<AdminUserDetailDto?> GetAdminUserAsync(long userId);
    Task<List<AdminRoleDto>> GetAdminRolesAsync();
    Task<AdminResultDto> CreateAdminUserAsync(AdminUserCreateRequest request, long currentUserId);
    Task<AdminResultDto> UpdateAdminUserAsync(long userId, AdminUserUpdateRequest request, long currentUserId);
    Task<List<AdminCategoryDto>> GetCategoriesAsync();
    Task<List<AdminCategoryDto>> GetCategoryTreeAsync();
}
