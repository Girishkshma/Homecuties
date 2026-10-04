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
    ///
    /// Each line's <c>Price</c> is what ONE of its units costs the customer, with everything the product form's
    /// 'Pricing &amp; Charges' and 'Taxes' sections say included (<see cref="ProductPricing.ListingPrice"/>) - the
    /// price the storefront shows for the same unit and the price the checkout charges - so the cart's total, the
    /// bill and the listing cannot disagree.
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
                Price = ProductPricing.ListingPrice(ProductPricing.Of(ci.Product)),
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
                Price = ProductPricing.ListingPrice(ProductPricing.Of(ci.Product)),
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

    /// <summary>
    /// The total of the lines that are actually going to be charged for: the checkout drops every unit it could not
    /// reserve stock for and recomputes over what is left. Each line's <c>Price</c> is what the customer pays for one
    /// unit (see the mapping above), so this total IS the amount the gateway is asked for.
    /// </summary>
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
