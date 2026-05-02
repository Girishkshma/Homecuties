using HC.Business;
using Microsoft.AspNetCore.Mvc;

namespace HC.Services.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService)
    {
        _cartService = cartService;
    }

    [HttpPost("AddToCart")]
    public async Task<ActionResult> AddToCart([FromBody] AddToCartRequest request)
    {
        var result = await _cartService.AddToCartAsync(request.CustomerID, request.IsGuest, request.ProductID, request.Quantity);
        return Ok(result);
    }

    [HttpPost("GetCart")]
    public async Task<ActionResult> GetCart([FromBody] CartRequest request)
    {
        var result = await _cartService.GetCartAsync(request.CustomerID, request.IsGuest);
        return Ok(result);
    }

    [HttpPost("GetItemsCount")]
    public async Task<ActionResult> GetItemsCount([FromBody] CartRequest request)
    {
        var result = await _cartService.GetItemsCountAsync(request.CustomerID, request.IsGuest);
        return Ok(result);
    }

    [HttpPost("TransferGuestCart")]
    public async Task<ActionResult> TransferGuestCart([FromBody] TransferCartRequest request)
    {
        var result = await _cartService.TransferGuestCartAsync(request.GuestCustomerID, request.CustomerID);
        return Ok(result);
    }

    [HttpPost("UpdateQuantity")]
    public async Task<ActionResult> UpdateQuantity([FromBody] UpdateCartQuantityRequest request)
    {
        var result = await _cartService.UpdateCartItemQuantityAsync(request.CustomerID, request.IsGuest, request.ProductID, request.Quantity);
        return Ok(result);
    }

    [HttpPost("RemoveItem")]
    public async Task<ActionResult> RemoveItem([FromBody] RemoveCartItemRequest request)
    {
        var result = await _cartService.RemoveFromCartAsync(request.CustomerID, request.IsGuest, request.ProductID);
        return Ok(result);
    }
}

public class AddToCartRequest
{
    public long CustomerID { get; set; }
    public bool IsGuest { get; set; }
    public int ProductID { get; set; }
    public int Quantity { get; set; }
}

public class CartRequest
{
    public long CustomerID { get; set; }
    public bool IsGuest { get; set; }
}

public class TransferCartRequest
{
    public long GuestCustomerID { get; set; }
    public long CustomerID { get; set; }
}

public class UpdateCartQuantityRequest
{
    public long CustomerID { get; set; }
    public bool IsGuest { get; set; }
    public int ProductID { get; set; }
    public int Quantity { get; set; }
}

public class RemoveCartItemRequest
{
    public long CustomerID { get; set; }
    public bool IsGuest { get; set; }
    public int ProductID { get; set; }
}
