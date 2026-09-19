// ============================================================
// OrderService.Payments.cs
// Partial class: OrderService - Payments operations
// ============================================================

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HC.Business;

public partial class OrderService : IOrderService
{
    // Razorpay order/payment statuses that matter here.
    private const string RazorpayStatusAuthorized = "authorized";
    private const string RazorpayStatusCaptured = "captured";

    // Orders.OrderStatusID: 1 = Pending (set when the order is placed), 2 = Confirmed.
    private const short OrderStatusPending = 1;
    private const short OrderStatusConfirmed = 2;

    /// <summary>
    /// Shared client for Razorpay's REST API. The Authorization header is set per request (never on
    /// the shared instance) so concurrent orders cannot borrow each other's credentials.
    /// </summary>
    private static readonly HttpClient RazorpayHttp = new();

    private string RazorpayAuthHeader =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_razorpayKeyId}:{_razorpayKeySecret}"));

    /// <summary>
    /// Confirms a paid order.
    ///
    /// Per Razorpay's go-live checklist the signature only proves the payment was AUTHORISED, and an
    /// uncaptured payment is auto-refunded - so the order is confirmed only once Razorpay reports the
    /// payment as captured, capturing it here first when needed. The amount and the order linkage are
    /// re-checked against Razorpay so a valid signature cannot be replayed against a different (or
    /// cheaper) order.
    /// </summary>
    public async Task<ResultDto> VerifyPaymentAsync(VerifyPaymentRequest request, long customerId)
    {
        if (!IsRazorpayConfigured)
        {
            _logger.LogError("Payment verification failed: Razorpay credentials are not configured.");
            return PaymentError("Payment verification is not available right now. Please contact support.");
        }

        // 1. Signature: HMAC-SHA256("<razorpay_order_id>|<razorpay_payment_id>", key_secret).
        var expectedSignature = GenerateRazorpaySignature(
            request.RazorpayOrderId,
            request.RazorpayPaymentId,
            _razorpayKeySecret);

        if (!FixedTimeEquals(expectedSignature, request.RazorpaySignature))
        {
            _logger.LogWarning("Payment verification failed: signature mismatch for order {OrderId}.", request.OrderId);
            return PaymentError("Payment verification failed - invalid signature.");
        }

        // 2. The order must exist, belong to the signed-in customer and still be pending.
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == request.OrderId);
        if (order == null || order.CustomerId != customerId)
        {
            _logger.LogWarning(
                "Payment verification failed: order {OrderId} not found for customer {CustomerId}.",
                request.OrderId, customerId);

            return PaymentError("Order not found.");
        }

        if (order.OrderStatusId == OrderStatusConfirmed)
            return new ResultDto { Result = 1, Messages = new[] { "Payment already verified." } };

        if (order.OrderStatusId != OrderStatusPending)
            return PaymentError("This order can no longer be paid.");

        // 3. The Razorpay order must be the one created for THIS order (linked through the notes).
        var (razorpayOrder, orderError) = await FetchRazorpayAsync($"orders/{Uri.EscapeDataString(request.RazorpayOrderId)}");
        if (razorpayOrder == null)
        {
            _logger.LogError("Payment verification failed for order {OrderId}: {Error}", request.OrderId, orderError);
            return PaymentError("We could not confirm the payment with the gateway. Please contact support.");
        }

        if (!string.Equals(ReadOrderIdNote(razorpayOrder), request.OrderId.ToString(), StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Payment verification failed: Razorpay order {RazorpayOrderId} is not linked to order {OrderId}.",
                request.RazorpayOrderId, request.OrderId);

            return PaymentError("Payment verification failed - the payment does not belong to this order.");
        }

        var expectedAmountInPaise = ReadAmount(razorpayOrder);

        // 4. The payment must belong to that order and cover its amount.
        var (payment, paymentError) = await FetchRazorpayAsync($"payments/{Uri.EscapeDataString(request.RazorpayPaymentId)}");
        if (payment == null)
        {
            _logger.LogError("Payment verification failed for order {OrderId}: {Error}", request.OrderId, paymentError);
            return PaymentError("We could not confirm the payment with the gateway. Please contact support.");
        }

        if (!string.Equals(ReadString(payment, "order_id"), request.RazorpayOrderId, StringComparison.Ordinal))
        {
            _logger.LogWarning("Payment verification failed: payment is not for Razorpay order {RazorpayOrderId}.", request.RazorpayOrderId);
            return PaymentError("Payment verification failed - the payment does not belong to this order.");
        }

        if (ReadAmount(payment) < expectedAmountInPaise)
        {
            _logger.LogWarning(
                "Payment verification failed: paid {Paid} paise but order {OrderId} requires {Expected} paise.",
                ReadAmount(payment), request.OrderId, expectedAmountInPaise);

            return PaymentError("Payment verification failed - the amount paid does not match the order total.");
        }

        var status = ReadString(payment, "status") ?? string.Empty;

        // 5. Capture when the payment is only authorised, then require "captured".
        if (string.Equals(status, RazorpayStatusAuthorized, StringComparison.Ordinal))
        {
            var (captured, captureError) = await CaptureRazorpayPaymentAsync(request.RazorpayPaymentId, expectedAmountInPaise);
            if (!captured)
            {
                _logger.LogError(
                    "Capture failed for payment {PaymentId} (order {OrderId}): {Error}",
                    request.RazorpayPaymentId, request.OrderId, captureError);

                return PaymentError("The payment could not be captured. Please contact support - you have not been charged twice.");
            }

            status = RazorpayStatusCaptured;
        }

        if (!string.Equals(status, RazorpayStatusCaptured, StringComparison.Ordinal))
        {
            _logger.LogWarning("Payment for order {OrderId} is in status '{Status}' - not confirming.", request.OrderId, status);
            return PaymentError($"The payment is not complete yet (status: {status}). Please contact support if money was debited.");
        }

        // 6. The money is captured - confirm the order.
        await ConfirmOrderAsync(order, $"Payment captured. Razorpay Payment ID: {request.RazorpayPaymentId}");

        return new ResultDto { Result = 1, Messages = new[] { "Payment verified successfully." } };
    }

    /// <summary>Signature returned by Razorpay Checkout: HMAC-SHA256("&lt;order_id&gt;|&lt;payment_id&gt;", key_secret), hex.</summary>
    private static string GenerateRazorpaySignature(string orderId, string paymentId, string secret)
        => GenerateHmacHex($"{orderId}|{paymentId}", secret);

    /// <summary>Lower-case hex HMAC-SHA256 - the format Razorpay uses for payment and webhook signatures.</summary>
    private static string GenerateHmacHex(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    /// <summary>Length-safe, constant-time comparison for signatures and secrets.</summary>
    private static bool FixedTimeEquals(string? a, string? b)
    {
        var bytesA = Encoding.UTF8.GetBytes(a ?? string.Empty);
        var bytesB = Encoding.UTF8.GetBytes(b ?? string.Empty);

        return bytesA.Length == bytesB.Length && CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }

    /// <summary>
    /// Creates the order on Razorpay's side and returns its id, or an error description when it
    /// could not be created (e.g. HTTP 401 for unknown keys). No mock/placeholder id is ever
    /// returned: a fake order cannot be paid for and only hides the misconfiguration.
    /// </summary>
    private async Task<(string? OrderId, string? Error)> CreateRazorpayOrder(
        string keyId,
        string keySecret,
        int amountInPaise,
        string receipt,
        long orderId)
    {
        try
        {
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));

            // amount is in the smallest currency unit (paise). `payment_capture` is deliberately not
            // sent any more - capture is handled explicitly after verification (go-live checklist).
            // The notes carry our own order id so webhooks and verification can be mapped back to it.
            var payload = new
            {
                amount = amountInPaise,
                currency = "INR",
                receipt = receipt,
                notes = new { hc_order_id = orderId.ToString() }
            };

            var json = JsonSerializer.Serialize(payload);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.razorpay.com/v1/orders")
            {
                Content = content
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);

            using var response = await RazorpayHttp.SendAsync(message);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return (null,
                    $"Razorpay returned {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(responseBody, 300)}");
            }

            using var document = JsonDocument.Parse(responseBody);

            if (!document.RootElement.TryGetProperty("id", out var idElement) ||
                string.IsNullOrEmpty(idElement.GetString()))
            {
                return (null, $"Razorpay did not return an order id: {Truncate(responseBody, 300)}");
            }

            return (idElement.GetString(), null);
        }
        catch (Exception ex)
        {
            return (null, $"The call to Razorpay failed: {ex.GetBaseException().Message}");
        }
    }

    private async Task<(JsonElement? Entity, string? Error)> FetchRazorpayAsync(string relativeUrl)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, $"https://api.razorpay.com/v1/{relativeUrl}");
            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", RazorpayAuthHeader);

            using var response = await RazorpayHttp.SendAsync(message);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (null, $"Razorpay returned {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(body, 300)}");

            using var document = JsonDocument.Parse(body);

            // Clone so the element stays usable after the document is disposed.
            return (document.RootElement.Clone(), null);
        }
        catch (Exception ex)
        {
            return (null, $"The call to Razorpay failed: {ex.GetBaseException().Message}");
        }
    }

    private async Task<(bool Captured, string? Error)> CaptureRazorpayPaymentAsync(string paymentId, int amountInPaise)
    {
        try
        {
            var json = JsonSerializer.Serialize(new { amount = amountInPaise, currency = "INR" });

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var message = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://api.razorpay.com/v1/payments/{Uri.EscapeDataString(paymentId)}/capture")
            {
                Content = content
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", RazorpayAuthHeader);

            using var response = await RazorpayHttp.SendAsync(message);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (false, $"Razorpay returned {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(body, 300)}");

            using var document = JsonDocument.Parse(body);
            var status = ReadString(document.RootElement, "status");

            return (string.Equals(status, RazorpayStatusCaptured, StringComparison.Ordinal),
                $"capture returned status '{status}'");
        }
        catch (Exception ex)
        {
            return (false, $"The capture call failed: {ex.GetBaseException().Message}");
        }
    }

    private static string Truncate(string value, int maxLength)
        => string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength] + "...";
    /// <summary>
    /// Handles a Razorpay webhook. Per the docs, webhooks are the primary way to learn about
    /// payments: this confirms the order even when the customer closes the browser before Checkout
    /// reports back. The payload signature is verified first and repeat deliveries are no-ops.
    /// </summary>
    public async Task<ResultDto> HandlePaymentWebhookAsync(string rawBody, string? signature)
    {
        if (string.IsNullOrWhiteSpace(_razorpayWebhookSecret))
        {
            _logger.LogError(
                "A Razorpay webhook was received but 'Razorpay:WebhookSecret' is not configured - the payload was ignored.");

            return PaymentError("Webhook is not configured.");
        }

        // The signature is HMAC-SHA256 over the RAW request body, keyed with the webhook secret.
        if (!FixedTimeEquals(GenerateHmacHex(rawBody, _razorpayWebhookSecret), signature))
        {
            _logger.LogWarning("A Razorpay webhook was rejected: signature mismatch.");
            return PaymentError("Invalid webhook signature.");
        }

        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;

            var eventName = ReadString(root, "event") ?? string.Empty;
            var paymentEntity = TryGetProperty(root, "payload", "payment", "entity");
            var orderEntity = TryGetProperty(root, "payload", "order", "entity");

            var paymentStatus = ReadString(paymentEntity, "status");
            var paymentId = ReadString(paymentEntity, "id");
            var razorpayOrderId = ReadString(paymentEntity, "order_id") ?? ReadString(orderEntity, "id");

            // Only a captured payment may confirm an order (an uncaptured one is auto-refunded).
            if (!string.Equals(paymentStatus, RazorpayStatusCaptured, StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "Webhook '{Event}' ignored (payment status '{Status}').", eventName, paymentStatus ?? "n/a");

                return new ResultDto { Result = 1, Messages = new[] { $"Ignored '{eventName}'." } };
            }

            // Our own order id travels in the notes set when the Razorpay order was created.
            var orderIdNote = ReadOrderIdNote(orderEntity) ?? ReadOrderIdNote(paymentEntity);

            if (!long.TryParse(orderIdNote, out var orderId))
            {
                _logger.LogWarning(
                    "Webhook '{Event}' could not be matched to an order (Razorpay order '{RazorpayOrderId}').",
                    eventName, razorpayOrderId);

                return new ResultDto { Result = 1, Messages = new[] { "No matching order." } };
            }

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
            {
                _logger.LogWarning("Webhook '{Event}' references unknown order {OrderId}.", eventName, orderId);
                return new ResultDto { Result = 1, Messages = new[] { "Order not found." } };
            }

            // Idempotent: a repeated delivery of the same event changes nothing.
            if (order.OrderStatusId == OrderStatusConfirmed)
                return new ResultDto { Result = 1, Messages = new[] { "Order already confirmed." } };

            await ConfirmOrderAsync(order, $"Payment captured (webhook '{eventName}'). Razorpay Payment ID: {paymentId}");

            _logger.LogInformation("Order {OrderId} confirmed from webhook '{Event}'.", orderId, eventName);
            return new ResultDto { Result = 1, Messages = new[] { "Order confirmed." } };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "A Razorpay webhook payload could not be parsed.");
            return PaymentError("Invalid webhook payload.");
        }
    }

    /// <summary>Moves a paid order to Confirmed and records it in the order history.</summary>
    private async Task ConfirmOrderAsync(Order order, string comment)
    {
        order.OrderStatusId = OrderStatusConfirmed;

        _context.OrderHistories.Add(new OrderHistory
        {
            OrderId = order.OrderId,
            HistoryDate = DateTime.UtcNow,
            OrderStatusId = OrderStatusConfirmed,
            Comments = comment
        });

        await _context.SaveChangesAsync();
    }

    private static ResultDto PaymentError(string message)
        => new() { Result = 0, Messages = new[] { message } };


    private static string? ReadString(JsonElement? element, string property)
    {
        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Object)
            return null;

        return element.Value.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int ReadAmount(JsonElement? element)
    {
        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Object)
            return 0;

        return element.Value.TryGetProperty("amount", out var value) && value.TryGetInt32(out var amount)
            ? amount
            : 0;
    }

    /// <summary>Our own order id, carried in the Razorpay notes as <c>hc_order_id</c>.</summary>
    private static string? ReadOrderIdNote(JsonElement? entity)
    {
        if (!entity.HasValue || entity.Value.ValueKind != JsonValueKind.Object)
            return null;

        return entity.Value.TryGetProperty("notes", out var notes)
            ? ReadString(notes, "hc_order_id")
            : null;
    }

    private static JsonElement? TryGetProperty(JsonElement root, params string[] path)
    {
        var current = root;

        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
                return null;

            current = next;
        }

        return current;
    }


}
