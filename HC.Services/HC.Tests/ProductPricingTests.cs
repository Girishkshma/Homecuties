using HC.Business;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HC.Tests;

/// <summary>
/// What one product costs the customer, and everything that price is made of (see HC.Business.ProductPricing).
///
/// The product form asks for a product's money in two sections - 'Pricing &amp; Charges' (the unit price, both
/// discounts, the packaging / storage / delivery charges and the profit margin) and 'Taxes' (CGST, SGST, IGST) - and
/// the price a shopper is shown has to account for all of them. These tests pin that composition, because the same
/// figure is read by the storefront's listings, the wish list, the cart, the amount the gateway is asked for, the
/// amount a retry or a refund is worked out from, and the shop's own books: a change here moves money on every one
/// of them.
///
/// Every figure of that price is a whole number of RUPEES, taken to the rupee as it is read - the gross, each
/// discount, the value the tax falls on and the tax itself (<see cref="ProductPricing.Rupees"/>, half a rupee going
/// up) - so the figures a bill is read a row at a time add up to the price the customer was charged.
/// </summary>
public class ProductPricingTests
{
    /// <summary>
    /// Every field of both sections is in the price, in the order they are read in: the unit price, plus the shop's
    /// margin, plus its own handling charges, less the discount off that gross, less the additional discount off
    /// what that left, plus the tax on the rest. A 200.00 unit with a 25% margin, 10.00 / 5.00 / 15.00 of handling,
    /// 10% off with a further 5% off, and 9% CGST with 9% SGST comes to 239.00 before tax and 282.00 with it.
    /// </summary>
    [Fact]
    public void EveryFieldOfBothSectionsIsInThePrice()
    {
        var price = TestPrices.Priced(
            200m,
            discountPercent: 10m,
            additionalDiscountPercent: 5m,
            profitMarginPercent: 25m,
            packagingCharge: 10m,
            storageCharge: 5m,
            deliveryCharge: 15m,
            cgstPercent: 9m,
            sgstPercent: 9m);

        Assert.Equal(50m, ProductPricing.Margin(price));
        Assert.Equal(30m, ProductPricing.Charges(price));
        Assert.Equal(280m, ProductPricing.Gross(price));
        Assert.Equal(28m, ProductPricing.Discount(price));
        Assert.Equal(252m, ProductPricing.AfterDiscount(price));

        // 5% of the 252.00 the discount left is 12.60, and a till takes that half-rupee UP: 13.00.
        Assert.Equal(13m, ProductPricing.AdditionalDiscount(price));
        Assert.Equal(41m, ProductPricing.TotalDiscount(price));
        Assert.Equal(239m, ProductPricing.TaxableValue(price));
        Assert.Equal(18m, ProductPricing.GstRate(price));

        // 18% of 239.00 is 43.02, taken to the rupee as 43.00: the tax is charged in the same coin as the price.
        Assert.Equal(43m, ProductPricing.GstAmount(price));
        Assert.Equal(282m, ProductPricing.ListingPrice(price));

        // With no discount it would be 280.00 of goods and the same 18% on them (50.40, taken up to 50.00) - what a
        // listing strikes through.
        Assert.Equal(330m, ProductPricing.PreDiscountListingPrice(price));

        // And with only the first of the two read: 252.00 of goods with the same 18% on them (45.36, taken up to
        // 45.00) - the middle figure of the three a product page walks a shopper down.
        Assert.Equal(297m, ProductPricing.PostDiscountListingPrice(price));
    }

