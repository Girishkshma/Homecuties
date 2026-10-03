using HC.Business;
using HC.Data.Entities;
using Xunit;

namespace HC.Tests;

/// <summary>
/// The gateway's own books: the lines of a settlement reconciliation report read the way Razorpay writes them,
/// the window a pull covers, what the authoritative charge does to a payment row, and what happens when the two
/// sources of the same figure disagree. Every rule here is one the Finance screen is built on, so it is pinned
/// without a gateway and without a database.
/// </summary>
public class RazorpaySettlementsTests
{
    /// <summary>
    /// The reconciliation report of 1 April 2025, as Razorpay hands it over: a 500.00 card payment it kept 11.80
    /// of (1.80 of that GST, so the shop keeps 486.40), a 500.00 refund taken back out of the same settlement,
    /// a 25.00 chargeback adjustment, and two items this code has to be able to walk past - one with no id of
    /// its own and one of a kind it does not read.
    /// </summary>
    private const string ReconReport = """
        {
          "entity": "collection",
          "count": 5,
          "items": [
            {
              "entity_id": "pay_29QQoUBi66xm2f",
              "type": "payment",
              "debit": 0,
              "credit": 48640,
              "amount": 50000,
              "currency": "INR",
              "fee": 1180,
              "tax": 180,
              "method": "card",
              "on_hold": false,
              "settled": true,
              "order_id": "order_9A33XWu170gUtm",
              "settlement_id": "setl_7M2oXqKTxTpvDl",
              "settlement_utr": "HDFCN5202504010001",
              "description": "Payment for order HC001001",
              "created_at": 1743465600,
              "settled_at": 1743552000
            },
            {
              "entity_id": "rfnd_29QQoUBi66xm2g",
              "type": "refund",
              "debit": 50000,
              "credit": 0,
              "amount": 50000,
              "currency": "INR",
              "method": "card",
              "settled": true,
              "payment_id": "pay_29QQoUBi66xm2f",
              "order_id": "order_9A33XWu170gUtm",
              "settlement_id": "setl_7M2oXqKTxTpvDl",
              "created_at": 1743638400,
              "settled_at": 1743642000
            },
            {
              "entity_id": "adj_0kR2vXqTzTpvDl",
              "type": "adjustment",
              "debit": 2500,
              "credit": 0,
              "amount": 2500,
              "dispute_id": "disp_0kR2vXqTzTpvDl",
              "settled": true,
              "settlement_id": "setl_7M2oXqKTxTpvDl",
              "created_at": 1743724800
            },
            {
              "entity_id": "",
              "type": "payment",
              "credit": 100
            },
            {
              "entity_id": "xyz_0kR2vXqTzTpvDl",
              "type": "chargeback",
              "credit": 100
            }
          ]
        }
        """;

    /// <summary>The report's lines, read the way the pull reads them.</summary>
    private static List<RazorpaySettlements.SettlementReconLine> Lines() =>
        RazorpaySettlements.ParseRecon(TestPayloads.Json(ReconReport));

    /// <summary>The report's payment line - the 500.00 card payment.</summary>
    private static RazorpaySettlements.SettlementReconLine PaymentLine() =>
        Lines().Single(line => line.IsPayment);

    /// <summary>The report's refund line - the 500.00 that went back.</summary>
    private static RazorpaySettlements.SettlementReconLine RefundLine() =>
        Lines().Single(line => line.IsRefund);

    /// <summary>
    /// A captured 500.00 payment as the payment row arrives at the pull: the capture reported a charge of its
    /// own (11.80 with 1.80 GST, i.e. <see cref="OrderPaymentCharges.FromPayment"/>), which the settlement's own
    /// figure is what corrects.
    /// </summary>
    private static OrderPayment CapturedPayment() => new()
    {
        PaymentId = 1,
        OrderId = 1001,
        Provider = OrderPayment.RazorpayProvider,
        RazorpayOrderId = "order_9A33XWu170gUtm",
        RazorpayPaymentId = "pay_29QQoUBi66xm2f",
        Status = OrderPaymentStatus.Captured,
        AmountInPaise = 50000,
        Amount = 500m,
        FeeAmountInPaise = 1180,
        FeeAmount = 11.80m,
        TaxAmountInPaise = 180,
        TaxAmount = 1.80m,
        NetAmount = 486.40m,
        ChargesSource = OrderPaymentCharges.FromPayment
    };

