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

    /// <summary>Why the refund could not be made - the shop team retries from the Razorpay dashboard.</summary>
    public string? RefundFailureReason { get; set; }

    public DateTime? RefundedOn { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public virtual Order Order { get; set; } = null!;
}