    /// <summary>
    /// Money is charged in whole RUPEES, and a half is taken UP - the till's rule, which is also what SQL Server's
    /// ROUND does, so the backfill script and the database-side reading of this rule
    /// (<see cref="ProductPricing.ChargedPriceExpression"/>) agree with it rupee for rupee.
    ///
    /// Half going up rather than to the nearest even figure is the point: 4.50 of additional discount on a 90.00
    /// price is 5.00 here and would be 4.00 under the halfway-to-even rounding the .NET default takes, which is a
    /// rupee of the customer's money decided by a coin toss.
    /// </summary>
    [Fact]
    public void MoneyIsChargedInWholeRupees()
    {
        Assert.Equal(280m, ProductPricing.Rupees(280m));
        Assert.Equal(13m, ProductPricing.Rupees(12.5m));
        Assert.Equal(13m, ProductPricing.Rupees(12.6m));
        Assert.Equal(12m, ProductPricing.Rupees(12.4m));
        Assert.Equal(-1m, ProductPricing.Rupees(-0.5m));
        Assert.Equal(0m, ProductPricing.Rupees(0m));

        // A price made of halves all the way down is still whole figures: 100.00 of goods, 10% off, then 5% of the
        // 90.00 that left - 4.50, taken up to 5.00 of additional discount.
        var price = TestPrices.Priced(100m, discountPercent: 10m, additionalDiscountPercent: 5m, cgstPercent: 9m, sgstPercent: 9m);

        Assert.Equal(100m, ProductPricing.Gross(price));
        Assert.Equal(10m, ProductPricing.Discount(price));
        Assert.Equal(5m, ProductPricing.AdditionalDiscount(price));
        Assert.Equal(85m, ProductPricing.TaxableValue(price));
        Assert.Equal(15m, ProductPricing.GstAmount(price));
        Assert.Equal(100m, ProductPricing.ListingPrice(price));

        // And the three figures a card walks a shopper down are whole figures too, each of them a price that could be
        // charged on its own: 118.00 of goods with 18% on them, then 90.00 of them with it, then 85.00 with it.
        Assert.Equal(118m, ProductPricing.PreDiscountListingPrice(price));
        Assert.Equal(106m, ProductPricing.PostDiscountListingPrice(price));
        Assert.Equal(100m, ProductPricing.ListingPrice(price));

        // The walk steps are whole rupees apart, so a card can say what each step saved without a fraction of a
        // rupee appearing in the difference.
        Assert.Equal(12m, ProductPricing.PreDiscountListingPrice(price) - ProductPricing.PostDiscountListingPrice(price));
        Assert.Equal(6m, ProductPricing.PostDiscountListingPrice(price) - ProductPricing.ListingPrice(price));
    }

    /// <summary>
    /// The margin is ADDED to the unit price, and it is the same figure the Finance screen declares on a sale
    /// (OrderMoney.DeclaredProfit): a 25% margin on a 200.00 unit is 50.00, and the customer pays it. A product with
    /// no margin declared is charged none - never a guessed one.
    /// </summary>
    [Fact]
    public void TheMarginIsAddedToTheUnitPrice()
    {
        var price = TestPrices.Priced(200m, profitMarginPercent: 25m);

        Assert.Equal(50m, ProductPricing.Margin(price));
        Assert.Equal(OrderMoney.DeclaredProfit(200m, 25m), ProductPricing.Margin(price));
        Assert.Equal(250m, ProductPricing.ListingPrice(price));

        Assert.Equal(200m, ProductPricing.ListingPrice(TestPrices.Priced(200m)));
    }

    /// <summary>
    /// The shop's own handling is charged on every unit the customer takes: packaging, storage and delivery added
    /// up, on top of the unit price and the margin, and taxed with them.
    /// </summary>
    [Fact]
    public void TheShopsOwnHandlingIsChargedOnEveryUnit()
    {
        var price = TestPrices.Priced(
            100m,
            packagingCharge: 12.50m,
            storageCharge: 7.50m,
            deliveryCharge: 40m);

        Assert.Equal(60m, ProductPricing.Charges(price));
        Assert.Equal(160m, ProductPricing.Gross(price));
        Assert.Equal(160m, ProductPricing.ListingPrice(price));
    }

