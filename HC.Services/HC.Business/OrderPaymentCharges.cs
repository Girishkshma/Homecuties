using HC.Data.Entities;
using System.Text.Json;

namespace HC.Business;

/// <summary>
/// The money the gateway keeps out of a payment - the one vocabulary the payment row, the settlement
/// ledger and the Finance screen share (see <see cref="RazorpaySettlements"/>).
///
/// Two things are recorded for every captured payment: the gateway's own charge for taking it (the
/// MDR) and the GST on that charge. Razorpay reports both on the payment entity as <c>fee</c> and
/// <c>tax</c> (in paise) and reports them again on the settlement recon row, once the money has been
/// settled to the bank. How much the shop really keeps is the payment minus both:
///
///     NetAmount = Amount - FeeAmount - TaxAmount
///
/// which is exactly the arithmetic the recon row uses for a payment (<c>credit = amount - fee - tax</c>),
/// so the two sources can never describe the same payment differently.
/// </summary>
public static class OrderPaymentCharges
{
    /// <summary>
    /// What a capture reported (Razorpay's payment entity). Early and cheap - the fields are in the
    /// payload Razorpay already sends - but not authoritative: a capture that arrived before the fee
    /// was computed, or a payment made in the Razorpay dashboard, can be worth nothing.
    /// </summary>
    public const string FromPayment = "Payment";

    /// <summary>
    /// What the daily settlement recon pull reported (see <see cref="RazorpaySettlements"/>). This is the
    /// figure the bank was actually settled on, so it overwrites <see cref="FromPayment"/> and is never
    /// overwritten back.
    /// </summary>
    public const string FromRecon = "Recon";

    /// <summary>True when the recorded charges came from the recon row - the figure that may not be corrected.</summary>
    public static bool IsAuthoritative(string? chargesSource) =>
        string.Equals(chargesSource, FromRecon, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// What the shop keeps out of a payment: the money taken, minus the gateway's charge, minus the GST
    /// on it. Kept as one method because a capture and a recon row must not round this differently.
    /// </summary>
    public static decimal Net(decimal amount, decimal fee, decimal tax) => amount - fee - tax;

    /// <summary>
    /// Writes down what the gateway kept out of a captured payment, straight off the payment entity
    /// Razorpay reported the capture with: the <c>fee</c> it charged for taking the money, the <c>tax</c>
    /// (GST) on that fee, the instrument it was taken with and when. Called for every capture the system
    /// hears about (see <c>OrderService.SavePaymentOutcomeAsync</c>), so a paid order's own screen can say
    /// what it made instead of only what the customer paid.
    ///
    /// A row whose charges already came from the settlement recon is left alone: the recon figure is the
    /// one the bank was settled on, and a webhook replayed afterwards must not put the capture's earlier
    /// estimate back (see <see cref="IsAuthoritative"/>). Returns true when anything changed.
    /// </summary>
    public static bool ApplyCapture(OrderPayment payment, JsonElement? gatewayEntity)
    {
        if (!gatewayEntity.HasValue || gatewayEntity.Value.ValueKind != JsonValueKind.Object)
            return false;

        if (IsAuthoritative(payment.ChargesSource))
            return false;

        var entity = gatewayEntity.Value;
        var charged = false;

        // Both are reported in paise. A capture that has only just happened may report 0 for either (the
        // charge is worked out a little later), which is exactly why the daily recon pull exists.
        var feeInPaise = ReadInt(entity, "fee");
        if (feeInPaise.HasValue && payment.FeeAmountInPaise != feeInPaise)
        {
            payment.FeeAmountInPaise = feeInPaise;
            payment.FeeAmount = feeInPaise.Value / 100m;
            charged = true;
        }

        var taxInPaise = ReadInt(entity, "tax");
        if (taxInPaise.HasValue && payment.TaxAmountInPaise != taxInPaise)
        {
            payment.TaxAmountInPaise = taxInPaise;
            payment.TaxAmount = taxInPaise.Value / 100m;
            charged = true;
        }

        // What the shop keeps, worked out the one way this class offers: Amount - Fee - GST.
        if (charged && payment.Amount > 0)
            payment.NetAmount = Net(payment.Amount, payment.FeeAmount ?? 0m, payment.TaxAmount ?? 0m);

        var method = ReadString(entity, "method");
        if (!string.IsNullOrEmpty(method) && !string.Equals(payment.PaymentMethod, method, StringComparison.Ordinal))
        {
            payment.PaymentMethod = method.Length <= 30 ? method : method[..30];
            charged = true;
        }

        // Razorpay's own created_at: the day the money was taken, which is the day the sale belongs to in
        // the books (the settlement to the bank comes a day or two later).
        var chargedOn = ReadTimestamp(entity, "created_at");
        if (chargedOn.HasValue && payment.GatewayChargedOn == null)
        {
            payment.GatewayChargedOn = chargedOn.Value;
            charged = true;
        }

        if (charged)
            payment.ChargesSource = FromPayment;

        return charged;
    }

    /// <summary>A string property of a Razorpay payload, null when it is missing or is not a string.</summary>
    private static string? ReadString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// A whole number property of a Razorpay payload. Null distinguishes "the gateway did not say" from a
    /// real 0, so a fee it has not worked out yet does not look like a free payment.
    /// </summary>
    private static int? ReadInt(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) &&
           value.ValueKind == JsonValueKind.Number &&
           value.TryGetInt32(out var number)
            ? number
            : null;

    /// <summary>Razorpay's Unix-second timestamp as a UTC DateTime (the convention the payment columns use).</summary>
    private static DateTime? ReadTimestamp(JsonElement element, string property)
    {
        var seconds = ReadInt(element, property);

        return seconds.HasValue && seconds.Value > 0
            ? DateTimeOffset.FromUnixTimeSeconds(seconds.Value).UtcDateTime
            : null;
    }
}
