using System;

namespace HC.Data.Entities;

/// <summary>
/// One payment ATTEMPT for an order - the money side of the order, written down as it happens.
///
/// Nothing about a payment used to be stored: the Razorpay order id only travelled to the browser,
/// the payment id only ended up in the free-text OrderHistory comment and a failed attempt left no
/// trace at all. An order therefore could not be refunded (the payment id and the amount that was
/// taken were unknown) and a payment that never went through could not be resumed (the Razorpay
/// order it belongs to was unknown - it had to be guessed from the order date).
///
/// One row per attempt, so a retry after a failure simply adds a row and the newest row is the
/// attempt the customer is currently on.
/// </summary>
public partial class OrderPayment
{
    /// <summary>The only provider the storefront uses.</summary>
    public const string RazorpayProvider = "Razorpay";

    public long PaymentId { get; set; }

    public long OrderId { get; set; }

    /// <summary>OrderPayment.RazorpayProvider - kept as a column so a second gateway could be added later.</summary>
    public string Provider { get; set; } = RazorpayProvider;

    /// <summary>The Razorpay order (<c>order_xxx</c>) this attempt was started against.</summary>
    public string? RazorpayOrderId { get; set; }

    /// <summary>The Razorpay payment (<c>pay_xxx</c>) once the customer actually paid.</summary>
    public string? RazorpayPaymentId { get; set; }

    /// <summary>See <see cref="HC.Business.OrderPaymentStatus"/>: Created/Authorized/Captured/Failed/Refunded.</summary>
    public string Status { get; set; } = null!;

    /// <summary>What the customer is charged, in rupees (Razorpay is asked for <see cref="AmountInPaise"/>).</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// The amount exactly as Razorpay speaks it (paise). Money is stored in paise next to the rupee
    /// column because a refund must be sent back in the same unit it was taken in.
    /// </summary>
    public int AmountInPaise { get; set; }

    /// <summary>Razorpay's error code of a failed attempt (e.g. <c>BAD_REQUEST_ERROR</c>).</summary>
    public string? FailureCode { get; set; }

    /// <summary>Razorpay's error description of a failed attempt, shown to support.</summary>
    public string? FailureReason { get; set; }

    public string? RefundId { get; set; }

    /// <summary>
    /// Razorpay's own refund status (processed / pending / failed), or <c>manual</c> when the shop
    /// team refunded it in the Razorpay dashboard (see <see cref="HC.Business.RazorpayRefunds"/>).
    /// </summary>
    public string? RefundStatus { get; set; }

    /// <summary>
    /// When the customer cancelled a paid order, i.e. when the refund was asked for. The money is
    /// still ours at that point - the shop team approves the refund from the admin order screen, and
    /// <see cref="RefundedOn"/> is when it actually went back.
    /// </summary>
    public DateTime? RefundRequestedOn { get; set; }

    /// <summary>Why the refund was asked for (e.g. "Order cancelled by the customer").</summary>
    public string? RefundRequestedComment { get; set; }

    public decimal? RefundAmount { get; set; }

    /// <summary>
    /// What Razorpay really gave back, in paise (the refund entity's own <c>amount</c>). Kept next to
    /// <see cref="RefundAmount"/> like every other money pair here: the refund is a gateway figure and
    /// must be able to be compared with what was asked for.
    /// </summary>
    public int? RefundAmountInPaise { get; set; }

    /// <summary>
    /// The bank's reference for the refund (Razorpay's <c>acquirer_data.arn</c>), so a refund the
    /// customer cannot find can be traced at their bank. Filled in by the refund webhook and by the
    /// daily gateway sync (<see cref="HC.Business.RazorpayRefunds"/>).
    /// </summary>
    public string? RefundArn { get; set; }

    /// <summary>How Razorpay sent the money back: <c>normal</c>, <c>optimum</c> or <c>instant</c>.</summary>
    public string? RefundSpeedProcessed { get; set; }

    /// <summary>Why the refund could not be made - the shop team retries from the Razorpay dashboard.</summary>
    public string? RefundFailureReason { get; set; }

    public DateTime? RefundedOn { get; set; }

    // ---------------------------------------------------------------------------------------------
    // What the gateway charged for taking this payment (see HC.Business.OrderPaymentCharges and
    // RazorpaySettlements).
    //
    // Razorpay reports the charge on the payment entity itself (fee and tax, both in paise) and again,
    // this time authoritatively, on the settlement recon row once the money has been settled to the
    // bank. Both are kept, told apart by ChargesSource: the capture's own figure is written first and
    // the daily recon pull corrects it (OrderPaymentCharges.FromRecon), which a later webhook replay
    // must not undo. Without these columns a paid order's own screen cannot say whether it made money.
    // ---------------------------------------------------------------------------------------------

    /// <summary>The gateway's charge for taking the payment (the MDR), as Razorpay speaks it (paise).</summary>
    public int? FeeAmountInPaise { get; set; }

    /// <summary>The gateway's charge in rupees - the figure deducted from the settlement.</summary>
    public decimal? FeeAmount { get; set; }

    /// <summary>
    /// GST charged on the gateway's fee, in paise. Razorpay reports <c>fee</c> and <c>tax</c> as
    /// separate fields, and both are kept apart here so the Finance screen can show the charge and the
    /// tax on it as the two lines a P&amp;L needs.
    /// </summary>
    public int? TaxAmountInPaise { get; set; }

    /// <summary>GST charged on the gateway's fee, in rupees.</summary>
    public decimal? TaxAmount { get; set; }

    /// <summary>
    /// What the shop keeps from this payment: <see cref="Amount"/> minus <see cref="FeeAmount"/> minus
    /// <see cref="TaxAmount"/> (see <see cref="HC.Business.OrderPaymentCharges.Net"/>).
    /// </summary>
    public decimal? NetAmount { get; set; }

    /// <summary>The instrument the customer paid with (card / upi / netbanking / wallet ...).</summary>
    public string? PaymentMethod { get; set; }

    /// <summary>When the gateway took the money (Razorpay's own <c>created_at</c>).</summary>
    public DateTime? GatewayChargedOn { get; set; }

    /// <summary>Where the charges came from: <see cref="HC.Business.OrderPaymentCharges.FromPayment"/> or .FromRecon.</summary>
    public string? ChargesSource { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public virtual Order Order { get; set; } = null!;
}
