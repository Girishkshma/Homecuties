using HC.Business;
using HC.Business.Dtos;
using HC.Business.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace HC.Services.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private const string DefaultJwtSecret = "123456789abcdefgh";

    private readonly IOrderService _orderService;
    private readonly string _jwtSecret;

    public OrderController(IOrderService orderService, IConfiguration configuration)
    {
        _orderService = orderService;
        _jwtSecret = configuration["JWTSecret"] ?? DefaultJwtSecret;
    }

    [HttpPost("CreateOrder")]
    public async Task<ActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var customerId = GetTokenCustomerId();
        if (customerId == null)
            return Unauthorized(new { Result = 0, Messages = new[] { "Please sign in to place an order." } });

        // Never trust the id in the body - the order always belongs to the signed-in customer.
        request.CustomerID = customerId.Value;
        request.IsGuest = false;

        var result = await _orderService.CreateOrderAsync(request);
        return Ok(result);
    }

    [HttpPost("VerifyPayment")]
    public async Task<ActionResult> VerifyPayment([FromBody] VerifyPaymentRequest request)
    {
        if (GetTokenCustomerId() == null)
            return Unauthorized(new { Result = 0, Messages = new[] { "Please sign in to continue." } });

        var result = await _orderService.VerifyPaymentAsync(request);
        return Ok(result);
    }

    [HttpPost("GetOrders")]
    public async Task<ActionResult> GetOrders([FromBody] CartRequest request)
    {
        var customerId = GetTokenCustomerId();
        if (customerId == null)
            return Unauthorized(new { Result = 0, Messages = new[] { "Please sign in to view your orders." } });

        var result = await _orderService.GetOrdersAsync(customerId.Value, false);
        return Ok(result);
    }

    /// <summary>Reads "Authorization: Bearer &lt;token&gt;" and returns the signed-in customer id when it is valid.</summary>
    private long? GetTokenCustomerId()
    {
        var header = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        return CustomerTokenService.Validate(header["Bearer ".Length..].Trim(), _jwtSecret);
    }
}
