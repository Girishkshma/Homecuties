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
        var user = _adminAuthService.ValidateToken(request.JWT);
        if (user == null)
            return Unauthorized(new { Result = 0, Messages = new[] { "Invalid or expired token." } });

        return Ok(new { Result = 1, User = user });
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
            return NotFound(new { Result = 0, Messages = new[] { "Product not found." } });

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
            return NotFound(new { Result = 0, Messages = new[] { "Order not found." } });

        return Ok(order);
    }

    #endregion

    #region Customers

    [HttpGet("customers")]
    public async Task<ActionResult> GetCustomers()
    {
        var customers = await _adminDashboardService.GetCustomersAsync();
        return Ok(customers);
    }

    #endregion

    #region Partners

    [HttpGet("partners")]
    public async Task<ActionResult> GetPartners()
    {
        var partners = await _adminDashboardService.GetPartnersAsync();
        return Ok(partners);
    }

    #endregion

    #region Vendors

    [HttpGet("vendors")]
    public async Task<ActionResult> GetVendors()
    {
        var vendors = await _adminDashboardService.GetVendorsAsync();
        return Ok(vendors);
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
}

public class AdminMenuRequest
{
    public short? RoleId { get; set; }
}
