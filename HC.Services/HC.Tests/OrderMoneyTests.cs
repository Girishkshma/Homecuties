using HC.Business;
using Xunit;

namespace HC.Tests;

/// <summary>
/// The money rules the shop's screens are built on (see HC.Business.OrderMoney): which payment rows hold money
/// the gateway took, the day a sale belongs to, when a refund counts as gone, the period a report covers, what a
/// line was worth to the customer (the product's own pricing rule, see ProductPricing), one partner's share of a
/// shared order's money, and the tax the shop is holding for the government.
///
/// These are pinned here because three screens read them - the dashboard's revenue tiles, the Finance screen and
/// the order screen's margin card - and a change to any of them moves money figures a shop keeper reads as fact.
/// </summary>
public class OrderMoneyTests
{
    /// <summary>The day the period rules are pinned against, so a test never depends on when it is run.</summary>
    private static readonly DateOnly Today = new(2026, 4, 17);

    /// <summary>
    /// Money is taken in four states of a payment row - a capture, and the three a refund can leave it in - and
    /// in no others. An authorisation is not a capture (the money is not ours yet), a failed attempt took
    /// nothing, and a row with no status at all is not money either.
    /// </summary>
    [Fact]
    public void MoneyIsTakenInTheCaptureStatesAndNoOthers()
    {
        Assert.True(OrderMoney.TookMoney(OrderPaymentStatus.Captured));
        Assert.True(OrderMoney.TookMoney(OrderPaymentStatus.RefundRequested));
        Assert.True(OrderMoney.TookMoney(OrderPaymentStatus.RefundFailed));
        Assert.True(OrderMoney.TookMoney(OrderPaymentStatus.Refunded));

        Assert.False(OrderMoney.TookMoney(OrderPaymentStatus.Created));
        Assert.False(OrderMoney.TookMoney(OrderPaymentStatus.Authorized));
        Assert.False(OrderMoney.TookMoney(OrderPaymentStatus.Failed));
        Assert.False(OrderMoney.TookMoney(null));
        Assert.False(OrderMoney.TookMoney("captured"));

        Assert.Equal(4, OrderMoney.TakenStatuses.Length);
    }

    /// <summary>
    /// A sale belongs to the day the gateway took the money - the day the customer paid - and the gateway's own
    /// <c>created_at</c> is the better answer when the capture reported one (a payment recorded late, or a
    /// webhook replayed the next day, must still land on its own day). The day the row was written is the
    /// fallback for a payment whose capture never said.
    /// </summary>
    [Fact]
    public void ASaleBelongsToTheDayTheGatewayTookTheMoney()
    {
        var chargedOn = new DateTime(2026, 4, 10, 18, 30, 0, DateTimeKind.Utc);
        var writtenOn = new DateTime(2026, 4, 12, 6, 0, 0, DateTimeKind.Utc);

        Assert.Equal(chargedOn, OrderMoney.TakenOn(chargedOn, writtenOn));
        Assert.Equal(writtenOn, OrderMoney.TakenOn(null, writtenOn));
    }

    /// <summary>
    /// A refund counts as gone once it has been SENT (Razorpay created it, for a figure): it has left the shop's
    /// account even while it is still travelling. A refund that has only been asked for has not moved yet - that
    /// is the money the shop still holds and owes, which the 'Refund due' tile is for - and a row with no
    /// figure on it has nothing to take off.
    /// </summary>
    [Fact]
    public void ARefundCountsAsGoneOnceItHasBeenSent()
    {
        var sentOn = new DateTime(2026, 4, 15, 9, 0, 0, DateTimeKind.Utc);

        Assert.True(OrderMoney.WasGivenBack(sentOn, 500m));

        Assert.False(OrderMoney.WasGivenBack(null, 500m));
        Assert.False(OrderMoney.WasGivenBack(sentOn, null));
        Assert.False(OrderMoney.WasGivenBack(sentOn, 0m));
    }

