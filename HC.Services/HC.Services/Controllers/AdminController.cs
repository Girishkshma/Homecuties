using System.IO;
using HC.Business;
using HC.Business.Dtos;
using HC.Business.Security;
using HC.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace HC.Services.Controllers;

/// <summary>
/// Admin area API.
///
/// Every action requires the JWT that 'POST login' issued, sent as 'Authorization: Bearer &lt;token&gt;';
/// the token is validated (signature, issuer, audience, expiry, active account) before the action runs.
/// Beside being authenticated, the roles carried by the token decide which sections of the admin area
/// may be called - the role to menu mapping lives in 'AdminMenusRoles'.
///
/// Only login, forgot-password, reset-password and validate-token are anonymous, and the acting user id
/// is always taken from the token (never from the query string) so the audit trail cannot be forged.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IAdminAuthService _adminAuthService;
    private readonly IAdminDashboardService _adminDashboardService;
    private readonly IWebHostEnvironment _env;

    public AdminController(IAdminAuthService adminAuthService, IAdminDashboardService adminDashboardService, IWebHostEnvironment env)
    {
        _adminAuthService = adminAuthService;
        _adminDashboardService = adminDashboardService;
        _env = env;
    }

    #region Authentication

    /// <summary>Anonymous: checks the credentials and returns the signed JWT for the new session.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult> Login([FromBody] AdminLoginRequest request)
    {
        var result = await _adminAuthService.LoginAsync(request.LoginId, request.Password);
        return Ok(result);
    }

    /// <summary>Anonymous: validates a token that is presented in the body instead of the header.</summary>
    [AllowAnonymous]
    [HttpPost("validate-token")]
    public ActionResult ValidateToken([FromBody] AdminValidateJwtRequest request)
    {
        var ipAddress = string.IsNullOrEmpty(request.IPAddress) ? GetClientIp() : request.IPAddress;
        var user = _adminAuthService.ValidateToken(request.JWT, ipAddress);
        if (user == null)
            return Unauthorized(new { result = 0, messages = new[] { "Invalid or expired token." } });

        return Ok(new { result = 1, user = user });
    }

    /// <summary>Anonymous: a forgotten password cannot be reset without a token first.</summary>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<ActionResult> ForgotPassword([FromBody] AdminForgotPasswordRequest request)
    {
        var result = await _adminAuthService.ForgotPasswordAsync(request.LoginId);
        return Ok(result);
    }

    /// <summary>Anonymous: the reset token in the request is the credential.</summary>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPassword([FromBody] AdminResetPasswordRequest request)
    {
        var result = await _adminAuthService.ResetPasswordAsync(request.Token, request.NewPassword);
        return Ok(result);
    }

    /// <summary>
    /// Navigation of the signed-in admin: the menus granted to the roles in its own token. A role id
    /// may be sent to narrow the answer down to one of those roles, but never to another role's menus.
    /// </summary>
    [Authorize(Policy = AdminPolicies.AdminArea)]
    [HttpPost("menus")]
    public async Task<ActionResult> GetMenus([FromBody] AdminMenuRequest request)
    {
        var roleIds = AdminJwtTokenService.GetRoleIds(User);
        if (roleIds.Count == 0)
            return StatusCode(StatusCodes.Status403Forbidden, new { result = 0, messages = new[] { "Your roles do not grant access to the admin area." } });

        if (request.RoleId.HasValue && !roleIds.Contains(request.RoleId.Value))
            return StatusCode(StatusCodes.Status403Forbidden, new { result = 0, messages = new[] { "You do not have access to the menus of this role." } });

        var requestedRoleIds = request.RoleId.HasValue
            ? new List<short> { request.RoleId.Value }
            : roleIds;
        var menus = await _adminAuthService.GetMenusByRolesAsync(requestedRoleIds);

        return Ok(menus);
    }

    #endregion

    #region Dashboard

    /// <summary>Dashboard tiles: any admin that can reach at least one section.</summary>
    [Authorize(Policy = AdminPolicies.AdminArea)]
    [HttpGet("dashboard/stats")]
    public async Task<ActionResult> GetDashboardStats()
    {
        var stats = await _adminDashboardService.GetDashboardStatsAsync();
        return Ok(stats);
    }

    #endregion

    #region Products

    [Authorize(Policy = AdminPolicies.Products)]
    [HttpGet("products")]
    public async Task<ActionResult> GetProducts()
    {
        var products = await _adminDashboardService.GetProductsAsync();
        return Ok(products);
    }

    [Authorize(Policy = AdminPolicies.Products)]
    [HttpGet("products/{id}")]
    public async Task<ActionResult> GetProductDetail(int id)
    {
        var product = await _adminDashboardService.GetProductDetailAsync(id);
        if (product == null)
            return NotFound(new { result = 0, messages = new[] { "Product not found." } });

        return Ok(product);
    }

    [Authorize(Policy = AdminPolicies.Products)]
    [HttpPost("products")]
    public async Task<ActionResult> CreateProduct([FromBody] CreateProductRequest request)
    {
        var userId = CurrentAdminUserId;
        var result = await _adminDashboardService.CreateProductAsync(request, userId);
        return Ok(result);
    }

    [Authorize(Policy = AdminPolicies.Products)]
    [HttpPut("products/{id}")]
    public async Task<ActionResult> UpdateProduct(int id, [FromBody] CreateProductRequest request)
    {
        var userId = CurrentAdminUserId;
        var result = await _adminDashboardService.UpdateProductAsync(id, request, userId);
        return Ok(result);
    }

    [Authorize(Policy = AdminPolicies.Products)]
    [HttpPut("products/{id}/deactivate")]
    public async Task<ActionResult> DeactivateProduct(int id)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.DeactivateProductAsync(id, userId);
        return Ok(result);
    }

    [Authorize(Policy = AdminPolicies.Products)]
    [HttpGet("product-options")]
    public async Task<ActionResult> GetProductFormOptions()
    {
        var options = await _adminDashboardService.GetProductFormOptionsAsync();
        return Ok(options);
    }

    [Authorize(Policy = AdminPolicies.Products)]
    [HttpPost("upload-product-image")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult> UploadProductImage([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { result = 0, messages = new[] { "No file was uploaded." } });

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { result = 0, messages = new[] { "Only JPG, PNG, WEBP and GIF images are allowed." } });

        const long maxBytes = 5 * 1024 * 1024; // 5 MB
        if (file.Length > maxBytes)
            return BadRequest(new { result = 0, messages = new[] { "Image size must be 5 MB or less." } });

        var uploadRoot = Path.Combine(
            _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"),
            "images", "products");
        Directory.CreateDirectory(uploadRoot);

        var fileName = $"prod_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(uploadRoot, fileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(new
        {
            result = 1,
            messages = new[] { "Image uploaded successfully." },
            fileName = fileName,
            url = $"/images/products/{fileName}"
        });
    }

    #endregion

    #region Orders

    [Authorize(Policy = AdminPolicies.Orders)]
    [HttpGet("orders")]
    public async Task<ActionResult> GetOrders()
    {
        var orders = await _adminDashboardService.GetOrdersAsync();
        return Ok(orders);
    }

    [Authorize(Policy = AdminPolicies.Orders)]
    [HttpGet("orders/{id}")]
    public async Task<ActionResult> GetOrderDetail(long id)
    {
        var order = await _adminDashboardService.GetOrderDetailAsync(id);
        if (order == null)
            return NotFound(new { result = 0, messages = new[] { "Order not found." } });

        return Ok(order);
    }

    [Authorize(Policy = AdminPolicies.Orders)]
    [HttpGet("order-statuses")]
    public async Task<ActionResult> GetOrderStatuses()
    {
        var statuses = await _adminDashboardService.GetOrderStatusesAsync();
        return Ok(statuses);
    }

    /// <summary>
    /// Moves an order to the next step of its lifecycle. Moving it to Shipped also records its parcel in
    /// the same action, when there is one: the body carries the provider the parcel was booked with
    /// ('provider'), the consignment number it gave ('awbNumber') and what it billed for the parcel
    /// ('freightCharge', optional - the books' own figure, never shown to the customer), and they are
    /// recorded against the order, so every later courier call goes through that provider's own adapter
    /// (see HC.Business.Shipping). None of them is required: an order dispatched without a courier
    /// (handed over in person, or given to a delivery service this shop has no integration with) simply
    /// moves to Shipped, with no parcel recorded and nothing to track. An unknown provider, a freight
    /// charge with no consignment number to keep it on, or a consignment the provider would not accept,
    /// is refused and nothing is saved.
    /// </summary>
    [Authorize(Policy = AdminPolicies.Orders)]
    [HttpPost("orders/{id}/status")]
    public async Task<ActionResult> UpdateOrderStatus(long id, [FromBody] AdminOrderStatusUpdateRequest request)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.UpdateOrderStatusAsync(id, request, userId);
        return Ok(result);
    }

    /// <summary>
    /// Approves the refund a cancelled paid order is waiting for: this is what sends the money back
    /// through Razorpay. Only the refund still recorded as owed on the order can be approved, so a
    /// second call cannot refund twice; when the app cannot send it (no captured payment on record, or
    /// Razorpay refuses), the message says so and the team refunds it in the Razorpay dashboard and
    /// records it with 'mark-refunded'.
    /// </summary>
    [Authorize(Policy = AdminPolicies.Orders)]
    [HttpPost("orders/{id}/approve-refund")]
    public async Task<ActionResult> ApproveOrderRefund(long id)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.ApproveOrderRefundAsync(id, userId);
        return Ok(result);
    }

    /// <summary>
    /// Records that the refund went out in the Razorpay dashboard instead of through the app, so the
    /// order stops being flagged as owing the money.
    /// </summary>
    [Authorize(Policy = AdminPolicies.Orders)]
    [HttpPost("orders/{id}/mark-refunded")]
    public async Task<ActionResult> MarkOrderRefunded(long id, [FromBody] AdminOrderRefundRequest? request)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.MarkOrderRefundedAsync(id, request?.Comment, userId);
        return Ok(result);
    }

    /// <summary>
    /// The shipping providers this shop is set up with, and whether each is actually configured - the
    /// provider list of the order screen's Shipment card. It comes from the provider registry
    /// (see HC.Business.Shipping), so the list is what is really wired up and nothing has to be
    /// hard-coded in the admin app.
    /// </summary>
    [Authorize(Policy = AdminPolicies.Orders)]
    [HttpGet("shipment-providers")]
    public async Task<ActionResult> GetShipmentProviders()
    {
        var providers = await _adminDashboardService.GetShipmentProvidersAsync();
        return Ok(providers);
    }

    /// <summary>
    /// Records the parcel the shop team booked in the provider's own panel: the AWB, plus the courier
    /// and tracking link when they were given them, and what the courier billed for it (the shop's own
    /// figure, kept for the books and never shown to the customer; blank leaves what is recorded).
    /// It never moves the order - booking a parcel is not dispatching it, and the order follows only the
    /// courier's own reports ('track-shipment' below). The AWB is written to the order's history, so the
    /// timeline says who recorded the parcel and when.
    /// </summary>
    [Authorize(Policy = AdminPolicies.Orders)]
    [HttpPost("orders/{id}/shipment")]
    public async Task<ActionResult> SaveOrderShipment(long id, [FromBody] SaveOrderShipmentRequest request)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.SaveOrderShipmentAsync(id, request, userId);
        return Ok(result);
    }

    /// <summary>
    /// 'Track now' on the Shipment card: asks the courier about this order's parcel right now and writes
    /// down what it said, moving the order - and its units - along when the courier's own status allows
    /// it. Unlike the customer-facing pull this is never throttled, because the shop team asked on
    /// purpose; a courier that cannot be reached leaves the order as it was, with the reason in the
    /// answer.
    /// </summary>
    [Authorize(Policy = AdminPolicies.Orders)]
    [HttpPost("orders/{id}/track-shipment")]
    public async Task<ActionResult> TrackOrderShipment(long id)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.TrackOrderShipmentAsync(id, userId);
        return Ok(result);
    }

    /// <summary>
    /// The shop team's answer to a return - 'Approve' or 'Refuse' on the order's Return card. The body says
    /// which answer it is ('approved') and carries the note the customer is told ('comment', required:
    /// 'refused' with no reason is nothing a customer can act on). Approving means the parcel is coming back,
    /// so the pickup is booked from the same card; the order itself only becomes 'Returned' - its units back
    /// on the shelf, its money owed back - when the return is closed with the parcel in the shop. An ask that
    /// has already been answered is refused here, so one return can never be approved twice.
    /// </summary>
    [Authorize(Policy = AdminPolicies.Orders)]
    [HttpPost("orders/{id}/returns/decision")]
    public async Task<ActionResult> DecideOrderReturn(long id, [FromBody] AdminOrderReturnDecisionRequest request)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.DecideOrderReturnAsync(id, request, userId);
        return Ok(result);
    }

    /// <summary>
    /// 'Parcel received' on the Return card: the parcel is physically back with the shop, so the return becomes
    /// 'Received' and its units come off the delivery pools - in the shop, and off sale until the return is closed.
    /// The order and the money are left exactly as they are: the order only becomes 'Returned' (and the refund only
    /// asked for) when the return is closed, which is the next step of the same card. The optional note is kept on
    /// the order's history, which the customer reads in 'My Orders'.
    /// </summary>
    [Authorize(Policy = AdminPolicies.Orders)]
    [HttpPost("orders/{id}/returns/received")]
    public async Task<ActionResult> MarkOrderReturnReceived(long id, [FromBody] AdminOrderReturnReceivedRequest? request)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.MarkOrderReturnReceivedAsync(
            id, request ?? new AdminOrderReturnReceivedRequest(), userId);

        return Ok(result);
    }

    /// <summary>
    /// The inspection of a parcel that came back: every SKU in 'damagedSkus' is a unit the shop team found broken,
    /// and each one is written off - out of every sellable pool for good, so closing the return cannot put it back
    /// on sale. The units are the order's own (a SKU belonging to another order is ignored), a request with no
    /// units is still recorded ("we looked and found nothing"), and 'comment' is what the shop team wrote about the
    /// parcel.
    /// </summary>
    [Authorize(Policy = AdminPolicies.Orders)]
    [HttpPost("orders/{id}/returns/inspection")]
    public async Task<ActionResult> MarkOrderReturnUnitsDamaged(long id, [FromBody] AdminOrderReturnInspectionRequest? request)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.MarkOrderReturnUnitsDamagedAsync(
            id, request ?? new AdminOrderReturnInspectionRequest(), userId);

        return Ok(result);
    }

    /// <summary>
    /// 'Close return' - the step that settles a return: the order becomes 'Returned', every unit the return brought
    /// back goes on sale again (the ones the inspection wrote off stay written off) and the refund of what the
    /// customer paid is asked for. Sending that money is a separate, deliberate step on the Refund card ('Approve
    /// refund'), so nothing leaves for the gateway from here. Only a return whose parcel is back with the shop can
    /// be closed, so a refund can never be given for something the customer still has.
    /// </summary>
    [Authorize(Policy = AdminPolicies.Orders)]
    [HttpPost("orders/{id}/returns/close")]
    public async Task<ActionResult> CloseOrderReturn(long id, [FromBody] AdminOrderReturnCloseRequest? request)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.CloseOrderReturnAsync(
            id, request ?? new AdminOrderReturnCloseRequest(), userId);

        return Ok(result);
    }

    #endregion

    #region Customers

    [Authorize(Policy = AdminPolicies.Customers)]
    [HttpGet("customers")]
    public async Task<ActionResult> GetCustomers([FromQuery] string? search)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchResults = await _adminDashboardService.SearchCustomersAsync(search);
            return Ok(searchResults);
        }

        var customers = await _adminDashboardService.GetCustomersAsync();
        return Ok(customers);
    }

    [Authorize(Policy = AdminPolicies.Customers)]
    [HttpGet("customers/{id}")]
    public async Task<ActionResult> GetCustomerDetail(long id)
    {
        var customer = await _adminDashboardService.GetCustomerDetailAsync(id);
        if (customer == null)
            return NotFound(new { result = 0, messages = new[] { "Customer not found." } });

        return Ok(customer);
    }

    [Authorize(Policy = AdminPolicies.Customers)]
    [HttpPut("customers/{id}/status")]
    public async Task<ActionResult> UpdateCustomerStatus(long id, [FromBody] UpdateCustomerStatusRequest request)
    {
        var result = await _adminDashboardService.UpdateCustomerStatusAsync(id, request.CustomerStatusId);
        return Ok(result);
    }

    #endregion


    #region Partners

    [Authorize(Policy = AdminPolicies.Partners)]
    [HttpGet("partners")]
    public async Task<ActionResult> GetPartners()
    {
        var partners = await _adminDashboardService.GetPartnersAsync();
        return Ok(partners);
    }

    [Authorize(Policy = AdminPolicies.Partners)]
    [HttpGet("partners/{id}")]
    public async Task<ActionResult> GetPartnerDetail(int id)
    {
        var partner = await _adminDashboardService.GetPartnerDetailAsync(id);
        if (partner == null)
            return NotFound(new { result = 0, messages = new[] { "Partner not found." } });

        return Ok(partner);
    }

    [Authorize(Policy = AdminPolicies.Partners)]
    [HttpGet("partner-statuses")]
    public async Task<ActionResult> GetPartnerStatuses()
    {
        var statuses = await _adminDashboardService.GetPartnerStatusesAsync();
        return Ok(statuses);
    }

    [Authorize(Policy = AdminPolicies.Partners)]
    [HttpPost("partners")]
    public async Task<ActionResult> CreatePartner([FromBody] PartnerFormRequest request)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.CreatePartnerAsync(request, userId);
        return Ok(result);
    }

    [Authorize(Policy = AdminPolicies.Partners)]
    [HttpPut("partners/{id}")]
    public async Task<ActionResult> UpdatePartner(int id, [FromBody] PartnerFormRequest request)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.UpdatePartnerAsync(id, request, userId);
        return Ok(result);
    }

    #endregion

    #region Vendors

    [Authorize(Policy = AdminPolicies.Vendors)]
    [HttpGet("vendors")]
    public async Task<ActionResult> GetVendors()
    {
        var vendors = await _adminDashboardService.GetVendorsAsync();
        return Ok(vendors);
    }

    [Authorize(Policy = AdminPolicies.Vendors)]
    [HttpGet("vendors/{id}")]
    public async Task<ActionResult> GetVendorDetail(short id)
    {
        var vendor = await _adminDashboardService.GetVendorDetailAsync(id);
        if (vendor == null)
            return NotFound(new { result = 0, messages = new[] { "Vendor not found." } });

        return Ok(vendor);
    }

    [Authorize(Policy = AdminPolicies.Vendors)]
    [HttpPost("vendors")]
    public async Task<ActionResult> CreateVendor([FromBody] VendorFormRequest request)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.CreateVendorAsync(request, userId);
        return Ok(result);
    }

    [Authorize(Policy = AdminPolicies.Vendors)]
    [HttpPut("vendors/{id}")]
    public async Task<ActionResult> UpdateVendor(short id, [FromBody] VendorFormRequest request)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.UpdateVendorAsync(id, request, userId);
        return Ok(result);
    }

    #endregion

    #region Purchases

    [Authorize(Policy = AdminPolicies.Purchases)]
    [HttpGet("purchases")]
    public async Task<ActionResult> GetPurchases()
    {
        var purchases = await _adminDashboardService.GetPurchasesAsync();
        return Ok(purchases);
    }

    [Authorize(Policy = AdminPolicies.Purchases)]
    [HttpGet("purchases/{id}")]
    public async Task<ActionResult> GetPurchaseDetail(long id)
    {
        var purchase = await _adminDashboardService.GetPurchaseDetailAsync(id);
        if (purchase == null)
            return NotFound(new { result = 0, messages = new[] { "Purchase not found." } });

        return Ok(purchase);
    }

    [Authorize(Policy = AdminPolicies.Purchases)]
    [HttpGet("purchasers")]
    public async Task<ActionResult> GetPurchasers()
    {
        var purchasers = await _adminDashboardService.GetPurchasersAsync();
        return Ok(purchasers);
    }

    [Authorize(Policy = AdminPolicies.Purchases)]
    [HttpPost("purchases")]
    public async Task<ActionResult> CreatePurchase([FromBody] AdminPurchaseCreateRequest request)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.CreatePurchaseAsync(request, userId);
        return Ok(result);
    }

    [Authorize(Policy = AdminPolicies.Purchases)]
    [HttpGet("purchases/{id}/statuses")]
    public async Task<ActionResult> GetPurchaseStatuses(long id)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var statuses = await _adminDashboardService.GetPurchaseStatusesAsync(id, userId);
        return Ok(statuses);
    }

    [Authorize(Policy = AdminPolicies.Purchases)]
    [HttpPost("purchases/{id}/status")]
    public async Task<ActionResult> UpdatePurchaseStatus(long id, [FromBody] AdminPurchaseStatusUpdateRequest request)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.UpdatePurchaseStatusAsync(id, request, userId);
        return Ok(result);
    }

    [Authorize(Policy = AdminPolicies.Purchases)]
    [HttpPut("purchases/{id}")]
    public async Task<ActionResult> UpdatePurchase(long id, [FromBody] AdminPurchaseUpdateRequest request)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.UpdatePurchaseAsync(id, request, userId);
        return Ok(result);
    }

    [Authorize(Policy = AdminPolicies.Purchases)]
    [HttpPut("purchases/{id}/items")]
    public async Task<ActionResult> SavePurchaseItems(long id, [FromBody] AdminPurchaseItemsRequest request)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.SavePurchaseItemsAsync(id, request, userId);
        return Ok(result);
    }

    [Authorize(Policy = AdminPolicies.Purchases)]
    [HttpPost("purchases/{id}/comments")]
    public async Task<ActionResult> AddPurchaseComment(long id, [FromBody] AdminPurchaseCommentRequest request)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.AddPurchaseCommentAsync(id, request, userId);
        return Ok(result);
    }

    #endregion

    #region Admin Users

    [Authorize(Policy = AdminPolicies.AdminUsers)]
    [HttpGet("users")]
    public async Task<ActionResult> GetAdminUsers()
    {
        var users = await _adminDashboardService.GetAdminUsersAsync();
        return Ok(users);
    }

    [Authorize(Policy = AdminPolicies.AdminUsers)]
    [HttpGet("users/{id}")]
    public async Task<ActionResult> GetAdminUser(long id)
    {
        var user = await _adminDashboardService.GetAdminUserAsync(id);
        if (user == null)
            return NotFound(new { result = 0, messages = new[] { "Admin user not found." } });

        return Ok(user);
    }

    [Authorize(Policy = AdminPolicies.AdminUsers)]
    [HttpGet("roles")]
    public async Task<ActionResult> GetAdminRoles()
    {
        var roles = await _adminDashboardService.GetAdminRolesAsync();
        return Ok(roles);
    }

    [Authorize(Policy = AdminPolicies.AdminUsers)]
    [HttpPost("users")]
    public async Task<ActionResult> CreateAdminUser([FromBody] AdminUserCreateRequest request)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.CreateAdminUserAsync(request, userId);
        return Ok(result);
    }

    [Authorize(Policy = AdminPolicies.AdminUsers)]
    [HttpPut("users/{id}")]
    public async Task<ActionResult> UpdateAdminUser(long id, [FromBody] AdminUserUpdateRequest request)
    {
        var userId = CurrentAdminUserId;
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.UpdateAdminUserAsync(id, request, userId);
        return Ok(result);
    }

    #endregion

    #region Categories

    /// <summary>Category tree: used by the product form and by the admin user form.</summary>
    [Authorize(Policy = AdminPolicies.ProductsOrAdminUsers)]
    [HttpGet("categories")]
    public async Task<ActionResult> GetCategories()
    {
        var categories = await _adminDashboardService.GetCategoryTreeAsync();
        return Ok(categories);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Id of the signed-in admin, read from the validated token. Mutating actions record this value
    /// as the author of the change, so it can no longer be supplied (and therefore forged) by the caller.
    /// </summary>
    private long CurrentAdminUserId => AdminJwtTokenService.GetUserId(User);

    private string GetClientIp()
    {
        // Try to get from X-Forwarded-For header
        if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var ip = forwardedFor.FirstOrDefault();
            if (!string.IsNullOrEmpty(ip))
                return ip.Split(',').First().Trim();
        }

        // Fall back to remote IP
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
    }

    #endregion
}

public class AdminMenuRequest
{
    public short? RoleId { get; set; }
}
