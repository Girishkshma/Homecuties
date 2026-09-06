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
    private static CartResponseDto MapGuestCartToResponse(GuestCart guestCart)
    {
        var items = guestCart.GuestCartItems.Select(ci =>
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

        var calculation = new CartCalculationDto
        {
            Count = items.Sum(i => i.Quantity),
            SalesPrice = items.Sum(i => i.Price * i.Quantity),
            GrandTotal = items.Sum(i => i.Price * i.Quantity)
        };

        return new CartResponseDto { Items = items, Calculation = calculation };
    }

    private static CartResponseDto MapCartToResponse(Data.Entities.Cart cart)
    {
        var items = cart.CartItems.Select(ci =>
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