    /// <summary>
    /// What the shop keeps is one subtraction wherever it is asked: a fully refunded day nets to nothing, a day
    /// with no refunds keeps everything, and a refund of an older sale makes the day it went out negative -
    /// which is what the bank statement of that day says too.
    /// </summary>
    [Fact]
    public void WhatTheShopKeepsIsWhatCameInLessWhatWentBack()
    {
        Assert.Equal(500m, OrderMoney.Net(500m, 0m));
        Assert.Equal(0m, OrderMoney.Net(500m, 500m));
        Assert.Equal(-200m, OrderMoney.Net(300m, 500m));
    }

    /// <summary>
    /// The margin is the share of what the customer paid that the shop declared on the product line: a 25% line
    /// of 200.00 has made 50.00 and cost 150.00. The two always add up to the line, at any percentage - which is
    /// what makes the 'declared cost' on the Finance screen something the shop can check against its own figures.
    /// </summary>
    [Fact]
    public void TheDeclaredMarginIsAShareOfWhatTheCustomerPaid()
    {
        Assert.Equal(50m, OrderMoney.DeclaredProfit(200m, 25m));
        Assert.Equal(150m, OrderMoney.DeclaredCost(200m, 25m));

        // A line with no margin declared earns nothing and costs everything - never a negative cost.
        Assert.Equal(0m, OrderMoney.DeclaredProfit(199.99m, 0m));
        Assert.Equal(199.99m, OrderMoney.DeclaredCost(199.99m, 0m));

        const decimal price = 1450.75m;
        const decimal percent = 32.5m;

        Assert.Equal(price, OrderMoney.DeclaredProfit(price, percent) + OrderMoney.DeclaredCost(price, percent));
    }

    /// <summary>
    /// The three figures the money screens read a sale back with - the value its tax was charged on, the rate it was
    /// charged at, and what the unit was worth with the tax in it - are the product's OWN pricing rule under the
    /// names the order side has always used them by (see ProductPricing): the unit price, the shop's margin, its
    /// packaging / storage / delivery charges and both discounts, taxed at the rate the 'Taxes' section records.
    ///
    /// Pinned as a TIE rather than as figures, because that is the invariant: a report reading a different
    /// composition from the one the checkout charged would be tax the shop never collected, which is the quietest
    /// way for these books to be wrong.
    /// </summary>
    [Fact]
    public void TheMoneyScreensReadASaleWithTheOnePricingRule()
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

        Assert.Equal(ProductPricing.TaxableValue(price), OrderMoney.TaxableValue(price));
        Assert.Equal(ProductPricing.GstRate(price), OrderMoney.ChargedGstRate(price));
        Assert.Equal(ProductPricing.ListingPrice(price), OrderMoney.LineValue(price));

