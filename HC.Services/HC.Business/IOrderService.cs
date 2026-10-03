using HC.Business.Dtos;

namespace HC.Business;

public interface IOrderService
{
    Task<CreateOrderResponse> CreateOrderAsync(CreateOrderRequest request);
    Task<ResultDto> VerifyPaymentAsync(VerifyPaymentRequest request, long customerId);
    Task<ResultDto> HandlePaymentWebhookAsync(string rawBody, string? signature);
    Task<List<OrderListDto>> GetOrdersAsync(long customerId, bool isGuest);

    /// <summary>
    /// Asks the courier about the live parcels of the signed-in customer's orders and answers with
    /// what it said, so 'My Orders' can show a parcel that moved since the page was last opened.
    ///
    /// It is deliberately throttled ('Shipping:SyncThrottleMinutes'): a parcel looked up a minute ago is
    /// not looked up again, and one the courier is done with (delivered, coming back, called off) is
    /// left alone for good - so a page open is not itself a courier call. A courier that cannot be
    /// reached is a sentence in <c>Messages</c>, never an error on the page, and the pull can only move
    /// an order along the steps its lifecycle allows (see HC.Business.ShipmentStatusFlow).
    /// </summary>
    Task<RefreshOrderShipmentsDto> RefreshShipmentsAsync(long customerId);

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

    /// <summary>
    /// Starts a fresh payment attempt for an order that is still waiting for its money ('Pay now' in
    /// 'My Orders'), after checking with Razorpay that the earlier attempt really was not paid.
    /// </summary>
    Task<CreateOrderResponse> RetryOrderPaymentAsync(long customerId, long orderId);
}
