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