        // What the customer paid for the unit: the value the tax was charged on, plus the tax on it - each figure
        // taken to the rupee as the price is built (ProductPricing.Rupees), so the two really do come to the figure
        // the gateway was asked for rather than to a fraction of a rupee beside it.
        Assert.Equal(282m, OrderMoney.LineValue(price));
        Assert.Equal(
            OrderMoney.TaxableValue(price) +
                ProductPricing.Rupees(OrderMoney.GstOn(OrderMoney.TaxableValue(price), OrderMoney.ChargedGstRate(price))),
            OrderMoney.LineValue(price));
    }

    /// <summary>
    /// The tax on a value is one multiplication by the rate - the one multiplication every GST figure these screens
    /// show is built from (see ProductPricing.GstAmount, which is it applied to a unit's taxable value) - and a zero
    /// on either side is a zero: never a guess, never a default rate.
    /// </summary>
    [Fact]
    public void TaxIsTheTaxableValueAtTheRate()
    {
        Assert.Equal(36m, OrderMoney.GstOn(200m, 18m));
        Assert.Equal(32.40m, OrderMoney.GstOn(180m, 18m));
        Assert.Equal(0m, OrderMoney.GstOn(200m, 0m));
        Assert.Equal(0m, OrderMoney.GstOn(0m, 18m));

        var price = TestPrices.Priced(899.50m, discountPercent: 20m, additionalDiscountPercent: 5m);
        var taxable = OrderMoney.TaxableValue(price);
        var tax = OrderMoney.GstOn(taxable, 12m);

        // 899.50 of goods is 900.00 to the rupee, 20% off that is 180.00 and 5% of the 720.00 left is 36.00, so the
        // value the tax falls on is a whole figure already. The multiplication below is the one unrounded figure in
        // the file - by design, since it is the reading a rate is applied with, not a price: 12% of 684.00 is 82.08,
        // and 82.00 the moment it is a figure a price is made of (ProductPricing.Rupees, which is what the tax
        // inside a listing price has been through).
        Assert.Equal(684m, taxable);
        Assert.Equal(82.08m, tax);
        Assert.Equal(82m, ProductPricing.Rupees(tax));

        // What the customer paid: the value and the tax of it, added up. Each taken to the rupee, that is a whole
        // figure a bill can show - 684.00 + 82.00 = 766.00 - where the raw multiplication would leave 8 paise beside
        // it.
        Assert.Equal(766.08m, decimal.Round(taxable * (1m + 12m / 100m), 2));
        Assert.Equal(766m, taxable + ProductPricing.Rupees(tax));
    }

    /// <summary>
    /// A refund gives the tax back with the goods, in the same proportion it gives the money back: a full refund
    /// gives back everything its order held, half a refund half of it. An order that charged no tax has none to give
    /// back, and neither has a refund of nothing or an order whose charge is unknown - those are zero rather than a
    /// division by zero.
    /// </summary>
    [Fact]
    public void ARefundGivesBackItsOwnShareOfTheTax()
    {
        // An order charged 2,124.00 of which 324.00 was tax.
        Assert.Equal(324m, OrderMoney.GstGivenBack(2124m, 324m, 2124m));
        Assert.Equal(162m, OrderMoney.GstGivenBack(1062m, 324m, 2124m));
        Assert.Equal(0.15254m, decimal.Round(OrderMoney.GstGivenBack(1m, 324m, 2124m), 5));

        Assert.Equal(0m, OrderMoney.GstGivenBack(500m, 0m, 2124m));
        Assert.Equal(0m, OrderMoney.GstGivenBack(0m, 324m, 2124m));
        Assert.Equal(0m, OrderMoney.GstGivenBack(500m, 324m, 0m));
    }

    /// <summary>
    /// A share of an amount is the same fraction of it that the slice is of the whole - the rule every partner's
    /// figure in the books is cut by. A slice that is the whole is the whole, a slice of nothing is nothing, and a
    /// whole of nothing is zero rather than a division by zero.
    /// </summary>
    [Fact]
    public void AShareOfAnAmountIsTheFractionOfItTheSliceIs()
    {
        // An order charged 1,000.00 that held two partners' goods: one partner's lines worth 400, the other's 600.
        Assert.Equal(400m, OrderMoney.Share(1000m, 400m, 1000m));
        Assert.Equal(600m, OrderMoney.Share(1000m, 600m, 1000m));

        // The same slice of a smaller amount - a charge the gateway took for that order.
        Assert.Equal(100m, OrderMoney.Share(250m, 400m, 1000m));

        Assert.Equal(0m, OrderMoney.Share(1000m, 0m, 1000m));
        Assert.Equal(0m, OrderMoney.Share(1000m, 400m, 0m));
        Assert.Equal(0m, OrderMoney.Share(0m, 400m, 1000m));
    }

    /// <summary>
    /// Two partners' shares of one order add back up to the order's own money, which is what lets a partner's screen
    /// hold only their own sales while the two screens between them still account for every rupee the customer paid.
    /// A charge the gateway took is the ORDER's, so it is cut the same way as the money it was charged on.
    /// </summary>
    [Fact]
    public void TheSharesOfOneOrderAddUpToTheOrder()
    {
        // 1,000.00 taken for an order whose lines were worth 400 and 600, with 20 charged for taking it.
        Assert.Equal(1000m, OrderMoney.Share(1000m, 400m, 1000m) + OrderMoney.Share(1000m, 600m, 1000m));
        Assert.Equal(20m, OrderMoney.Share(20m, 400m, 1000m) + OrderMoney.Share(20m, 600m, 1000m));
    }

    /// <summary>
    /// A settlement line that took money out - a refund or a chargeback the bank deducted - shares out negatively,
    /// so a partner's slice of one reduces their bank side instead of adding to it.
    /// </summary>
    [Fact]
    public void AMoneyOutLineSharesOutNegatively()
    {
        Assert.Equal(-40m, OrderMoney.Share(-100m, 400m, 1000m));
    }

    /// <summary>
    /// A partner's part of a shared order's money is their own lines' worth against the whole order's - the
    /// apportionment every figure of their books is read by. What the customer paid, the gateway's charge for taking
    /// it and the courier's bill are all recorded against the ORDER, so all of them are cut the same way, and the two
    /// partners' parts add back up to the whole.
    /// </summary>
    [Fact]
    public void APartnersShareOfASharedOrderIsTheirOwnLinesWorth()
    {
        // One order, two partners' goods: a line of 400.00 and one of 600.00, each charged at 9% tax.
        var mine = OrderMoney.LineValue(TestPrices.Priced(400m, cgstPercent: 9m));
        var theirs = OrderMoney.LineValue(TestPrices.Priced(600m, cgstPercent: 9m));
        var order = mine + theirs;

        // 1,200.00 was taken for it and the gateway kept 24.00 for taking it (the courier billed 30.00).
        var paid = 1200m;
        var gatewayFee = 24m;
        var courierBill = 30m;

        var myPart = OrderMoney.Share(paid, mine, order);
        var theirPart = OrderMoney.Share(paid, theirs, order);

        Assert.Equal(paid, myPart + theirPart);
        Assert.Equal(gatewayFee, OrderMoney.Share(gatewayFee, mine, order) + OrderMoney.Share(gatewayFee, theirs, order));
        Assert.Equal(courierBill, OrderMoney.Share(courierBill, mine, order) + OrderMoney.Share(courierBill, theirs, order));

        // The share is the lines' worth as a fraction of the order, not a figure of its own.
        Assert.Equal(paid * mine / order, myPart);
    }

    /// <summary>
    /// An order that carried one partner's goods only is entirely theirs: their lines are the whole order, so their
    /// share of each of its figures is the figure itself. (The stock is taken from whichever partner has it, so a
    /// shared order is possible and an order of one partner's goods is the usual case - both are the same rule.)
    /// </summary>
    [Fact]
    public void AnOrderOfOnePartnersGoodsIsEntirelyTheirs()
    {
        var mine = OrderMoney.LineValue(TestPrices.Priced(400m, cgstPercent: 9m));

        Assert.Equal(1200m, OrderMoney.Share(1200m, mine, mine));
        Assert.Equal(24m, OrderMoney.Share(24m, mine, mine));
    }

    /// <summary>
    /// The tax a refund gives back is that same rule under a name that says what it gives back, so a change to the
    /// division is a change to both - and the refund's own guard (a refund of nothing gives nothing back) stays
    /// where it is.
    /// </summary>
    [Fact]
    public void TheTaxARefundGivesBackIsTheSameRuleAsAnyOtherShare()
    {
        Assert.Equal(OrderMoney.Share(1062m, 324m, 2124m), OrderMoney.GstGivenBack(1062m, 324m, 2124m));
        Assert.Equal(162m, OrderMoney.GstGivenBack(1062m, 324m, 2124m));

        Assert.Equal(0m, OrderMoney.GstGivenBack(0m, 324m, 2124m));
        Assert.Equal(0m, OrderMoney.GstGivenBack(500m, 0m, 2124m));
        Assert.Equal(0m, OrderMoney.GstGivenBack(500m, 324m, 0m));
    }

    /// <summary>
    /// What the shop owes is what it holds once the tax its refunds gave back is off, less the tax it can set
    /// against it. A period whose credit is larger owes nothing and carries the difference forward, so the answer is
    /// allowed to be negative and is never floored at zero.
    /// </summary>
    [Fact]
    public void WhatIsOwedIsWhatIsHeldLessTheCredit()
    {
        Assert.Equal(324m, OrderMoney.GstHeld(500m, 176m));
        Assert.Equal(500m, OrderMoney.GstHeld(500m, 0m));

        Assert.Equal(274m, OrderMoney.NetGstPayable(OrderMoney.GstHeld(500m, 176m), 50m));
        Assert.Equal(-100m, OrderMoney.NetGstPayable(OrderMoney.GstHeld(500m, 0m), 600m));
    }

    /// <summary>
    /// Nothing asked for is the month to date - the first of the month the far end is in, up to today - which is
    /// the same stretch the dashboard's monthly tile reports, so opening the Finance screen against the dashboard
    /// cannot show two different months.
    /// </summary>
    [Fact]
    public void NothingAskedForIsTheMonthToDate()
    {
        var (from, to) = OrderMoney.ResolvePeriod(null, null, Today);

        Assert.Equal(new DateOnly(2026, 4, 1), from);
        Assert.Equal(Today, to);
    }

    /// <summary>
    /// A period asked for has its far end honoured: one day is one day, a far end only is the month that day is
    /// in, and a near end only runs to today. A far end in the future is pulled back to today, because nothing
    /// has been taken tomorrow.
    /// </summary>
    [Fact]
    public void APeriodAskedForIsReadAsItWasAsked()
    {
        var oneDay = OrderMoney.ResolvePeriod(new DateOnly(2026, 2, 10), new DateOnly(2026, 2, 10), Today);
        Assert.Equal(new DateOnly(2026, 2, 10), oneDay.From);
        Assert.Equal(new DateOnly(2026, 2, 10), oneDay.To);

        var farEndOnly = OrderMoney.ResolvePeriod(null, new DateOnly(2026, 2, 10), Today);
        Assert.Equal(new DateOnly(2026, 2, 1), farEndOnly.From);
        Assert.Equal(new DateOnly(2026, 2, 10), farEndOnly.To);

        var nearEndOnly = OrderMoney.ResolvePeriod(new DateOnly(2026, 1, 5), null, Today);
        Assert.Equal(new DateOnly(2026, 1, 5), nearEndOnly.From);
        Assert.Equal(Today, nearEndOnly.To);

        var inTheFuture = OrderMoney.ResolvePeriod(null, Today.AddDays(10), Today);
        Assert.Equal(new DateOnly(2026, 4, 1), inTheFuture.From);
        Assert.Equal(Today, inTheFuture.To);
    }

    /// <summary>
    /// A period that runs backwards is read as the single day it names - the same rule the settlement pull's
    /// window resolves by - so a reversed pair of date boxes shows a day rather than an empty screen.
    /// </summary>
    [Fact]
    public void APeriodThatRunsBackwardsIsTheSingleDayItNames()
    {
        var (from, to) = OrderMoney.ResolvePeriod(new DateOnly(2026, 4, 20), new DateOnly(2026, 4, 10), Today);

        Assert.Equal(new DateOnly(2026, 4, 10), from);
        Assert.Equal(new DateOnly(2026, 4, 10), to);
    }

    /// <summary>
    /// A period wider than a year is trimmed at its far end, so a report can never be asked to read every row the
    /// shop has ever taken: the recent days are what a shop keeper is looking at, and an older stretch is read a
    /// year at a time.
    /// </summary>
    [Fact]
    public void APeriodWiderThanAYearIsTrimmedAtItsNearEnd()
    {
        var (from, to) = OrderMoney.ResolvePeriod(new DateOnly(2020, 1, 1), Today, Today);

        Assert.Equal(Today.AddDays(-(OrderMoney.MaxPeriodDays - 1)), from);
        Assert.Equal(Today, to);
        Assert.Equal(OrderMoney.MaxPeriodDays, to.DayNumber - from.DayNumber + 1);
    }

    /// <summary>
    /// An order-level figure is split across the order's lines by what each line was worth - the same weight a
    /// partner's share has always been taken by - so the line that was half the order carries half of the fee the
    /// gateway kept for taking it. A weight of nothing is charged nothing: a line that was given away did not
    /// bring the fee in.
    /// </summary>
    [Fact]
    public void AnOrderLevelFigureIsSplitByWhatEachLineWasWorth()
    {
        var parts = OrderMoney.Apportion(100m, new[] { 60m, 40m });

        Assert.Equal(new[] { 60.00m, 40.00m }, parts);

        var withAFreeLine = OrderMoney.Apportion(100m, new[] { 100m, 0m });

        Assert.Equal(new[] { 100.00m, 0.00m }, withAFreeLine);
    }

    /// <summary>
    /// The parts of a split always add up to the whole they came from, to the paisa - which is the only reason this
    /// rule exists rather than a plain division. The odd paise are given to the largest remainders, so an even split
    /// of a figure that does not divide evenly puts them in the first lines (in order, so the same order always
    /// splits the same way) rather than losing them.
    /// </summary>
    [Fact]
    public void ThePartsOfASplitAlwaysAddUpToTheWhole()
    {
        var threeWays = OrderMoney.Apportion(100m, new[] { 1m, 1m, 1m });

        Assert.Equal(new[] { 33.34m, 33.33m, 33.33m }, threeWays);
        Assert.Equal(100m, threeWays.Sum());

        // A real one: the GST-bearing fee on an order of three differently-priced lines, which is where the
        // fractions really land.
        var fee = OrderMoney.Apportion(2.36m, new[] { 199m, 199m, 199m });

        Assert.Equal(new[] { 0.79m, 0.79m, 0.78m }, fee);
        Assert.Equal(2.36m, fee.Sum());

        // Nothing to split is nothing per line, not an error.
        var nothing = OrderMoney.Apportion(0m, new[] { 5m, 5m });

        Assert.Equal(new[] { 0.00m, 0.00m }, nothing);

        Assert.Empty(OrderMoney.Apportion(10m, Array.Empty<decimal>()));
    }

    /// <summary>
    /// An order whose lines were all worth nothing still has a fee to carry, so the units share it equally rather
    /// than the figure being dropped - a shop that gave goods away still paid the gateway for taking the money.
    /// </summary>
    [Fact]
    public void AnAmountWithNoLineWorthSplittingByIsSharedEqually()
    {
        var parts = OrderMoney.Apportion(10m, new[] { 0m, 0m, 0m });

        Assert.Equal(new[] { 3.34m, 3.33m, 3.33m }, parts);
        Assert.Equal(10m, parts.Sum());
    }

    /// <summary>
    /// A negative amount - a refund of a fee, a settlement line that took money out - is split in the same coins it
    /// was taken in: the parts are negative, and they still add up to the amount exactly.
    /// </summary>
    [Fact]
    public void ANegativeAmountIsSplitInTheSameCoinsItWasTakenIn()
    {
        var parts = OrderMoney.Apportion(-10m, new[] { 1m, 1m, 1m });

        Assert.Equal(new[] { -3.34m, -3.33m, -3.33m }, parts);
        Assert.Equal(-10m, parts.Sum());
    }
}
