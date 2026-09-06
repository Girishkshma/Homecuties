using System.IO;
using HC.Business;
using HC.Business.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace HC.Services.Controllers;

[ApiController]
[Route("api/admin")]
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

    [HttpPost("login")]
    public async Task<ActionResult> Login([FromBody] AdminLoginRequest request)
    {
        var result = await _adminAuthService.LoginAsync(request.LoginId, request.Password);
        return Ok(result);
    }

    [HttpPost("validate-token")]
    public ActionResult ValidateToken([FromBody] AdminValidateJwtRequest request)
    {
        var ipAddress = string.IsNullOrEmpty(request.IPAddress) ? GetClientIp() : request.IPAddress;
        var user = _adminAuthService.ValidateToken(request.JWT, ipAddress);
        if (user == null)
            return Unauthorized(new { result = 0, messages = new[] { "Invalid or expired token." } });

        return Ok(new { result = 1, user = user });
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult> ForgotPassword([FromBody] AdminForgotPasswordRequest request)
    {
        var result = await _adminAuthService.ForgotPasswordAsync(request.LoginId);
        return Ok(result);
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPassword([FromBody] AdminResetPasswordRequest request)
    {
        var result = await _adminAuthService.ResetPasswordAsync(request.Token, request.NewPassword);
        return Ok(result);
    }

    [HttpPost("menus")]
    public async Task<ActionResult> GetMenus([FromBody] AdminMenuRequest request)
    {
        List<AdminMenuDto> menus;
        if (request.RoleId.HasValue)
            menus = await _adminAuthService.GetMenusByRoleAsync(request.RoleId.Value);
        else
            menus = await _adminAuthService.GetAllMenusAsync();

        return Ok(menus);
    }

    #endregion

    #region Dashboard

    [HttpGet("dashboard/stats")]
    public async Task<ActionResult> GetDashboardStats()
    {
        var stats = await _adminDashboardService.GetDashboardStatsAsync();
        return Ok(stats);
    }

    #endregion

    #region Products

    [HttpGet("products")]
    public async Task<ActionResult> GetProducts()
    {
        var products = await _adminDashboardService.GetProductsAsync();
        return Ok(products);
    }

    [HttpGet("products/{id}")]
    public async Task<ActionResult> GetProductDetail(int id)
    {
        var product = await _adminDashboardService.GetProductDetailAsync(id);
        if (product == null)
            return NotFound(new { result = 0, messages = new[] { "Product not found." } });

        return Ok(product);
    }

    [HttpPost("products")]
    public async Task<ActionResult> CreateProduct([FromBody] CreateProductRequest request, [FromQuery] long userId)
    {
        var result = await _adminDashboardService.CreateProductAsync(request, userId);
        return Ok(result);
    }

    [HttpPut("products/{id}")]
    public async Task<ActionResult> UpdateProduct(int id, [FromBody] CreateProductRequest request, [FromQuery] long userId)
    {
        var result = await _adminDashboardService.UpdateProductAsync(id, request, userId);
        return Ok(result);
    }

    [HttpPut("products/{id}/deactivate")]
    public async Task<ActionResult> DeactivateProduct(int id, [FromQuery] long userId)
    {
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.DeactivateProductAsync(id, userId);
        return Ok(result);
    }

    [HttpGet("product-options")]
    public async Task<ActionResult> GetProductFormOptions()
    {
        var options = await _adminDashboardService.GetProductFormOptionsAsync();
        return Ok(options);
    }

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

    [HttpGet("orders")]
    public async Task<ActionResult> GetOrders()
    {
        var orders = await _adminDashboardService.GetOrdersAsync();
        return Ok(orders);
    }

    [HttpGet("orders/{id}")]
    public async Task<ActionResult> GetOrderDetail(long id)
    {
        var order = await _adminDashboardService.GetOrderDetailAsync(id);
        if (order == null)
            return NotFound(new { result = 0, messages = new[] { "Order not found." } });

        return Ok(order);
    }

    #endregion

    #region Customers

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

    [HttpGet("customers/{id}")]
    public async Task<ActionResult> GetCustomerDetail(long id)
    {
        var customer = await _adminDashboardService.GetCustomerDetailAsync(id);
        if (customer == null)
            return NotFound(new { result = 0, messages = new[] { "Customer not found." } });

        return Ok(customer);
    }

    [HttpPut("customers/{id}/status")]
    public async Task<ActionResult> UpdateCustomerStatus(long id, [FromBody] UpdateCustomerStatusRequest request)
    {
        var result = await _adminDashboardService.UpdateCustomerStatusAsync(id, request.CustomerStatusId);
        return Ok(result);
    }

    #endregion


    #region Partners

    [HttpGet("partners")]
    public async Task<ActionResult> GetPartners()
    {
        var partners = await _adminDashboardService.GetPartnersAsync();
        return Ok(partners);
    }

    [HttpGet("partners/{id}")]
    public async Task<ActionResult> GetPartnerDetail(int id)
    {
        var partner = await _adminDashboardService.GetPartnerDetailAsync(id);
        if (partner == null)
            return NotFound(new { result = 0, messages = new[] { "Partner not found." } });

        return Ok(partner);
    }

    [HttpGet("partner-statuses")]
    public async Task<ActionResult> GetPartnerStatuses()
    {
        var statuses = await _adminDashboardService.GetPartnerStatusesAsync();
        return Ok(statuses);
    }

    [HttpPost("partners")]
    public async Task<ActionResult> CreatePartner([FromBody] PartnerFormRequest request, [FromQuery] long userId)
    {
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.CreatePartnerAsync(request, userId);
        return Ok(result);
    }

    [HttpPut("partners/{id}")]
    public async Task<ActionResult> UpdatePartner(int id, [FromBody] PartnerFormRequest request, [FromQuery] long userId)
    {
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.UpdatePartnerAsync(id, request, userId);
        return Ok(result);
    }

    #endregion

    #region Vendors

    [HttpGet("vendors")]
    public async Task<ActionResult> GetVendors()
    {
        var vendors = await _adminDashboardService.GetVendorsAsync();
        return Ok(vendors);
    }

    [HttpGet("vendors/{id}")]
    public async Task<ActionResult> GetVendorDetail(short id)
    {
        var vendor = await _adminDashboardService.GetVendorDetailAsync(id);
        if (vendor == null)
            return NotFound(new { result = 0, messages = new[] { "Vendor not found." } });

        return Ok(vendor);
    }

    [HttpPost("vendors")]
    public async Task<ActionResult> CreateVendor([FromBody] VendorFormRequest request, [FromQuery] long userId)
    {
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.CreateVendorAsync(request, userId);
        return Ok(result);
    }

    [HttpPut("vendors/{id}")]
    public async Task<ActionResult> UpdateVendor(short id, [FromBody] VendorFormRequest request, [FromQuery] long userId)
    {
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.UpdateVendorAsync(id, request, userId);
        return Ok(result);
    }

    #endregion

    #region Purchases

    [HttpGet("purchases")]
    public async Task<ActionResult> GetPurchases()
    {
        var purchases = await _adminDashboardService.GetPurchasesAsync();
        return Ok(purchases);
    }

    [HttpGet("purchases/{id}")]
    public async Task<ActionResult> GetPurchaseDetail(long id)
    {
        var purchase = await _adminDashboardService.GetPurchaseDetailAsync(id);
        if (purchase == null)
            return NotFound(new { result = 0, messages = new[] { "Purchase not found." } });

        return Ok(purchase);
    }

    #endregion

    #region Admin Users

    [HttpGet("users")]
    public async Task<ActionResult> GetAdminUsers()
    {
        var users = await _adminDashboardService.GetAdminUsersAsync();
        return Ok(users);
    }

    [HttpGet("users/{id}")]
    public async Task<ActionResult> GetAdminUser(long id)
    {
        var user = await _adminDashboardService.GetAdminUserAsync(id);
        if (user == null)
            return NotFound(new { result = 0, messages = new[] { "Admin user not found." } });

        return Ok(user);
    }

    [HttpGet("roles")]
    public async Task<ActionResult> GetAdminRoles()
    {
        var roles = await _adminDashboardService.GetAdminRolesAsync();
        return Ok(roles);
    }

    [HttpPost("users")]
    public async Task<ActionResult> CreateAdminUser([FromBody] AdminUserCreateRequest request, [FromQuery] long userId)
    {
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.CreateAdminUserAsync(request, userId);
        return Ok(result);
    }

    [HttpPut("users/{id}")]
    public async Task<ActionResult> UpdateAdminUser(long id, [FromBody] AdminUserUpdateRequest request, [FromQuery] long userId)
    {
        if (userId <= 0)
            return BadRequest(new { result = 0, messages = new[] { "Current user id is required." } });

        var result = await _adminDashboardService.UpdateAdminUserAsync(id, request, userId);
        return Ok(result);
    }

    #endregion

    #region Categories

    [HttpGet("categories")]
    public async Task<ActionResult> GetCategories()
    {
        var categories = await _adminDashboardService.GetCategoryTreeAsync();
        return Ok(categories);
    }

    #endregion

    #region Helpers

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
