using HC.Business.Dtos;

namespace HC.Business;

public interface IAdminDashboardService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync();
    Task<List<AdminProductListDto>> GetProductsAsync();
    Task<AdminProductDetailDto?> GetProductDetailAsync(int productId);
    Task<CreateProductResultDto> CreateProductAsync(CreateProductRequest request, long userId);
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

    /// <summary>
    /// The catalogue as the admin manages it: every category in the order the screens list it, with where each one
    /// sits and what is filed on it (see <see cref="CategoryTree.RowsInDisplayOrder"/> for the walk and depth, and
    /// AdminDashboardService.Categories.cs for the reads).
    ///
    /// Everything is listed, including the rows a heading leads into and the rows inside a loop the table has been
    /// left in - a category an admin cannot see is one they cannot repair. The two figures on each row, the products
    /// filed on it and the categories sitting directly under it, are what say why it cannot be taken out yet (see
    /// <see cref="CategoryCatalog.DeleteProblem"/>); the screen shows them rather than working them out again.
    /// </summary>
    Task<List<AdminCategoryListDto>> GetManagedCategoriesAsync();

    /// <summary>
    /// Adds a category to the catalogue: a heading when it names no parent, a shelf of one when it does.
    ///
    /// The name and the place are put to <see cref="CategoryCatalog"/> first, and a refusal comes back as that rule's
    /// own sentence in an <see cref="AdminResultDto"/> rather than as a failure - an admin who is told why can fix it,
    /// and the row that would have been written is not written. Nothing else moves: no product is filed by adding a
    /// category, and a shelf of a heading sits last among its parent's shelves, in the order the table answers.
    /// </summary>
    Task<AdminResultDto> CreateCategoryAsync(CategoryFormRequest request, long currentUserId);

    /// <summary>
    /// Renames a category and moves it: the same body as <see cref="CreateCategoryAsync"/>, so a shelf can become a
    /// heading and a heading a shelf of another. The category's own shelves travel with it, which is exactly what
    /// makes the move worth refusing when the parent chosen is one of them: the branch would then be one no heading
    /// leads into, and the products filed anywhere in it would drop out of the shop's menus while still sitting in the
    /// table (see <see cref="CategoryCatalog.ParentProblem"/>).
    /// </summary>
    Task<AdminResultDto> UpdateCategoryAsync(short categoryId, CategoryFormRequest request, long currentUserId);

    /// <summary>
    /// Takes a category out of the catalogue, and nothing else.
    ///
    /// It is refused while the category still holds something the delete would leave behind - a category sitting under
    /// it, which would be left naming a parent that is gone, or a product filed on it, which would drop out of the
    /// shop's menus with the link that filed it (see <see cref="CategoryCatalog.DeleteProblem"/>, where both rules and
    /// their reasons live). A category that holds neither is a row nothing refers to, so the delete is exactly what it
    /// says and takes no product, order or purchase with it.
    /// </summary>
    Task<AdminResultDto> DeleteCategoryAsync(short categoryId, long currentUserId);

    /// <summary>
    /// The dashboard's 'Stock by category' cards: the shop's stock cut by the catalogue's headings, so the business
    /// can see which shelf needs restocking rather than which single product does - one card per heading, with the
    /// categories beneath it and what each of them can still sell (see HC.Business.CategoryStock for the rules behind
    /// every figure and AdminDashboardService.StockByCategory.cs for the reads).
    ///
    /// Like the tiles above it, this is a read of the whole shop: it takes no acting admin, and what a partner may
    /// see is not a question it answers - the figures are the shop's own stock, which is what the people who restock
    /// shelves work from.
    /// </summary>
    Task<AdminStockByCategoryDto> GetStockByCategoryAsync();

    /// <summary>
    /// The gateway's own books (see HC.Business.RazorpaySettlements): reads what Razorpay settled to the shop's
    /// bank account over a window - the rolling one when no window is given - and writes down what it settled it
    /// on, correcting each settled payment's charge with the figure the bank was actually paid on.
    ///
    /// No acting admin is taken: nothing here is an edit to an order, a refund or a parcel, and the only thing a
    /// person can get wrong about it is asking for the wrong window (which the answer says back).
    /// </summary>
    Task<AdminResultDto> SyncSettlementsAsync(DateTime? from, DateTime? to);

    /// <summary>
    /// The shop's own books over a period: what the customers paid, what was given back, what the gateway kept,
    /// what the parcels cost and what the shop's own margin on the goods was - the answer the Finance screen is
    /// built from (see AdminDashboardService.Finance.cs, where the reads are, and HC.Business.OrderMoney, which
    /// owns every money rule in it).
    ///
    /// Nothing here is an edit and nothing here asks the gateway: the settlement ledger is written by the pull
    /// alone (<see cref="SyncSettlementsAsync"/> and the server's own pass), and this only reports what has
    /// already been written down - including, in its messages, the days the pull has not been run for.
    ///
    /// The acting admin IS taken, and it is what decides whose books come back: an admin or a super admin is given
    /// the whole shop's, and a user the shop has linked to a partner (<c>PartnersUser</c>) that partner's own sales
    /// alone - the goods their own stock supplied, with the answer saying whose books these are. The id must be the
    /// signed-in admin's own, read from the validated token and never from the request (see
    /// AdminController.GetFinanceSummary): a caller that could name the user could name any partner and read their
    /// books.
    /// </summary>
    Task<AdminFinanceSummaryDto> GetFinanceSummaryAsync(DateTime? from, DateTime? to, long adminUserId);
}
