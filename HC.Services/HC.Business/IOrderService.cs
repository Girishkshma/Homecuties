using HC.Business.Dtos;

namespace HC.Business;

public interface IOrderService
{
    Task<CreateOrderResponse> CreateOrderAsync(CreateOrderRequest request);
    Task<ResultDto> VerifyPaymentAsync(VerifyPaymentRequest request);
    Task<List<OrderListDto>> GetOrdersAsync(long customerId, bool isGuest);
}
