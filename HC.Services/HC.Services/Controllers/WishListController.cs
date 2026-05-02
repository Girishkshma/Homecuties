using HC.Business;
using HC.Business.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace HC.Services.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WishListController : ControllerBase
{
    private readonly IWishListService _wishListService;

    public WishListController(IWishListService wishListService)
    {
        _wishListService = wishListService;
    }

    [HttpPost("GetWishList")]
    public async Task<ActionResult<IEnumerable<WishListDto>>> GetWishList([FromBody] GetWishListRequest request)
    {
        var wishList = await _wishListService.GetWishListAsync(request.CustomerId);
        return Ok(wishList);
    }

    [HttpPost("AddToWishList")]
    public async Task<ActionResult<ResultDto>> AddToWishList([FromBody] AddToWishListRequest request)
    {
        var result = await _wishListService.AddToWishListAsync(request.CustomerId, request.ProductId);
        return Ok(result);
    }

    [HttpPost("RemoveFromWishList")]
    public async Task<ActionResult<ResultDto>> RemoveFromWishList([FromBody] RemoveFromWishListRequest request)
    {
        var result = await _wishListService.RemoveFromWishListAsync(request.CustomerId, request.ProductId);
        return Ok(result);
    }

    [HttpPost("IsInWishList")]
    public async Task<ActionResult<bool>> IsInWishList([FromBody] CheckWishListItemRequest request)
    {
        var isInWishList = await _wishListService.IsInWishListAsync(request.CustomerId, request.ProductId);
        return Ok(isInWishList);
    }
}
