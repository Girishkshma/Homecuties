// ============================================================
// CartService.Calculation.cs
// Partial class: CartService - Calculation operations
// ============================================================

using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

public partial class CartService : ICartService
{
    private static CartCalculationDto CalculateCart(List<CartItem> items)
    {
        var count = items.Sum(i => (int)i.Quantity);
        var salesPrice = items.Sum(i => i.Product.UnitPrice * i.Quantity);
        var discount = items.Sum(i => i.Product.UnitPrice * i.Product.DiscountPercent / 100 * i.Quantity);
        var addDiscount = items.Sum(i => i.Product.UnitPrice * i.Product.AdditionalDiscountPercent / 100 * i.Quantity);
        var gstPercent = items.FirstOrDefault()?.Product.Cgstpercent ?? 0;
        var gstCharge = (salesPrice - discount - addDiscount) * gstPercent / 100;
        var subTotal = salesPrice - discount - addDiscount;
        var grandTotal = subTotal + gstCharge;

        return new CartCalculationDto
        {
            Count = count,
            SalesPrice = salesPrice,
            Discount = discount,
            AddDiscount = addDiscount,
            GST = gstPercent.ToString("F2"),
            GSTCharge = gstCharge,
            SubTotal = subTotal,
            GrandTotal = grandTotal
        };
    }

    private static CartCalculationDto CalculateCart(List<GuestCartItem> items)
    {
        var count = items.Sum(i => (int)i.Quantity);
        var salesPrice = items.Sum(i => i.Product.UnitPrice * i.Quantity);
        var discount = items.Sum(i => i.Product.UnitPrice * i.Product.DiscountPercent / 100 * i.Quantity);
        var addDiscount = items.Sum(i => i.Product.UnitPrice * i.Product.AdditionalDiscountPercent / 100 * i.Quantity);
        var gstPercent = items.FirstOrDefault()?.Product.Cgstpercent ?? 0;
        var gstCharge = (salesPrice - discount - addDiscount) * gstPercent / 100;
        var subTotal = salesPrice - discount - addDiscount;
        var grandTotal = subTotal + gstCharge;

        return new CartCalculationDto
        {
            Count = count,
            SalesPrice = salesPrice,
            Discount = discount,
            AddDiscount = addDiscount,
            GST = gstPercent.ToString("F2"),
            GSTCharge = gstCharge,
            SubTotal = subTotal,
            GrandTotal = grandTotal
        };
    }
}
