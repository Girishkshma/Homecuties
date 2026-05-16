using HC.Business;
using HC.Business.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace HC.Services.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminAuthService _adminAuthService;
    private readonly IAdminDashboardService _adminDashboardService;

    public AdminController(IAdminAuthService adminAuthService, IAdminDashboardService adminDashboardService)
    {
        _adminAuthService = adminAuthService;
        _adminDashboardService = adminDashboardService;
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

    [HttpDelete("products/{id}")]
    public async Task<ActionResult> DeleteProduct(int id)
    {
        var result = await _adminDashboardService.DeleteProductAsync(id);
        return Ok(result);
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

    #endregion

    #region Admin Users

    [HttpGet("users")]
    public async Task<ActionResult> GetAdminUsers()
    {
        var users = await _adminDashboardService.GetAdminUsersAsync();
        return Ok(users);
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