    /// <summary>
    /// A payment line is read whole: Razorpay's own id of the payment is both the line's entity id and the
    /// payment it belongs to (it leaves <c>payment_id</c> out on a payment line), the order it was taken against
    /// comes with it, and the money is kept in paise and in rupees beside it.
    /// </summary>
    [Fact]
    public void APaymentLineIsReadWhole()
    {
        var line = PaymentLine();

        Assert.Equal("pay_29QQoUBi66xm2f", line.EntityId);
        Assert.Equal("pay_29QQoUBi66xm2f", line.PaymentBehind);
        Assert.Equal("order_9A33XWu170gUtm", line.OrderId);
        Assert.Equal("setl_7M2oXqKTxTpvDl", line.SettlementId);
        Assert.Equal("HDFCN5202504010001", line.Utr);

        Assert.Equal(50000, line.AmountInPaise);
        Assert.Equal(500m, line.Amount);
        Assert.Equal(0m, line.Debit);
        Assert.Equal(486.40m, line.Credit);
        Assert.Equal(11.80m, line.FeeAmount);
        Assert.Equal(1.80m, line.TaxAmount);
        Assert.Equal(486.40m, line.NetAmount);
        Assert.Equal(486.40m, line.Effect);

        Assert.Equal("card", line.Method);
        Assert.True(line.Settled);
        Assert.False(line.OnHold);
        Assert.Equal(new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc), line.CreatedOn);
        Assert.Equal(new DateTime(2025, 4, 2, 0, 0, 0, DateTimeKind.Utc), line.SettledOn);

