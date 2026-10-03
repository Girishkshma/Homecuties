using HC.Business;
using HC.Data.Entities;
using Xunit;

namespace HC.Tests;

/// <summary>
/// What a refund is written down as while it travels and once it lands - the reader the refund call, the refund
/// webhook and the daily gateway sync all share (see RazorpayRefunds.ApplyRefundOutcome), and the rule that
/// decides which refunds are still worth asking the gateway about (see RazorpayRefunds.RefundInFlight). A refund
/// read three ways is exactly how a customer's money gets lost, so all three are pinned on the same fixtures.
/// </summary>
public class RazorpayRefundsTests
{
    /// <summary>A refund of 500.00 that the gateway has taken but the bank has not settled yet.</summary>
    private const string PendingRefund = """
        {
          "id": "rfnd_1",
          "entity": "refund",
          "payment_id": "pay_29QQoUBi66xm2f",
          "amount": 50000,
          "status": "pending",
          "created_at": 1743465600
        }
        """;

    /// <summary>The same refund settled: the bank's reference is there and the money has left the shop.</summary>
    private const string ProcessedRefund = """
        {
          "id": "rfnd_1",
          "payment_id": "pay_29QQoUBi66xm2f",
          "amount": 50000,
          "status": "processed",
          "speed_processed": "normal",
          "acquirer_data": { "arn": "1234567890123456789012" },
          "created_at": 1743465600
        }
        """;

    /// <summary>A refund Razorpay refused after it was sent.</summary>
    private const string FailedRefund = """
        {
          "id": "rfnd_1",
          "payment_id": "pay_29QQoUBi66xm2f",
          "amount": 50000,
          "status": "failed",
          "error": { "description": "Insufficient balance in merchant account" },
          "created_at": 1743465600
        }
        """;

