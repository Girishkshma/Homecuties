using HC.Business.Dtos;

namespace HC.Business;

public interface IWishListService
{
    Task<IEnumerable<WishListDto>> GetWishListAsync(long customerId);
    Task<ResultDto> AddToWishListAsync(long customerId, int productId);
    Task<ResultDto> RemoveFromWishListAsync(long customerId, int productId);
    Task<bool> IsInWishListAsync(long customerId, int productId);
}
