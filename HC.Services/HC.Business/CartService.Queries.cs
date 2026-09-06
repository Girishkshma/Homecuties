// ============================================================
// CartService.Queries.cs
// Partial class: CartService - Queries operations
// ============================================================

using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

public partial class CartService : ICartService
{
    public async Task<CartResponseDto> GetCartAsync(long customerId, bool isGuest)
    {
        if (isGuest)
        {
            var guestCart = await _context.Set<GuestCart>()
                .Include(gc => gc.GuestCartItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.ProductImages)
                .FirstOrDefaultAsync(gc => gc.CustomerId == customerId);

            if (guestCart == null)
                return new CartResponseDto();

            var cartItems = guestCart.GuestCartItems.Select(ci =>
            {
                var availableSkus = ci.Product.PurchaseDetails
                    .SelectMany(pd => pd.Skus)
                    .Where(s => !s.OrderItems.Any())
                    .ToList();
                var availableQty = availableSkus.Count;
                return new CartItemDto
                {
                    ProductID = ci.ProductId,
                    ProductName = ci.Product.ProductName,
                    ProductTitle = ci.Product.ProductTitle,
                    Quantity = ci.Quantity,
                    Price = ci.Product.UnitPrice,
                    Image = ci.Product.ProductImages
                        .Where(pi => pi.IsPromoImage && pi.IsActive)
                        .Select(pi => pi.ImageUrl)
                        .FirstOrDefault() ?? "",
                    IsInStock = availableQty > 0,
                    AvailableQty = availableQty
                };
            }).ToList();

            var calculation = CalculateCart(guestCart.GuestCartItems.ToList());
            return new CartResponseDto { Items = cartItems, Calculation = calculation };
        }
        else
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.ProductImages)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (cart == null)
                return new CartResponseDto();

            var cartItems = cart.CartItems.Select(ci =>
            {
                var availableSkus = ci.Product.PurchaseDetails
                    .SelectMany(pd => pd.Skus)
                    .Where(s => !s.OrderItems.Any())
                    .ToList();
                var availableQty = availableSkus.Count;
                return new CartItemDto
                {
                    ProductID = ci.ProductId,
                    ProductName = ci.Product.ProductName,
                    ProductTitle = ci.Product.ProductTitle,
                    Quantity = ci.Quantity,
                    Price = ci.Product.UnitPrice,
                    Image = ci.Product.ProductImages
                        .Where(pi => pi.IsPromoImage && pi.IsActive)
                        .Select(pi => pi.ImageUrl)
                        .FirstOrDefault() ?? "",
                    IsInStock = availableQty > 0,
                    AvailableQty = availableQty
                };
            }).ToList();

            var calculation = CalculateCart(cart.CartItems.ToList());
            return new CartResponseDto { Items = cartItems, Calculation = calculation };
        }
    }

    public async Task<CartCalculationDto> GetItemsCountAsync(long customerId, bool isGuest)
    {
        if (isGuest)
        {
            var guestCart = await _context.Set<GuestCart>()
                .Include(gc => gc.GuestCartItems)
                    .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(gc => gc.CustomerId == customerId);

            return CalculateCart(guestCart?.GuestCartItems.ToList() ?? new List<GuestCartItem>());
        }
        else
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            return CalculateCart(cart?.CartItems.ToList() ?? new List<CartItem>());
        }
    }

}
