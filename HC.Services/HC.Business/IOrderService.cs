using HC.Business.Dtos;

namespace HC.Business;

public interface IOrderService
{
    Task<CreateOrderResponse> CreateOrderAsync(CreateOrderRequest request);
    Task<ResultDto> VerifyPaymentAsync(VerifyPaymentRequest request, long customerId);
    Task<ResultDto> HandlePaymentWebhookAsync(string rawBody, string? signature);
    Task<List<OrderListDto>> GetOrdersAsync(long customerId, bool isGuest);

    /// <summary>
    /// Cancels an order that is still waiting for its payment and returns the reserved units to the
    /// available pool. Paid orders are refused on purpose - refunds are handled by the shop team.
    /// </summary>
    Task<ResultDto> CancelOrderAsync(long customerId, long orderId);

    /// <summary>
    /// Re-checks the payment of an order directly with Razorpay and confirms the order when the money
    /// was taken. This is the safety net for a customer whose browser never reported the payment
    /// result back (closed window, lost connection) and for a missing webhook.
    /// </summary>
    Task<ResultDto> SyncOrderPaymentAsync(long customerId, long orderId);
}
