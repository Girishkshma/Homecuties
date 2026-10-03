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

    /// <summary>
    /// The throttled courier pull 'My Orders' makes when it opens, and again when the customer taps
    /// 'Track parcel': the live parcels of the signed-in customer are looked up with their provider once
    /// more and answered fresh. A parcel checked a moment ago - or one the courier is done with - is left
    /// alone, so opening the page is not itself a courier call, and a courier that cannot be reached is a
    /// sentence in the answer rather than an error on the page.
    /// </summary>
    [HttpPost("RefreshShipments")]
    public async Task<ActionResult> RefreshShipments()
    {
        var customerId = GetTokenCustomerId();
        if (customerId == null)
            return Unauthorized(new { Result = 0, Messages = new[] { "Please sign in to track your parcels." } });

        var result = await _orderService.RefreshShipmentsAsync(customerId.Value);
        return Ok(result);
    }

    /// <summary>
    /// Cancels an unpaid order from 'My Orders'. The order is always matched against the signed-in
    /// customer, so a guessed id cannot cancel somebody else's order.
    /// </summary>
    [HttpPost("CancelOrder")]
    public async Task<ActionResult> CancelOrder([FromBody] OrderActionRequest request)
    {
        var customerId = GetTokenCustomerId();
        if (customerId == null)
            return Unauthorized(new { Result = 0, Messages = new[] { "Please sign in to cancel an order." } });

        var result = await _orderService.CancelOrderAsync(customerId.Value, request.OrderId);
        return Ok(result);
    }

    /// <summary>
    /// Re-checks an order's payment with Razorpay and confirms the order when the money was captured.
    /// Used by the checkout page (and 'My Orders') when the browser never learned the payment result,
    /// which is what used to leave the page spinning forever.
    /// </summary>
    [HttpPost("SyncPayment")]
    public async Task<ActionResult> SyncPayment([FromBody] OrderActionRequest request)
    {
        var customerId = GetTokenCustomerId();
        if (customerId == null)
            return Unauthorized(new { Result = 0, Messages = new[] { "Please sign in to check a payment." } });

        var result = await _orderService.SyncOrderPaymentAsync(customerId.Value, request.OrderId);
        return Ok(result);
    }

    /// <summary>
    /// Starts a fresh payment attempt for an order that is still waiting for its money - the 'Pay now'
    /// button in 'My Orders'. The order is matched against the signed-in customer and the amount comes
    /// from the order itself, so a retry can never pay less than the order costs.
    /// </summary>
    [HttpPost("RetryPayment")]
    public async Task<ActionResult> RetryPayment([FromBody] OrderActionRequest request)
    {
        var customerId = GetTokenCustomerId();
        if (customerId == null)
            return Unauthorized(new { Result = 0, Messages = new[] { "Please sign in to pay for an order." } });

        var result = await _orderService.RetryOrderPaymentAsync(customerId.Value, request.OrderId);
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