    /// <summary>
    /// The two discounts are taken one after the other, and each is a share of what it is taken off: the discount a
    /// share of the gross - the unit price with the shop's margin and its own handling charges already added to it,
    /// which is the price the form's discount is advertised on - and the additional discount a share of what the
    /// first one left. Two percentages of one bill read this way are worth less than the two added up, which is
    /// what a bill does with them. Each is a whole number of rupees, and so is what it leaves, so the figures a bill
    /// shows for a line add up to what the line was charged.
    /// </summary>
    [Fact]
    public void TheTwoDiscountsAreTakenOneAfterTheOther()
    {
        var price = TestPrices.Priced(200m, discountPercent: 10m, additionalDiscountPercent: 5m);

        // 10% of 200.00, and then 5% of the 180.00 that left - not 10% and 5% of 200.00.
        Assert.Equal(20m, ProductPricing.Discount(price));
        Assert.Equal(180m, ProductPricing.AfterDiscount(price));
        Assert.Equal(9m, ProductPricing.AdditionalDiscount(price));
        Assert.Equal(29m, ProductPricing.TotalDiscount(price));
        Assert.Equal(171m, ProductPricing.TaxableValue(price));

        var withMargin = TestPrices.Priced(
            200m,
            discountPercent: 10m,
            additionalDiscountPercent: 5m,
            profitMarginPercent: 25m);

        // The margin is inside what the discount is taken off: 10% of 250.00 is 25.00, and 5% of the 225.00 it left
        // is 11.25 - taken to the rupee as 11.00, because the second discount is a whole figure as the first is.
        Assert.Equal(25m, ProductPricing.Discount(withMargin));
        Assert.Equal(225m, ProductPricing.AfterDiscount(withMargin));
        Assert.Equal(11m, ProductPricing.AdditionalDiscount(withMargin));
        Assert.Equal(36m, ProductPricing.TotalDiscount(withMargin));
        Assert.Equal(214m, ProductPricing.TaxableValue(withMargin));
    }

    /// <summary>
    /// The three figures a product page walks a shopper down - the sell price, the price the discount leaves, and the
    /// price the additional discount leaves - are the one composition read with one more discount in it each time,
    /// each saying the tax on the value left at that step. The walk therefore only ever comes down, and a product
    /// with nothing to discount is the one figure said once.
    ///
    /// Each of the three is a whole number of rupees and a price a customer could be charged on its own, which is
    /// what lets a card show the walk at all: a shopper comparing the steps is comparing three real prices, and the
    /// saving between two neighbouring steps is the discount that came off (with the tax it took with it) to the
    /// rupee.
    /// </summary>
    [Fact]
    public void ThePriceIsWalkedDownOneDiscountAtATime()
    {
        var price = TestPrices.Priced(
            200m,
            discountPercent: 10m,
            additionalDiscountPercent: 5m,
            profitMarginPercent: 25m,
            packagingCharge: 10m,
            storageCharge: 5m,
            deliveryCharge: 15m,
            cgstPercent: 9m,
            sgstPercent: 9m);

        // 280.00 of goods less 28.00 = 252.00, less 13.00 = 239.00 - each taxed at the same 18%, and each figure
        // taken to the rupee as the walk reads it.
        Assert.Equal(330m, ProductPricing.PreDiscountListingPrice(price));
        Assert.Equal(297m, ProductPricing.PostDiscountListingPrice(price));
        Assert.Equal(282m, ProductPricing.ListingPrice(price));

        Assert.True(ProductPricing.PreDiscountListingPrice(price) > ProductPricing.PostDiscountListingPrice(price));
        Assert.True(ProductPricing.PostDiscountListingPrice(price) > ProductPricing.ListingPrice(price));

        // Nothing to discount: the sell price is the price, and the steps that would follow it say the same figure.
        var plain = TestPrices.Priced(200m, cgstPercent: 9m, sgstPercent: 9m);

        Assert.Equal(236m, ProductPricing.PreDiscountListingPrice(plain));
        Assert.Equal(ProductPricing.PreDiscountListingPrice(plain), ProductPricing.PostDiscountListingPrice(plain));
        Assert.Equal(ProductPricing.PostDiscountListingPrice(plain), ProductPricing.ListingPrice(plain));
    }

