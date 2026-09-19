using HC.Business.Dtos;

namespace HC.Business;

public interface IOrderService
{
    Task<CreateOrderResponse> CreateOrderAsync(CreateOrderRequest request);
    Task<ResultDto> VerifyPaymentAsync(VerifyPaymentRequest request, long customerId);
    Task<ResultDto> HandlePaymentWebhookAsync(string rawBody, string? signature);
    Task<List<OrderListDto>> GetOrdersAsync(long customerId, bool isGuest);
}
