using System;

namespace HC.Data.Entities;

/// <summary>
/// One LINE of a settlement: the thing the money behind it was - a payment that was settled, a refund that
/// was taken out of the settlement, an adjustment or a transfer. This is Razorpay's reconciliation report
/// for a day (<c>GET /v1/settlements/recon/combined</c>, see <c>HC.Business.RazorpaySettlements</c>).
///
/// It is the authoritative word on what the gateway kept out of a payment: the payment entity's own
/// <c>fee</c>/<c>tax</c> (written by a capture as <c>ChargesSource = Payment</c>, see
/// <c>HC.Business.OrderPaymentCharges</c>) is an estimate read off the wrong payload, while a recon line is
/// the figure the bank was actually settled on - which is why the pull corrects the payment row with it and
/// nothing may correct it back.
///
/// The line is written whole, exactly as Razorpay spoke it, and <see cref="NetAmount"/> is worked out
/// beside it (<c>OrderPaymentCharges.Net</c>: amount - fee - GST) instead of overwriting the gateway's own
/// <see cref="Credit"/>. When the two disagree the gateway's <c>fee</c> already includes the GST on it -
/// the one thing about these numbers that cannot be reasoned about, and the reason both spellings are
/// stored (see <c>RazorpaySettlements.CreditAgreesWithFee</c>).
///
/// Identity: <see cref="RazorpayEntityId"/> + <see cref="ItemType"/> (a unique index in
/// CreateSettlementTables.sql), so a day pulled twice updates its lines and adds none.
/// </summary>
public partial class SettlementItem
{
    public long SettlementItemId { get; set; }

    /// <summary>The settlement this line was part of, when the pull has already fetched it. Null is allowed.</summary>
    public long? SettlementId { get; set; }

    /// <summary>
    /// Razorpay's id of the thing that was settled: <c>pay_xxx</c> on a payment line, <c>rfnd_xxx</c> on a
    /// refund, and its own id of an adjustment or a transfer.
    /// </summary>
    public string RazorpayEntityId { get; set; } = null!;

    /// <summary>
    /// Razorpay's own word for the kind of line - see <c>HC.Business.RazorpaySettlements</c>
    /// (payment / refund / adjustment / transfer). Stored as the gateway spells it, so a payload field that
    /// changes shape is visible in the data rather than silently unmapped.
    /// </summary>
    public string ItemType { get; set; } = null!;

    /// <summary>The payment behind the line (Razorpay's <c>payment_id</c>), which on a payment line is the entity id itself.</summary>
    public string? RazorpayPaymentId { get; set; }

    /// <summary>
    /// The order the payment was taken against (Razorpay's <c>order_id</c>) - what the line is matched to
    /// <c>OrderPayments.RazorpayOrderId</c> with.
    /// </summary>
    public string? RazorpayOrderId { get; set; }

    /// <summary>The refund this line is, on a refund line (the entity id, restated so it can be looked up by name).</summary>
    public string? RazorpayRefundId { get; set; }

    /// <summary>The settlement the line belongs to, as the report names it (the same id as <see cref="Settlement.RazorpaySettlementId"/>).</summary>
    public string? RazorpaySettlementId { get; set; }

    /// <summary>The bank reference of the settlement this line was part of (Razorpay's <c>settlement_utr</c>).</summary>
    public string? SettlementUtr { get; set; }

    /// <summary>The size of the transaction behind the line, as Razorpay speaks it (paise).</summary>
    public int? AmountInPaise { get; set; }

    /// <summary>The size of the transaction behind the line, in rupees.</summary>
    public decimal? Amount { get; set; }

    /// <summary>What the settlement took out of the shop's account for this line (a refund), in paise.</summary>
    public int? DebitInPaise { get; set; }

    /// <summary>What the settlement took out of the shop's account for this line, in rupees.</summary>
    public decimal? Debit { get; set; }

    /// <summary>What the settlement paid in for this line (a payment, net of the fee), in paise.</summary>
    public int? CreditInPaise { get; set; }

    /// <summary>What the settlement paid in for this line, in rupees - the figure a bank statement shows.</summary>
    public decimal? Credit { get; set; }

    /// <summary>The gateway's charge for taking the money (the MDR), in paise - the authoritative figure.</summary>
    public int? FeeAmountInPaise { get; set; }

    /// <summary>The gateway's charge for taking the money, in rupees.</summary>
    public decimal? FeeAmount { get; set; }

    /// <summary>GST charged on the gateway's fee, in paise.</summary>
    public int? TaxAmountInPaise { get; set; }

    /// <summary>GST charged on the gateway's fee, in rupees.</summary>
    public decimal? TaxAmount { get; set; }

    /// <summary>
    /// Amount - Fee - GST, worked out the one way this database offers
    /// (<see cref="HC.Business.OrderPaymentCharges.Net"/>). Stored next to Razorpay's own
    /// <see cref="Credit"/> on purpose: the two disagreeing is how the GST question is answered from live
    /// data instead of from an assumption.
    /// </summary>
    public decimal? NetAmount { get; set; }

    /// <summary>The instrument the customer paid with (card / upi / netbanking / wallet ...).</summary>
    public string? PaymentMethod { get; set; }

    /// <summary>Whether Razorpay is holding the settlement of this line back.</summary>
    public bool? IsOnHold { get; set; }

    /// <summary>Whether Razorpay has settled this line at all.</summary>
    public bool? IsSettled { get; set; }

    /// <summary>When the transaction behind the line happened (Razorpay's <c>created_at</c>).</summary>
    public DateTime? GatewayCreatedOn { get; set; }

    /// <summary>When the line was settled to the bank (Razorpay's <c>settled_at</c>) - the day the money left.</summary>
    public DateTime? SettledOn { get; set; }

    /// <summary>The dispute this line belongs to, when it is one (a chargeback taken out of the settlement).</summary>
    public string? DisputeId { get; set; }

    /// <summary>Razorpay's own note about the line, when it wrote one (truncated to the column's width).</summary>
    public string? Description { get; set; }

    /// <summary>
    /// The day the recon report this line came from was asked for - which pull first saw the line. Razorpay
    /// settles a transaction on one day, so a re-pull of that day always describes the same lines.
    /// </summary>
    public DateTime? ReconDay { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public virtual Settlement? Settlement { get; set; }
}
