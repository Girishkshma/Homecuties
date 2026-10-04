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
    /// <summary>
    /// One cart line as the money is read: how many units the customer asked for, and the price of one of them.
    /// </summary>
    private readonly record struct CartLine(int Quantity, ProductPricing.Inputs Price);

    /// <summary>
    /// What a cart is worth, read the one way a price is read anywhere else (see <see cref="ProductPricing"/>): every
    /// line's unit is priced from its product's own 'Pricing &amp; Charges' and 'Taxes' fields - the unit price, the
    /// shop's margin, its packaging / storage / delivery charges, the discount taken off that gross, the additional
    /// discount taken off what it left, and the tax on the rest - and multiplied by how many of it the customer
    /// asked for.
    ///
    /// The figures add up to the one that matters: <see cref="CartCalculationDto.GrandTotal"/> is each line's listing
    /// price times its quantity, which is the price the storefront shows for that same unit, the figure the checkout
    /// asks the gateway for, and the figure the order's own lines are written at.
    ///
    /// <see cref="CartCalculationDto.GST"/> stays a single rate - the one the cart's first line is charged at, which
    /// is what it has always been - because one rate cannot speak for a cart of products taxed differently. The
    /// charge itself (<see cref="CartCalculationDto.GSTCharge"/>) is worked out per line, so it is right whatever
    /// those rates are.
    /// </summary>
    private static CartCalculationDto CalculateCart(IEnumerable<CartLine> lines)
    {
        var cartLines = lines.ToList();

        return new CartCalculationDto
        {
            Count = cartLines.Sum(line => line.Quantity),
            SalesPrice = cartLines.Sum(line => ProductPricing.Gross(line.Price) * line.Quantity),
            Discount = cartLines.Sum(line => ProductPricing.Discount(line.Price) * line.Quantity),
            AddDiscount = cartLines.Sum(line => ProductPricing.AdditionalDiscount(line.Price) * line.Quantity),
            GST = (cartLines.Count == 0 ? 0m : ProductPricing.GstRate(cartLines[0].Price)).ToString("F2"),
            GSTCharge = cartLines.Sum(line => ProductPricing.GstAmount(line.Price) * line.Quantity),
            SubTotal = cartLines.Sum(line => ProductPricing.TaxableValue(line.Price) * line.Quantity),
            GrandTotal = cartLines.Sum(line => ProductPricing.ListingPrice(line.Price) * line.Quantity)
        };
    }

    private static CartCalculationDto CalculateCart(List<CartItem> items) =>
        CalculateCart(items.Select(item => new CartLine(item.Quantity, ProductPricing.Of(item.Product))));

    private static CartCalculationDto CalculateCart(List<GuestCartItem> items) =>
        CalculateCart(items.Select(item => new CartLine(item.Quantity, ProductPricing.Of(item.Product))));
}