        Assert.Equal("Payment for order HC001001", line.Description);
        Assert.Null(line.DisputeId);
        Assert.Null(line.RefundId);
    }

    /// <summary>
    /// A refund line is money the settlement took out, and Razorpay names it by the refund rather than by the
    /// payment - so the refund id is the line's own entity id and the payment behind it comes from the field it
    /// does fill in. What it moves is negative, which is what a day's settlements add up to.
    /// </summary>
    [Fact]
    public void ARefundLineIsWhatTheSettlementTookOut()
    {
        var line = RefundLine();

        Assert.Equal("rfnd_29QQoUBi66xm2g", line.RefundId);
        Assert.Equal("pay_29QQoUBi66xm2f", line.PaymentBehind);
        Assert.Equal(500m, line.Debit);
        Assert.Equal(0m, line.Credit);
        Assert.Equal(-500m, line.Effect);
        Assert.Equal(-500m, line.NetAmount);

        // Only a payment line can answer the fee/GST question, so a refund is never counted against it.
        Assert.False(line.IsPayment);
        Assert.True(line.CreditAgreesWithFee);
    }

    /// <summary>A chargeback is money taken out of the settlement for a dispute, and carries the dispute it is.</summary>
    [Fact]
    public void AnAdjustmentLineIsMoneyTakenOutOfTheSettlement()
    {
        var line = Lines().Single(l => l.ItemType == RazorpaySettlements.ItemTypeAdjustment);

        Assert.Equal("disp_0kR2vXqTzTpvDl", line.DisputeId);
        Assert.Equal(-25m, line.Effect);
        Assert.False(line.IsPayment);
    }

    /// <summary>
    /// Items this code cannot read are walked past rather than stored: one with no id of its own cannot be
    /// matched to anything later, and one of a kind the ledger does not know would be a row no screen could make
    /// sense of. Everything readable is returned.
    /// </summary>
    [Fact]
    public void ItemsThatCannotBeReadAreSkippedRatherThanStored()
    {
        var lines = Lines();

        Assert.Equal(3, lines.Count);
        Assert.DoesNotContain(lines, l => l.ItemType == "chargeback");
        Assert.Contains(lines, l => l.ItemType == RazorpaySettlements.ItemTypePayment);
        Assert.Contains(lines, l => l.ItemType == RazorpaySettlements.ItemTypeRefund);
        Assert.Contains(lines, l => l.ItemType == RazorpaySettlements.ItemTypeAdjustment);
    }

    /// <summary>A payload that is not a collection reads as no lines at all - an error body, an empty answer.</summary>
    [Fact]
    public void APayloadThatIsNotACollectionReadsAsNoLines()
    {
        Assert.Empty(RazorpaySettlements.ParseRecon(
            TestPayloads.Json("""{ "entity": "collection", "count": 0 }""")));
        Assert.Empty(RazorpaySettlements.ParseRecon(TestPayloads.Json("""[]""")));
        Assert.Empty(RazorpaySettlements.ParseRecon(
            TestPayloads.Json("""{ "error": { "code": "BAD_REQUEST_ERROR" } }""")));
        Assert.Empty(RazorpaySettlements.ParseRecon(TestPayloads.Json("""{ "items": "not a collection" }""")));
    }

    /// <summary>
    /// The arithmetic that answers the GST question: Razorpay's own credit is exactly what amount - fee - GST
    /// works out to, so the fee it quotes is the charge WITHOUT the tax on it and the net columns here are right.
    /// </summary>
    [Fact]
    public void ACreditOfAmountMinusFeeMinusTaxSaysTheFeeExcludesTheGst()
    {
        Assert.True(PaymentLine().CreditAgreesWithFee);
    }

    /// <summary>
    /// And the other answer, which is the one that would change the arithmetic: a credit of amount - fee with a
    /// non-zero tax on the same line means the fee Razorpay quotes already has the GST inside it, and
    /// subtracting the tax again would take it twice. The line records both spellings and says which one its own
    /// settlement shows; the pull counts these lines and says so (see RazorpaySettlements.SyncAsync).
    /// </summary>
    [Fact]
    public void ACreditOfAmountMinusFeeWithTaxSaysTheFeeIncludesTheGst()
    {
        var line = RazorpaySettlements.ParseRecon(TestPayloads.Json("""
            {
              "items": [
                { "entity_id": "pay_a", "type": "payment", "amount": 50000, "fee": 1180, "tax": 180,
                  "credit": 48820 }
              ]
            }
            """)).Single();

        Assert.False(line.CreditAgreesWithFee);
    }

    /// <summary>Today, as the pull sees it, for the window rules below.</summary>
    private static readonly DateOnly Today = new(2025, 4, 10);

    /// <summary>
    /// Nothing asked for means the plain rolling window: yesterday and the seven days before it. Eight days is
    /// what a settlement that was created late, was put on hold or had its charges corrected after the fact
    /// needs, and it is what makes the pull safe to run every hour.
    /// </summary>
    [Fact]
    public void ThePlainWindowIsYesterdayAndTheWeekBeforeIt()
    {
        var (from, to) = RazorpaySettlements.ResolvePullDays(null, null, Today);

        Assert.Equal(new DateOnly(2025, 4, 2), from);
        Assert.Equal(new DateOnly(2025, 4, 9), to);
        Assert.Equal(RazorpaySettlements.LookbackDays, to.DayNumber - from.DayNumber + 1);
    }

    /// <summary>What a caller asks for is what it gets, as long as it is a window the gateway can answer.</summary>
    [Fact]
    public void TheWindowAskedForIsTheWindowPulled()
    {
        var (from, to) = RazorpaySettlements.ResolvePullDays(
            new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 5), Today);

        Assert.Equal(new DateOnly(2025, 3, 1), from);
        Assert.Equal(new DateOnly(2025, 3, 5), to);
    }

    /// <summary>
    /// A catch-up over a longer gap is trimmed at its far end, not its near one: the recent days are what the
    /// books need first and the rest is asked for in another pull. One pull is capped because every day is a
    /// request to the gateway.
    /// </summary>
    [Fact]
    public void AWindowWiderThanTheCapIsTrimmedAtItsFarEnd()
    {
        var (from, to) = RazorpaySettlements.ResolvePullDays(
            new DateOnly(2025, 1, 1), new DateOnly(2025, 4, 9), Today);

        Assert.Equal(new DateOnly(2025, 4, 9), to);
        Assert.Equal(new DateOnly(2025, 3, 10), from);
        Assert.Equal(RazorpaySettlements.MaxPullDays, to.DayNumber - from.DayNumber + 1);
    }

    /// <summary>
    /// The window never reaches past today (a settlement is made after the money moved, so there is nothing for
    /// tomorrow to report), and a window that runs backwards is read as the single day it names rather than as an
    /// empty pull.
    /// </summary>
    [Fact]
    public void TheWindowNeverReachesPastTodayAndNeverRunsBackwards()
    {
        var (from, to) = RazorpaySettlements.ResolvePullDays(null, new DateOnly(2025, 4, 20), Today);

        Assert.Equal(Today, to);
        Assert.Equal(new DateOnly(2025, 4, 3), from);

        var backwards = RazorpaySettlements.ResolvePullDays(
            new DateOnly(2025, 4, 9), new DateOnly(2025, 4, 2), Today);

        Assert.Equal(new DateOnly(2025, 4, 2), backwards.From);
        Assert.Equal(new DateOnly(2025, 4, 2), backwards.To);

        var oneDay = RazorpaySettlements.ResolvePullDays(
            new DateOnly(2025, 4, 5), new DateOnly(2025, 4, 5), Today);

        Assert.Equal(new DateOnly(2025, 4, 5), oneDay.From);
        Assert.Equal(new DateOnly(2025, 4, 5), oneDay.To);
    }

    /// <summary>
    /// The settlement's own figure is what the payment row ends up with: the gateway's charge and the GST on it
    /// are the ones the bank was settled on, what the shop keeps is worked out from them, and the row is marked
    /// as carrying the recon's word (OrderPaymentCharges.FromRecon) - which nothing may put back.
    /// </summary>
    [Fact]
    public void TheSettlementFigureIsWrittenOverTheCapturesEstimate()
    {
        var payment = CapturedPayment();

        // The capture's own estimate, which the settlement corrects: it guessed 12.00 where the fee was 11.80.
        payment.FeeAmountInPaise = 1200;
        payment.FeeAmount = 12m;
        payment.NetAmount = 486.20m;

        Assert.True(RazorpaySettlements.ApplyReconCharges(payment, PaymentLine()));

        Assert.Equal(1180, payment.FeeAmountInPaise);
        Assert.Equal(11.80m, payment.FeeAmount);
        Assert.Equal(180, payment.TaxAmountInPaise);
        Assert.Equal(1.80m, payment.TaxAmount);
        Assert.Equal(486.40m, payment.NetAmount);
        Assert.Equal(OrderPaymentCharges.FromRecon, payment.ChargesSource);
    }

    /// <summary>
    /// Applied twice, the second time says nothing changed. The pull runs hour after hour over the same days, and
    /// a bookkeeping write that reported news every run would be unreadable.
    /// </summary>
    [Fact]
    public void TheSettlementFigureIsNotTakenTwice()
    {
        var payment = CapturedPayment();

        Assert.True(RazorpaySettlements.ApplyReconCharges(payment, PaymentLine()));
        Assert.False(RazorpaySettlements.ApplyReconCharges(payment, PaymentLine()));
    }

    /// <summary>
    /// A recon line of a payment whose fee Razorpay has not worked out yet carries 0/0, and it must not blank out
    /// a figure the capture already reported - a charge written as nothing is a payment that looks free.
    /// </summary>
    [Fact]
    public void ALineWithNoChargeOnItDoesNotBlankOutWhatTheCaptureKnew()
    {
        var payment = CapturedPayment();

        var emptyLine = RazorpaySettlements.ParseRecon(TestPayloads.Json("""
            {
              "items": [
                { "entity_id": "pay_29QQoUBi66xm2f", "type": "payment", "amount": 50000, "credit": 50000,
                  "order_id": "order_9A33XWu170gUtm" }
              ]
            }
            """)).Single();

        Assert.False(RazorpaySettlements.ApplyReconCharges(payment, emptyLine));

        Assert.Equal(1180, payment.FeeAmountInPaise);
        Assert.Equal(11.80m, payment.FeeAmount);
        Assert.Equal(1.80m, payment.TaxAmount);
        Assert.Equal(486.40m, payment.NetAmount);
        Assert.Equal(OrderPaymentCharges.FromPayment, payment.ChargesSource);
    }

    /// <summary>
    /// Once a row carries the recon's charges, nothing here writes over them: a capture heard about afterwards
    /// (a replayed webhook, a status check) is the payment row's own business, not this one. What the shop keeps
    /// is still recalculated, because the payment's amount may have been corrected since.
    /// </summary>
    [Fact]
    public void TheReconsFigureIsFinalAndWhatTheShopKeepsIsStillRecalculated()
    {
        var payment = CapturedPayment();
        payment.ChargesSource = OrderPaymentCharges.FromRecon;
        payment.FeeAmountInPaise = 1180;
        payment.FeeAmount = 11.80m;
        payment.NetAmount = 100m;

        Assert.True(RazorpaySettlements.ApplyReconCharges(payment, PaymentLine()));

        Assert.Equal(1180, payment.FeeAmountInPaise);
        Assert.Equal(11.80m, payment.FeeAmount);
        Assert.Equal(486.40m, payment.NetAmount);
        Assert.Equal(OrderPaymentCharges.FromRecon, payment.ChargesSource);
    }

    /// <summary>
    /// The instrument and the day the money was taken are filled in from the line when the row does not know
    /// them - an order paid before those columns existed is completed by the pull. A row that already knows them
    /// keeps what it has.
    /// </summary>
    [Fact]
    public void TheMethodAndTheChargingDayAreFilledInWhenTheRowDoesNotKnowThem()
    {
        var payment = CapturedPayment();
        payment.PaymentMethod = null;
        payment.GatewayChargedOn = null;

        Assert.True(RazorpaySettlements.ApplyReconCharges(payment, PaymentLine()));

        Assert.Equal("card", payment.PaymentMethod);
        Assert.Equal(new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc), payment.GatewayChargedOn);

        var chargedOn = new DateTime(2025, 3, 20, 0, 0, 0, DateTimeKind.Utc);
        var known = CapturedPayment();
        known.PaymentMethod = "upi";
        known.GatewayChargedOn = chargedOn;

        RazorpaySettlements.ApplyReconCharges(known, PaymentLine());

        Assert.Equal("upi", known.PaymentMethod);
        Assert.Equal(chargedOn, known.GatewayChargedOn);
    }

    /// <summary>
    /// A line that does not describe the same money as the payment row is a finding for a person, not a
    /// correction: the pull counts it and names it rather than writing over either figure. A refund line, and a
    /// payment line that agrees, are not findings.
    /// </summary>
    [Fact]
    public void ALineThatDisagreesWithThePaymentRowIsAFinding()
    {
        var payment = CapturedPayment();

        Assert.False(RazorpaySettlements.AmountDisagrees(payment, PaymentLine()));
        Assert.False(RazorpaySettlements.AmountDisagrees(payment, RefundLine()));

        var partial = RazorpaySettlements.ParseRecon(TestPayloads.Json("""
            {
              "items": [
                { "entity_id": "pay_29QQoUBi66xm2f", "type": "payment", "amount": 25000, "credit": 24000,
                  "order_id": "order_9A33XWu170gUtm" }
              ]
            }
            """)).Single();

        Assert.True(RazorpaySettlements.AmountDisagrees(payment, partial));
    }

    /// <summary>
    /// A line is written down whole: what Razorpay said in paise and in rupees, the ids that trace it back to the
    /// order's payment, the day it was settled, and which day's report it was read from - pointed at the
    /// settlement it came in with.
    /// </summary>
    [Fact]
    public void ALineIsWrittenDownWhole()
    {
        var readOn = new DateTime(2025, 4, 10, 6, 0, 0, DateTimeKind.Utc);
        var item = RazorpaySettlements.FromReconLine(PaymentLine(), 42, readOn, readOn);

        Assert.Equal(42, item.SettlementId);
        Assert.Equal("pay_29QQoUBi66xm2f", item.RazorpayEntityId);
        Assert.Equal(RazorpaySettlements.ItemTypePayment, item.ItemType);
        Assert.Equal("pay_29QQoUBi66xm2f", item.RazorpayPaymentId);
        Assert.Equal("order_9A33XWu170gUtm", item.RazorpayOrderId);
        Assert.Null(item.RazorpayRefundId);
        Assert.Equal("setl_7M2oXqKTxTpvDl", item.RazorpaySettlementId);
        Assert.Equal("HDFCN5202504010001", item.SettlementUtr);

        Assert.Equal(50000, item.AmountInPaise);
        Assert.Equal(500m, item.Amount);
        Assert.Equal(48640, item.CreditInPaise);
        Assert.Equal(486.40m, item.Credit);
        Assert.Equal(0m, item.Debit);
        Assert.Equal(1180, item.FeeAmountInPaise);
        Assert.Equal(11.80m, item.FeeAmount);
        Assert.Equal(180, item.TaxAmountInPaise);
        Assert.Equal(1.80m, item.TaxAmount);
        Assert.Equal(486.40m, item.NetAmount);

        Assert.Equal("card", item.PaymentMethod);
        Assert.False(item.IsOnHold);
        Assert.True(item.IsSettled);
        Assert.Equal(new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc), item.GatewayCreatedOn);
        Assert.Equal(new DateTime(2025, 4, 2, 0, 0, 0, DateTimeKind.Utc), item.SettledOn);

        // The day the report was asked for, not the day the money moved.
        Assert.Equal(readOn.Date, item.ReconDay);
        Assert.Equal(readOn, item.CreatedOn);
    }

    /// <summary>
    /// Read again, the row says whether anything really changed: the same line on the next pull is not news, a
    /// figure Razorpay corrects later is, and a field the report stops carrying is not a way to blank a line that
    /// settled money - the day it settled and the note it came with stay as they were read.
    /// </summary>
    [Fact]
    public void ReadingALineAgainReportsWhatActuallyChanged()
    {
        var readOn = new DateTime(2025, 4, 10, 6, 0, 0, DateTimeKind.Utc);
        var item = RazorpaySettlements.FromReconLine(PaymentLine(), 42, readOn, readOn);

        // The same day read again a day later: the row already is what the line says.
        Assert.False(RazorpaySettlements.ApplyReconLine(item, PaymentLine(), 42, readOn.AddDays(1)));
        Assert.Equal(readOn.Date, item.ReconDay);

        // Razorpay correcting the charge it took, and the shop's share moving down with it.
        var corrected = RazorpaySettlements.ParseRecon(TestPayloads.Json("""
            {
              "items": [
                { "entity_id": "pay_29QQoUBi66xm2f", "type": "payment", "amount": 50000, "credit": 48460,
                  "fee": 1270, "tax": 180, "method": "card", "settled": true,
                  "order_id": "order_9A33XWu170gUtm" }
              ]
            }
            """)).Single();

        Assert.True(RazorpaySettlements.ApplyReconLine(item, corrected, 42, readOn.AddDays(2)));

        Assert.Equal(1270, item.FeeAmountInPaise);
        Assert.Equal(12.70m, item.FeeAmount);
        Assert.Equal(48460, item.CreditInPaise);
        Assert.Equal(484.60m, item.Credit);
        Assert.Equal(485.50m, item.NetAmount);

        // Nothing this report left out was blanked: the day the money left and the note stand.
        Assert.Equal(new DateTime(2025, 4, 2, 0, 0, 0, DateTimeKind.Utc), item.SettledOn);
        Assert.Equal("Payment for order HC001001", item.Description);
        Assert.Equal(readOn.Date, item.ReconDay);
    }

    /// <summary>
    /// A settlement of the settlement list is copied field by field onto the row the pull already has - the bank's
    /// reference, what was settled, the gateway's own fee and tax totals, the currency, and the status it has
    /// moved to. The day it settled is left alone: a settlement entity does not say it (its lines do, see the
    /// pull). Copied a second time, there is nothing left to write.
    /// </summary>
    [Fact]
    public void ASettlementIsCopiedFieldByFieldAndTheDayItSettledIsLeftAlone()
    {
        var settledOn = new DateTime(2025, 4, 2, 0, 0, 0, DateTimeKind.Utc);
        var createdOn = new DateTime(2025, 4, 1, 12, 0, 0, DateTimeKind.Utc);

        var known = new Settlement
        {
            SettlementId = 7,
            RazorpaySettlementId = "setl_7M2oXqKTxTpvDl",
            Utr = "HDFCN5202504010001",
            AmountInPaise = 486400,
            Amount = 4864.00m,
            FeesInPaise = 1180,
            Fees = 11.80m,
            TaxInPaise = 180,
            Tax = 1.80m,
            Currency = "INR",
            Status = RazorpaySettlements.StatusCreated,
            SettledOn = settledOn
        };

        var listed = new Settlement
        {
            RazorpaySettlementId = "setl_7M2oXqKTxTpvDl",
            Utr = "HDFCN5202504010001",
            AmountInPaise = 486400,
            Amount = 4864.00m,
            FeesInPaise = 1180,
            Fees = 11.80m,
            TaxInPaise = 180,
            Tax = 1.80m,
            Currency = "INR",
            Status = RazorpaySettlements.StatusProcessed,
            GatewayCreatedOn = createdOn
        };

        Assert.True(RazorpaySettlements.CopySettlement(known, listed));

        Assert.Equal(RazorpaySettlements.StatusProcessed, known.Status);
        Assert.Equal(createdOn, known.GatewayCreatedOn);
        Assert.Equal(4864.00m, known.Amount);
        Assert.Equal(settledOn, known.SettledOn);

        Assert.False(RazorpaySettlements.CopySettlement(known, listed));
        Assert.Equal(settledOn, known.SettledOn);
    }

    /// <summary>
    /// The one piece of bookkeeping every writer here shares: a value is written only when it really differs, and
    /// the write says whether it did. Every 'did anything change' answer above rests on it.
    /// </summary>
    [Fact]
    public void AValueIsOnlyWrittenWhenItDiffers()
    {
        var method = "card";

        Assert.False(RazorpaySettlements.Changed<string?>(method, "card", v => method = v));
        Assert.Equal("card", method);

        Assert.True(RazorpaySettlements.Changed<string?>(method, "upi", v => method = v));
        Assert.Equal("upi", method);

        decimal? fee = null;

        Assert.False(RazorpaySettlements.Changed<decimal?>(fee, null, v => fee = v));
        Assert.True(RazorpaySettlements.Changed<decimal?>(fee, 11.80m, v => fee = v));
        Assert.Equal(11.80m, fee);
    }

    /// <summary>
    /// When the rolling pull runs by itself (see SettlementSyncSchedule): a shop that has switched it off gets no
    /// pass at all, and anything else - an unset key included - leaves it running, because the books being current
    /// is the point of it and switching them off is the deliberate act.
    /// </summary>
    [Fact]
    public void TheRollingPullRunsUnlessConfigurationSaysFalse()
    {
        Assert.True(SettlementSyncSchedule.IsEnabled(null));
        Assert.True(SettlementSyncSchedule.IsEnabled(""));
        Assert.True(SettlementSyncSchedule.IsEnabled("true"));
        Assert.False(SettlementSyncSchedule.IsEnabled("false"));
        Assert.False(SettlementSyncSchedule.IsEnabled("FALSE"));
        Assert.False(SettlementSyncSchedule.IsEnabled(" false "));
    }

    /// <summary>
    /// How long the pull waits between two passes: an hour when nothing usable is set, what was asked for when it is
    /// a number, and never sooner than one pass is worth - a pass reads eight days off the gateway, so a shorter
    /// interval would be asking a question that was just answered.
    /// </summary>
    [Fact]
    public void TheIntervalIsAnHourByDefaultAndNeverShorterThanOnePassIsWorth()
    {
        Assert.Equal(60, SettlementSyncSchedule.IntervalMinutes(null));
        Assert.Equal(60, SettlementSyncSchedule.IntervalMinutes("tomorrow"));
        Assert.Equal(30, SettlementSyncSchedule.IntervalMinutes("30"));
        Assert.Equal(5, SettlementSyncSchedule.IntervalMinutes("5"));
        Assert.Equal(5, SettlementSyncSchedule.IntervalMinutes("1"));
        Assert.Equal(5, SettlementSyncSchedule.IntervalMinutes("0"));
        Assert.Equal(5, SettlementSyncSchedule.IntervalMinutes("-30"));
    }

    /// <summary>
    /// How long after a restart the first pass waits: a minute by default, and 0 is obeyed - the shop that wants the
    /// pull at the first moment the app is serving asked for exactly that - while a wait long enough to postpone the
    /// pull past the day it is for is trimmed to an hour.
    /// </summary>
    [Fact]
    public void TheFirstPassWaitsForTheAppToBeServingButNeverLongerThanAnHour()
    {
        Assert.Equal(60, SettlementSyncSchedule.StartupDelaySeconds(null));
        Assert.Equal(60, SettlementSyncSchedule.StartupDelaySeconds("in a bit"));
        Assert.Equal(0, SettlementSyncSchedule.StartupDelaySeconds("0"));
        Assert.Equal(90, SettlementSyncSchedule.StartupDelaySeconds("90"));
        Assert.Equal(3600, SettlementSyncSchedule.StartupDelaySeconds("86400"));
    }

    /// <summary>
    /// The three settings read together - what the job is handed: the defaults when configuration says nothing, and
    /// each value where it was set, in the units the timer works in.
    /// </summary>
    [Fact]
    public void TheJobIsHandedTheThreeSettingsAsTimes()
    {
        var unset = SettlementSyncSchedule.From(null, null, null);

        Assert.True(unset.Enabled);
        Assert.Equal(TimeSpan.FromHours(1), unset.Interval);
        Assert.Equal(TimeSpan.FromMinutes(1), unset.StartupDelay);

        var configured = SettlementSyncSchedule.From("false", "15", "0");

        Assert.False(configured.Enabled);
        Assert.Equal(TimeSpan.FromMinutes(15), configured.Interval);
        Assert.Equal(TimeSpan.Zero, configured.StartupDelay);
    }

    /// <summary>
    /// The one sentence a pull answers with - the admin screen's message and the scheduler's log line are both it:
    /// what was read and written, and, when there is one, the finding a person has to look at. A pull that found
    /// nothing wrong says so, so a green answer is never read as an empty one.
    /// </summary>
    [Fact]
    public void TheSummarySaysWhatThePullDidAndWhetherAnythingNeedsLookingAt()
    {
        var clean = new SettlementSyncOutcome(8, 12, 3, 0, 0, 0, 1, 0, Array.Empty<string>());

        Assert.Contains("8 day(s) read", clean.Summary);
        Assert.Contains("12 settlement line(s) written down", clean.Summary);
        Assert.Contains("3 payment(s) charged the settled figure", clean.Summary);
        Assert.Contains("1 refund(s) brought up to date", clean.Summary);
        Assert.EndsWith("nothing here needed a second look.", clean.Summary);
        Assert.DoesNotContain("with no row here", clean.Summary);

        var findings = new SettlementSyncOutcome(7, 4, 0, 2, 1, 3, 0, 1, Array.Empty<string>());

        Assert.Contains("2 payment(s) with no row here", findings.Summary);
        Assert.Contains("1 line(s) disagreeing with a payment row", findings.Summary);
        Assert.Contains("3 line(s) whose credit I cannot account for", findings.Summary);
        Assert.DoesNotContain("nothing here needed a second look", findings.Summary);
    }
}
