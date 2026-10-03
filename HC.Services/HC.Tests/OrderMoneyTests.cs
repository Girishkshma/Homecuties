using HC.Business;
using Xunit;

namespace HC.Tests;

/// <summary>
/// The money rules the shop's screens are built on (see HC.Business.OrderMoney): which payment rows hold money
/// the gateway took, the day a sale belongs to, when a refund counts as gone, and the period a report covers.
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
}
