using HC.Business;

namespace HC.Tests;

/// <summary>
/// One unit's pricing fields built by hand: the ten figures the product form's 'Pricing &amp; Charges' and 'Taxes'
/// sections hold, in the shape the one pricing rule reads them in (<see cref="ProductPricing.Inputs"/>).
///
/// Every figure but the unit price defaults to nothing, so a test about the tax says only the tax: ten positional
/// numbers in a row is where a test stops being readable, and a test nobody can read is one nobody checks when the
/// rule moves. It lives here rather than in either test file because two of them build prices.
/// </summary>
internal static class TestPrices
{
    public static ProductPricing.Inputs Priced(
        decimal unitPrice,
        decimal discountPercent = 0m,
        decimal additionalDiscountPercent = 0m,
        decimal profitMarginPercent = 0m,
        decimal packagingCharge = 0m,
        decimal storageCharge = 0m,
        decimal deliveryCharge = 0m,
        decimal cgstPercent = 0m,
        decimal sgstPercent = 0m,
        decimal igstPercent = 0m) =>
        new(
            unitPrice,
            discountPercent,
            additionalDiscountPercent,
            profitMarginPercent,
            packagingCharge,
            storageCharge,
            deliveryCharge,
            cgstPercent,
            sgstPercent,
            igstPercent);
}
