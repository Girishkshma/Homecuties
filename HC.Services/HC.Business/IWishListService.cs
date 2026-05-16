using HC.Business.Dtos;

namespace HC.Business;

public interface IWishListService
{
    Task<IEnumerable<WishListDto>> GetWishListAsync(long customerId, bool isGuest);
    Task<ResultDto> AddToWishListAsync(long customerId, int productId, bool isGuest);
    Task<ResultDto> RemoveFromWishListAsync(long customerId, int productId, bool isGuest);
    Task<bool> IsInWishListAsync(long customerId, int productId, bool isGuest);
    Task<ResultDto> TransferGuestWishListAsync(long guestCustomerId, long customerId);
}