    /// <summary>
    /// The rate is what the 'Taxes' section records, read the one way a sale can be taxed: the CGST and SGST rates
    /// together where either is set (inside the shop's own state), else the IGST rate (across states). A product
    /// carrying none of the three is charged no tax - never a default rate.
    ///
    /// A listing has no customer behind it to say which of the two a sale will be, so the price it shows is the
    /// intra-state one; a product recording both readings at once is charged the CGST + SGST one, which is what the
    /// Finance screen warns a shop keeper about.
    /// </summary>
    [Fact]
    public void TheRateIsCgstAndSgstTogetherElseIgst()
    {
        Assert.Equal(18m, ProductPricing.GstRate(TestPrices.Priced(100m, cgstPercent: 9m, sgstPercent: 9m)));
        Assert.Equal(18m, ProductPricing.GstRate(TestPrices.Priced(100m, cgstPercent: 9m, sgstPercent: 9m, igstPercent: 18m)));
        Assert.Equal(18m, ProductPricing.GstRate(TestPrices.Priced(100m, igstPercent: 18m)));
        Assert.Equal(18m, ProductPricing.GstRate(TestPrices.Priced(100m, cgstPercent: 18m)));
        Assert.Equal(9m, ProductPricing.GstRate(TestPrices.Priced(100m, cgstPercent: 9m, igstPercent: 18m)));
        Assert.Equal(0m, ProductPricing.GstRate(TestPrices.Priced(100m)));
        Assert.Equal(0m, ProductPricing.GstAmount(TestPrices.Priced(100m)));

        // And the tax on a unit is that rate applied to what is left after the discounts, taken to the rupee the
        // same way the price it is part of is.
        var price = TestPrices.Priced(200m, discountPercent: 10m, cgstPercent: 9m, sgstPercent: 9m);

        Assert.Equal(180m, ProductPricing.TaxableValue(price));
        Assert.Equal(32m, ProductPricing.GstAmount(price));
        Assert.Equal(212m, ProductPricing.ListingPrice(price));
    }

    /// <summary>
    /// The price is the value the tax was charged on plus that tax, and nothing else: a listing showing a figure
    /// made up any other way would be a price the checkout does not charge.
    /// </summary>
    [Fact]
    public void ThePriceIsTheTaxableValuePlusTheTaxOnIt()
    {
        var price = TestPrices.Priced(
            899.50m,
            discountPercent: 20m,
            additionalDiscountPercent: 5m,
            profitMarginPercent: 12.5m,
            packagingCharge: 25m,
            cgstPercent: 6m,
            sgstPercent: 6m);

        var taxable = ProductPricing.TaxableValue(price);

        // 899.50 plus a 12.5% margin plus the 25.00 of packaging is 1,036.9375, taken up to 1,037.00; 20% off that
        // is 207.40 (207.00) and 5% of the 830.00 left is 41.50 (42.00, up), which leaves 788.00.
        Assert.Equal(1037m, ProductPricing.Gross(price));
        Assert.Equal(788m, ProductPricing.Gross(price) - ProductPricing.TotalDiscount(price));
        Assert.Equal(788m, taxable);

        // The tax is the multiplication at the rate and the price is the value with it on: 12% of 788.00 is 94.56,
        // which a till takes up to 95.00 - the figure inside the price below.
        Assert.Equal(94.56m, OrderMoney.GstOn(taxable, 12m));
        Assert.Equal(95m, ProductPricing.GstAmount(price));
        Assert.Equal(taxable + ProductPricing.GstAmount(price), ProductPricing.ListingPrice(price));
        Assert.Equal(883m, ProductPricing.ListingPrice(price));
    }

