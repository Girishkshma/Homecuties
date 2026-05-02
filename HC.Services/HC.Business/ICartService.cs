using HC.Business.Dtos;

namespace HC.Business;

public interface ICartService
{
    Task<ResultDto> AddToCartAsync(long customerId, bool isGuest, int productId, int quantity);
    Task<CartResponseDto> GetCartAsync(long customerId, bool isGuest);
    Task<CartCalculationDto> GetItemsCountAsync(long customerId, bool isGuest);
    Task<ResultDto> TransferGuestCartAsync(long guestCustomerId, long customerId);
    Task<ResultDto> UpdateCartItemQuantityAsync(long customerId, bool isGuest, int productId, int quantity);
    Task<ResultDto> RemoveFromCartAsync(long customerId, bool isGuest, int productId);
}
