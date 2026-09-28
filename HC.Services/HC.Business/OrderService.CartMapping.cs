// ============================================================
// OrderService.CartMapping.cs
// Partial class: OrderService - CartMapping operations
// ============================================================

using System.Security.Cryptography;
using System.Text;
using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HC.Business;

public partial class OrderService : IOrderService
{
    /// <summary>
    /// Maps a guest cart to the response used by the storefront and by the checkout.
    /// <paramref name="stock"/> holds the sellable units per product (see
    /// <see cref="SkuAvailability.CountSellableByProductAsync"/>) - the checkout trims the quantities
    /// with it, so it is the same figure every other screen shows.
    /// </summary>
    private static CartResponseDto MapGuestCartToResponse(GuestCart guestCart, IReadOnlyDictionary<int, int> stock)
    {
        var items = guestCart.GuestCartItems.Select(ci =>
        {
            var availableQty = stock[ci.ProductId];
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

        var calculation = new CartCalculationDto
        {
            Count = items.Sum(i => i.Quantity),
            SalesPrice = items.Sum(i => i.Price * i.Quantity),
            GrandTotal = items.Sum(i => i.Price * i.Quantity)
        };

        return new CartResponseDto { Items = items, Calculation = calculation };
    }

    /// <inheritdoc cref="MapGuestCartToResponse(GuestCart, IReadOnlyDictionary{int, int})" />
    private static CartResponseDto MapCartToResponse(Data.Entities.Cart cart, IReadOnlyDictionary<int, int> stock)
    {
        var items = cart.CartItems.Select(ci =>
        {
            var availableQty = stock[ci.ProductId];
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

        var calculation = new CartCalculationDto
        {
            Count = items.Sum(i => i.Quantity),
            SalesPrice = items.Sum(i => i.Price * i.Quantity),
            GrandTotal = items.Sum(i => i.Price * i.Quantity)
        };

        return new CartResponseDto { Items = items, Calculation = calculation };
    }

    private static CartCalculationDto RecalculateCart(List<CartItemDto> items)
    {
        return new CartCalculationDto
        {
            Count = items.Sum(i => i.Quantity),
            SalesPrice = items.Sum(i => i.Price * i.Quantity),
            GrandTotal = items.Sum(i => i.Price * i.Quantity)
        };
    }

}