    /// <summary>
    /// A product and an order line carry the same ten fields, so a unit is priced from what it is or from what it
    /// was: a line read from its own snapshot is charged what it was sold for, whatever the product says today.
    /// </summary>
    [Fact]
    public void AProductAndItsOrderLineArePricedTheSameWay()
    {
        var product = new Product
        {
            UnitPrice = 200m,
            DiscountPercent = 10m,
            AdditionalDiscountPercent = 5m,
            ProfitMarginPercent = 25m,
            PackagingCharge = 10m,
            StorageCharge = 5m,
            DeliveryCharge = 15m,
            Cgstpercent = 9m,
            Sgstpercent = 9m,
            Igstpercent = 18m
        };

        var line = new OrderItem
        {
            UnitPrice = product.UnitPrice,
            DiscountPercent = product.DiscountPercent,
            AdditionalDiscountPercent = product.AdditionalDiscountPercent,
            ProfitMarginPercent = product.ProfitMarginPercent,
            PackagingCharge = product.PackagingCharge,
            StorageCharge = product.StorageCharge,
            DeliveryCharge = product.DeliveryCharge,
            Cgstpercent = product.Cgstpercent,
            Sgstpercent = product.Sgstpercent,
            Igstpercent = product.Igstpercent
        };

        Assert.Equal(ProductPricing.Of(product), ProductPricing.Of(line));
        Assert.Equal(282m, ProductPricing.ListingPrice(ProductPricing.Of(product)));
        Assert.Equal(ProductPricing.ListingPrice(ProductPricing.Of(product)), ProductPricing.ChargedPrice(line));
    }

    /// <summary>
    /// An order's lines are charged at the listing price of the day, one line per physical unit, and they add up to
    /// what the customer paid: the figure the gateway was asked for, the figure a retry asks for again, and the
    /// figure a full refund gives back.
    /// </summary>
    [Fact]
    public void AnOrdersLinesAddUpToWhatWasChargedForThem()
    {
        var first = new OrderItem { UnitPrice = 200m, Cgstpercent = 9m, Sgstpercent = 9m };
        var second = new OrderItem { UnitPrice = 100m, ProfitMarginPercent = 20m, Igstpercent = 18m };

        // 200.00 of goods with 18% on them (36.00), and 120.00 of goods - a 20% margin - with 18% on them: 21.60,
        // which a till takes up to 22.00.
        Assert.Equal(236m, ProductPricing.ChargedPrice(first));
        Assert.Equal(142m, ProductPricing.ChargedPrice(second));
        Assert.Equal(378m, ProductPricing.ChargedTotal(new[] { first, second }));

        // The same unit twice is charged twice, and no lines are charged nothing.
        Assert.Equal(472m, ProductPricing.ChargedTotal(new[] { first, first }));
        Assert.Equal(0m, ProductPricing.ChargedTotal(Array.Empty<OrderItem>()));
    }