    /// <summary>The day the fixtures' <c>created_at</c> stands for.</summary>
    private static readonly DateTime RefundedOn = new(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>The order's payment row when the shop team sends the refund: the money is flagged as owed, and
    /// nothing has been heard back from the gateway yet.</summary>
    private static OrderPayment RefundablePayment() => new()
    {
        PaymentId = 1,
        OrderId = 1001,
        Provider = OrderPayment.RazorpayProvider,
        RazorpayOrderId = "order_9A33XWu170gUtm",
        RazorpayPaymentId = "pay_29QQoUBi66xm2f",
        Status = OrderPaymentStatus.RefundRequested,
        AmountInPaise = 50000,
        Amount = 500m
    };

    /// <summary>
    /// A refund is worth asking the gateway about until it has an answer: it travels for a day or two, so a
    /// 'pending' one is asked again, and so is one reported 'processed' that has no bank reference yet. A
    /// refund Razorpay refused has nothing left to hear, and neither has one the shop team made in the
    /// Razorpay dashboard itself (there is no refund of ours to ask about).
    /// </summary>
    [Theory]
    [InlineData("rfnd_1", RazorpayRefunds.RefundStatusPending, null, 0, true)]
    [InlineData("rfnd_1", RazorpayRefunds.RefundStatusProcessed, null, 0, true)]
    [InlineData("rfnd_1", RazorpayRefunds.RefundStatusProcessed, "1234567890123456789012", 0, false)]
    [InlineData("rfnd_1", RazorpayRefunds.RefundStatusFailed, null, 0, false)]
    [InlineData(null, RazorpayRefunds.ManualRefundStatus, null, 0, false)]
    [InlineData("rfnd_1", null, null, 0, true)]
    [InlineData("rfnd_1", RazorpayRefunds.RefundStatusPending, null, 40, false)]
    public void ARefundIsAskedAboutUntilItHasAnAnswer(
        string? refundId,
        string? refundStatus,
        string? arn,
        int refundedDaysAgo,
        bool expected)
    {
        var payment = RefundablePayment();
        payment.RefundId = refundId;
        payment.RefundStatus = refundStatus;
        payment.RefundArn = arn;
        payment.RefundedOn = DateTime.UtcNow.AddDays(-refundedDaysAgo);

        Assert.Equal(expected, RazorpayRefunds.RefundInFlight(payment));
    }

    /// <summary>
    /// A payment nobody sent a refund for is never asked about - the daily sync must not invent questions
    /// about orders that were never refunded.
    /// </summary>
    [Fact]
    public void APaymentWithNoRefundIsNeverInFlight()
    {
        Assert.False(RazorpayRefunds.RefundInFlight(RefundablePayment()));
    }

    /// <summary>
    /// The refund's own id, how far it has got, what it really gave back and the day it went back are written
    /// onto the payment row - but the row is NOT marked refunded: the money has left us and not yet arrived, so
    /// the order still owes it until the bank settles. Asking again with the same answer changes nothing, which
    /// is what makes webhook replays and the daily sync harmless.
    /// </summary>
    [Fact]
    public void ARefundStillTravellingIsWrittenDownAsPending()
    {
        var payment = RefundablePayment();

        Assert.True(RazorpayRefunds.ApplyRefundOutcome(payment, TestPayloads.Json(PendingRefund)));

        Assert.Equal("rfnd_1", payment.RefundId);
        Assert.Equal(RazorpayRefunds.RefundStatusPending, payment.RefundStatus);
        Assert.Equal(50000, payment.RefundAmountInPaise);
        Assert.Equal(500m, payment.RefundAmount);
        Assert.Equal(RefundedOn, payment.RefundedOn);
        Assert.Equal(OrderPaymentStatus.RefundRequested, payment.Status);

        Assert.False(RazorpayRefunds.ApplyRefundOutcome(payment, TestPayloads.Json(PendingRefund)));
    }

    /// <summary>
    /// The moment the bank settles, the row is closed: marked refunded, the bank's reference kept so a customer
    /// who cannot find the money can be answered, how the money was sent recorded - and the reason a previous
    /// attempt failed cleared, because a row now refused would be shown to the shop team as money still owed
    /// that has in fact gone back.
    /// </summary>
    [Fact]
    public void ARefundTheBankHasSettledClosesTheBooksOnIt()
    {
        var payment = RefundablePayment();
        payment.RefundId = "rfnd_1";
        payment.RefundStatus = RazorpayRefunds.RefundStatusFailed;
        payment.RefundFailureReason = "Razorpay reported the refund as failed.";
        payment.Status = OrderPaymentStatus.RefundFailed;

        Assert.True(RazorpayRefunds.ApplyRefundOutcome(payment, TestPayloads.Json(ProcessedRefund)));

        Assert.Equal(OrderPaymentStatus.Refunded, payment.Status);
        Assert.Equal(RazorpayRefunds.RefundStatusProcessed, payment.RefundStatus);
        Assert.Null(payment.RefundFailureReason);
        Assert.Equal("1234567890123456789012", payment.RefundArn);
        Assert.Equal("normal", payment.RefundSpeedProcessed);
        Assert.False(RazorpayRefunds.RefundInFlight(payment));
    }

    /// <summary>
    /// A refund Razorpay refused leaves the money owed and says why: the row goes to RefundFailed with Razorpay's
    /// own words beside it, so the shop team can retry it - and the order keeps flagging it as owed, because it
    /// is.
    /// </summary>
    [Fact]
    public void ARefundRazorpayRefusedLeavesTheMoneyOwedAndSaysWhy()
    {
        var payment = RefundablePayment();

        Assert.True(RazorpayRefunds.ApplyRefundOutcome(payment, TestPayloads.Json(FailedRefund)));

        Assert.Equal(OrderPaymentStatus.RefundFailed, payment.Status);
        Assert.Equal("Insufficient balance in merchant account", payment.RefundFailureReason);
        Assert.False(RazorpayRefunds.RefundInFlight(payment));
        Assert.True(OrderPaymentStatus.IsRefundOwed(OrderStatusFlow.Cancelled, payment.Status));
    }
}
