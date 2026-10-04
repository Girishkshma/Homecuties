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

            var stock = await SkuAvailability.CountSellableByProductAsync(
                _context, guestCart.GuestCartItems.Select(ci => ci.ProductId));

            var cartItems = guestCart.GuestCartItems.Select(ci =>
            {
                var availableQty = stock[ci.ProductId];
                return new CartItemDto
                {
                    ProductID = ci.ProductId,
                    ProductName = ci.Product.ProductName,
                    ProductTitle = ci.Product.ProductTitle,
                    Quantity = ci.Quantity,
                    // What one of this product costs the customer, everything in (ProductPricing) - so the price the
                    // cart shows is the price the checkout charges for it.
                    Price = ProductPricing.ListingPrice(ProductPricing.Of(ci.Product)),
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

            var stock = await SkuAvailability.CountSellableByProductAsync(
                _context, cart.CartItems.Select(ci => ci.ProductId));

            var cartItems = cart.CartItems.Select(ci =>
            {
                var availableQty = stock[ci.ProductId];
                return new CartItemDto
                {
                    ProductID = ci.ProductId,
                    ProductName = ci.Product.ProductName,
                    ProductTitle = ci.Product.ProductTitle,
                    Quantity = ci.Quantity,
                    // What one of this product costs the customer, everything in (ProductPricing) - so the price the
                    // cart shows is the price the checkout charges for it.
                    Price = ProductPricing.ListingPrice(ProductPricing.Of(ci.Product)),
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