    /// <summary>
    /// The database-side reading of the rule IS the rule. EF Core cannot call a method inside a query, so
    /// ChargedPriceExpression spells the same arithmetic out over the lines' own columns for the screens that add an
    /// order's money up in SQL - and this pins the two to each other over a spread of lines: one taxed inside the
    /// state, one across it, one with no tax at all, one discounted and charged, and one carrying every field at
    /// once with a price that does not divide evenly.
    /// </summary>
    [Fact]
    public void TheDatabaseSideReadingOfTheRuleIsTheRule()
    {
        var compiled = ProductPricing.ChargedPriceExpression.Compile();

        var lines = new[]
        {
            new OrderItem { UnitPrice = 200m, Cgstpercent = 9m, Sgstpercent = 9m },
            new OrderItem { UnitPrice = 100m, Igstpercent = 18m },
            new OrderItem { UnitPrice = 100m, DiscountPercent = 10m },
            new OrderItem
            {
                UnitPrice = 899.50m,
                DiscountPercent = 20m,
                AdditionalDiscountPercent = 5m,
                ProfitMarginPercent = 12.5m,
                PackagingCharge = 25m,
                StorageCharge = 7.25m,
                DeliveryCharge = 40m,
                Cgstpercent = 6m,
                Sgstpercent = 6m,
                Igstpercent = 12m
            },
            new OrderItem { UnitPrice = 0m, Cgstpercent = 18m },
            new OrderItem { UnitPrice = 1450.75m, ProfitMarginPercent = 32.5m, DeliveryCharge = 99m, Igstpercent = 5m },

            // Two figures sitting exactly on half a rupee, one each side of zero: the half goes UP, away from zero,
            // and the tree's two branches have to say what the members say (see ChargedPriceExpression - it is
            // written as FLOOR / CEILING rather than one rounding call). A tree that took the positive half down or
            // the negative half up would agree with the members on every line above and still charge the wrong rupee.
            new OrderItem { UnitPrice = 12.50m },
            new OrderItem { UnitPrice = -12.50m }
        };

        foreach (var line in lines)
            Assert.Equal(ProductPricing.ChargedPrice(line), compiled(line));
    }

    /// <summary>
    /// The database-side reading is one EF can actually turn into SQL, and it reads every field of both sections: the
    /// admin order list and a customer's lifetime spend add an order's money up IN THE DATABASE, so a figure missing
    /// from the tree - or arithmetic EF cannot translate - would take those screens down, and no unit of the
    /// arithmetic above would catch it.
    ///
    /// The query is translated, never run: a provider has to be named for a tree to become SQL, and nothing here
    /// opens a connection with it (the same way the model tests beside this one read the schema).
    /// </summary>
    [Fact]
    public void TheDatabaseSideReadingCanBeTranslatedToSql()
    {
        using var context = new HomecutiesDbContext(new DbContextOptionsBuilder<HomecutiesDbContext>()
            .UseSqlServer("Server=none;Database=none;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);

        // The query the screens write: a Sum over the lines, which is the shape a translation has to survive. EF is
        // free to evaluate arithmetic in memory inside a plain Select - and does, silently - but it cannot finish an
        // aggregate on the client, so anything it cannot translate throws here instead of being quietly handed to
        // .NET. That is the failure this pins: the tree this test was written for read back raw columns in a Select
        // and answered with a raw, unrounded figure, and threw the moment the admin order list summed it.
        var sql = context.Orders
            .Where(order => order.OrderId == 1)
            .Select(order => context.OrderItems
                .Where(line => line.OrderId == order.OrderId)
                .Sum(ProductPricing.ChargedPriceExpression))
            .ToQueryString();

        // The lines are added up by the database, and every figure of each one is taken to the rupee by the database
        // as well (FLOOR / CEILING, see ChargedPriceExpression) rather than handed back with its paise on it.
        Assert.Contains("SUM(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FLOOR(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CEILING(", sql, StringComparison.OrdinalIgnoreCase);

        // And the tree reads every field of both sections of the product form. A figure left out of it is a figure
        // the database-side reading does not have.
        foreach (var column in new[]
                 {
                     nameof(OrderItem.UnitPrice),
                     nameof(OrderItem.ProfitMarginPercent),
                     nameof(OrderItem.PackagingCharge),
                     nameof(OrderItem.StorageCharge),
                     nameof(OrderItem.DeliveryCharge),
                     nameof(OrderItem.DiscountPercent),
                     nameof(OrderItem.AdditionalDiscountPercent),
                     nameof(OrderItem.Cgstpercent),
                     nameof(OrderItem.Sgstpercent),
                     nameof(OrderItem.Igstpercent)
                 })
        {
            Assert.Contains(column, sql, StringComparison.OrdinalIgnoreCase);
        }
    }
}
