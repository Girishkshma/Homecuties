using HC.Business;
using HC.Data.Entities;
using Xunit;

namespace HC.Tests;

/// <summary>
/// What the shop keeps out of a payment: the gateway's own charge for taking it and the GST on that charge,
/// read off the Razorpay payment entity a capture reported (see OrderPaymentCharges.ApplyCapture). These are
/// the figures the order screen's margin and the Finance screen are built on, so what gets written - and what
/// may never be overwritten - is pinned here rather than discovered from the books.
/// </summary>
public class OrderPaymentChargesTests
{
    /// <summary>
    /// A card payment of 500.00 rupees, captured, as Razorpay reports it: it kept 11.80 of which 1.80 is GST,
    /// so the shop keeps 486.40.
    /// </summary>
    private const string CapturedCardPayment = """
        {
          "id": "pay_29QQoUBi66xm2f",
          "entity": "payment",
          "amount": 50000,
          "currency": "INR",
          "status": "captured",
          "method": "card",
          "fee": 1180,
          "tax": 180,
          "created_at": 1743465600
        }
        """;

    /// <summary>The day the fixture's <c>created_at</c> stands for - the day the sale belongs to.</summary>
    private static readonly DateTime ChargedOn = new(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>A captured 500.00 payment as the row arrives at ApplyCapture: a capture that was written down
    /// before anything knew what the gateway charged for taking it.</summary>
    private static OrderPayment CapturedPayment() => new()
    {
        PaymentId = 1,
        OrderId = 1001,
        Provider = OrderPayment.RazorpayProvider,
        RazorpayOrderId = "order_9A33XWu170gUtm",
        RazorpayPaymentId = "pay_29QQoUBi66xm2f",
        Status = OrderPaymentStatus.Captured,
        AmountInPaise = 50000,
        Amount = 500m
    };

    /// <summary>
    /// The capture's own answer is where the charges come from: the gateway's fee, the GST on it, what the shop
    /// keeps, the instrument the money came by and the day it was taken - the day the sale belongs to in the
    /// books, not the day the money reached the bank.
    /// </summary>
    [Fact]
    public void ACaptureWritesDownWhatTheGatewayKept()
    {
        var payment = CapturedPayment();

        Assert.True(OrderPaymentCharges.ApplyCapture(payment, TestPayloads.Json(CapturedCardPayment)));

        Assert.Equal(1180, payment.FeeAmountInPaise);
        Assert.Equal(11.80m, payment.FeeAmount);
        Assert.Equal(180, payment.TaxAmountInPaise);
        Assert.Equal(1.80m, payment.TaxAmount);
        Assert.Equal(486.40m, payment.NetAmount);
        Assert.Equal("card", payment.PaymentMethod);
        Assert.Equal(ChargedOn, payment.GatewayChargedOn);
        Assert.Equal(OrderPaymentCharges.FromPayment, payment.ChargesSource);
    }

    /// <summary>
    /// The arithmetic the whole feature rests on is one method, so a capture and a recon row can never round
    /// the same payment differently: what is taken, minus the gateway's charge, minus the GST on it.
    /// </summary>
    [Fact]
    public void NetIsWhatIsTakenMinusTheChargeMinusTheGstOnIt()
    {
        Assert.Equal(486.40m, OrderPaymentCharges.Net(500m, 11.80m, 1.80m));
    }

    /// <summary>
    /// A webhook replayed after the row was written (Razorpay retries, and the same capture is reported more
    /// than once) changes nothing - otherwise every replay would look like news.
    /// </summary>
    [Fact]
    public void ACaptureThatSaysWhatIsAlreadyWrittenChangesNothing()
    {
        var payment = CapturedPayment();

        Assert.True(OrderPaymentCharges.ApplyCapture(payment, TestPayloads.Json(CapturedCardPayment)));
        Assert.False(OrderPaymentCharges.ApplyCapture(payment, TestPayloads.Json(CapturedCardPayment)));
    }

    /// <summary>
    /// The settlement recon pull is the figure the bank was actually settled on, so a capture heard about
    /// afterwards - a replayed webhook, a payment status check - must not put its earlier estimate back. The
    /// row keeps the recon's fee, its net and its source.
    /// </summary>
    [Fact]
    public void AReconFigureIsNeverPutBackByACaptureHeardAboutLater()
    {
        var payment = CapturedPayment();
        payment.ChargesSource = OrderPaymentCharges.FromRecon;
        payment.FeeAmountInPaise = 1000;
        payment.FeeAmount = 10m;
        payment.TaxAmountInPaise = 180;
        payment.TaxAmount = 1.80m;
        payment.NetAmount = 488.20m;

        Assert.False(OrderPaymentCharges.ApplyCapture(payment, TestPayloads.Json(CapturedCardPayment)));

        Assert.Equal(1000, payment.FeeAmountInPaise);
        Assert.Equal(10m, payment.FeeAmount);
        Assert.Equal(1.80m, payment.TaxAmount);
        Assert.Equal(488.20m, payment.NetAmount);
        Assert.Equal(OrderPaymentCharges.FromRecon, payment.ChargesSource);
    }

    /// <summary>
    /// A payload that says nothing about charges - an authorised attempt, or a payment the gateway has not
    /// worked the fee out for yet (it reports <c>null</c>) - writes nothing at all. An unknown charge must stay
    /// unknown rather than be recorded as a free payment.
    /// </summary>
    [Fact]
    public void APayloadWithoutChargesWritesNothing()
    {
        var payment = CapturedPayment();

        Assert.False(OrderPaymentCharges.ApplyCapture(payment, TestPayloads.Json(
            """{ "id": "pay_29QQoUBi66xm2f", "status": "captured", "amount": 50000 }""")));

        Assert.Null(payment.FeeAmountInPaise);
        Assert.Null(payment.TaxAmountInPaise);
        Assert.Null(payment.NetAmount);
        Assert.Null(payment.GatewayChargedOn);
        Assert.Null(payment.ChargesSource);
    }

    /// <summary>
    /// A zero fee is the gateway saying it charged nothing, and is written as such - the recon pull corrects it
    /// later if that turns out to be wrong. What is never written from is a payload that is not there at all:
    /// the callers that have no payment entity in hand pass nothing.
    /// </summary>
    [Fact]
    public void AZeroFeeIsRecordedAndNoPayloadAtAllIsNot()
    {
        var payment = CapturedPayment();

        Assert.True(OrderPaymentCharges.ApplyCapture(payment, TestPayloads.Json(
            """{ "fee": 0, "tax": 0, "method": "upi", "created_at": 1743465600 }""")));

        Assert.Equal(0, payment.FeeAmountInPaise);
        Assert.Equal(500m, payment.NetAmount);
        Assert.Equal(OrderPaymentCharges.FromPayment, payment.ChargesSource);

        var untouched = CapturedPayment();

        Assert.False(OrderPaymentCharges.ApplyCapture(untouched, null));
        Assert.False(OrderPaymentCharges.ApplyCapture(untouched, TestPayloads.Json("null")));
        Assert.Null(untouched.FeeAmountInPaise);
        Assert.Null(untouched.ChargesSource);
    }

    /// <summary>
    /// Only a recon row is authoritative - anything else recorded on the row is an estimate the next pull is
    /// allowed to correct.
    /// </summary>
    [Fact]
    public void OnlyTheReconRowIsAuthoritative()
    {
        Assert.True(OrderPaymentCharges.IsAuthoritative(OrderPaymentCharges.FromRecon));
        Assert.True(OrderPaymentCharges.IsAuthoritative("recon"));
        Assert.False(OrderPaymentCharges.IsAuthoritative(OrderPaymentCharges.FromPayment));
        Assert.False(OrderPaymentCharges.IsAuthoritative(null));
    }
}
