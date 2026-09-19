using HC.Business;
using HC.Business.Dtos;
using HC.Business.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Text;

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
        var customerId = GetTokenCustomerId();
        if (customerId == null)
            return Unauthorized(new { Result = 0, Messages = new[] { "Please sign in to continue." } });

        // The order is always verified against the signed-in customer, never on the body alone.
        var result = await _orderService.VerifyPaymentAsync(request, customerId.Value);
        return Ok(result);
    }

    /// <summary>
    /// Razorpay webhook: configure this URL in the Razorpay Dashboard (Account &amp; Settings →
    /// Webhooks) and set 'Razorpay:WebhookSecret'. The body is read RAW because the signature is
    /// calculated over the exact bytes Razorpay sent - parsing it first would break the check.
    /// </summary>
    [HttpPost("Webhook")]
    public async Task<ActionResult> Webhook()
    {
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            rawBody = await reader.ReadToEndAsync();
        }

        var signature = Request.Headers["X-Razorpay-Signature"].ToString();
        var result = await _orderService.HandlePaymentWebhookAsync(rawBody, signature);

        // Deliberate: only a bad signature is rejected (400) so Razorpay retries a genuine
        // delivery; anything we handled or deliberately ignored answers 200 to stop retries.
        var invalidSignature = result.Result == 0 &&
            result.Messages.Any(message => message.Contains("signature", StringComparison.OrdinalIgnoreCase));

        return invalidSignature ? BadRequest(result) : Ok(result);
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
