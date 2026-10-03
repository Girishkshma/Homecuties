namespace HC.Business;

/// <summary>
/// The shop's money, read one way: what the gateway took for an order, what was given back, what the gateway
/// kept for itself, and what the shop's own margin on the goods was - plus the period rule the money screens
/// report over.
///
/// It exists so the dashboard's revenue tiles, the Finance screen and the order screen's margin card cannot
/// describe the same payment differently: all of them read the payment rows (OrderPayments) through the members
/// here, exactly as the charges themselves are read through <see cref="OrderPaymentCharges"/>.
///
/// Money is counted on the day the gateway TOOK it (Razorpay's own <c>created_at</c> where the capture reported
/// one, else the day the row was written) and not on the day the money reached the bank: a settlement comes a
/// day or two later and covers however many payments were in it, so the sale and the bank credit are never the
/// same day. A refund is counted on the day it was sent back (Razorpay's own <c>created_at</c> on the refund,
/// which is when the money left the shop's account - a refund still travelling is already gone).
///
/// The order's own status is deliberately not asked about here: whether the shop still owes money back is a
/// different question (<see cref="OrderPaymentStatus.IsRefundOwed"/>, which the refund cards and the 'Refund
/// due' tile ask), while what the gateway took is a fact of the payment row.
/// </summary>
public static class OrderMoney
{
    /// <summary>
    /// The states of a payment row in which the gateway holds the shop's money. A capture is the money taken; a
    /// refund that has been asked for, one Razorpay refused to send and one that has gone back all sit on top
    /// of a capture, so the money was taken in every one of them. What was given back is read BESIDE that (see
    /// <see cref="WasGivenBack"/>) and never instead of it, so a refunded payment is not counted twice.
    ///
    /// A failed or abandoned attempt is not here, and neither is an authorised one: an authorisation is not a
    /// capture - the shop is not paid until the money is taken.
    /// </summary>
    public static readonly string[] TakenStatuses =
    {
        OrderPaymentStatus.Captured,
        OrderPaymentStatus.RefundRequested,
        OrderPaymentStatus.RefundFailed,
        OrderPaymentStatus.Refunded
    };

    /// <summary>How long a period the money screens will report on at once.</summary>
    /// <remarks>
    /// The reads behind them are per-payment rows, which grow with the shop, so a period asked for too wide is
    /// trimmed rather than turned into a screen that takes a minute to open. A longer stretch is read a year at
    /// a time - and the settlement pull, which is the only thing needed to explain an older day, is bounded on
    /// its own (<see cref="RazorpaySettlements.MaxPullDays"/>).
    /// </remarks>
    public const int MaxPeriodDays = 366;

    /// <summary>True when this payment row holds money the gateway took (see <see cref="TakenStatuses"/>).</summary>
    public static bool TookMoney(string? paymentStatus) =>
        paymentStatus != null && TakenStatuses.Contains(paymentStatus);

    /// <summary>
    /// The day the sale belongs to: the day the gateway took the money. Razorpay's own <c>created_at</c> where
    /// the capture reported it, else the day the row was written here - the same day for every payment this
    /// checkout takes, and a better answer than nothing for one written before that field was kept.
    /// </summary>
    public static DateTime TakenOn(DateTime? gatewayChargedOn, DateTime createdOn) =>
        gatewayChargedOn ?? createdOn;

    /// <summary>
    /// True when this money has been given back: a refund was sent and Razorpay created it (<c>RefundedOn</c>),
    /// for a figure (<c>RefundAmount</c>). A refund still travelling (Razorpay reports it as pending) counts as
    /// gone - it has already left the shop's account - and so does one the shop team made by hand in the
    /// Razorpay dashboard. A refund that has only been ASKED for (<c>RefundRequested</c>) has not moved yet, and
    /// is what the 'Refund due' tile is for.
    /// </summary>
    public static bool WasGivenBack(DateTime? refundedOn, decimal? refundAmount) =>
        refundedOn.HasValue && refundAmount > 0m;

    /// <summary>What the shop really keeps of the money taken: what came in, less what went back.</summary>
    public static decimal Net(decimal taken, decimal givenBack) => taken - givenBack;

    /// <summary>
    /// The shop's own margin on one line: 'Profit margin %' is the figure the shop typed on the product, read
    /// here as the profit's share of what the customer paid - a 25% line of 200.00 has made 50.00 and cost
    /// 150.00 before anything else is taken off. It is the shop's DECLARED figure, not a purchase price:
    /// nothing in this system records what the goods cost to buy.
    /// </summary>
    public static decimal DeclaredProfit(decimal unitPrice, decimal profitMarginPercent) =>
        unitPrice * profitMarginPercent / 100m;

    /// <summary>What the shop declared the line cost: what was paid for it, less the margin above.</summary>
    public static decimal DeclaredCost(decimal unitPrice, decimal profitMarginPercent) =>
        unitPrice - DeclaredProfit(unitPrice, profitMarginPercent);

    /// <summary>
    /// The period a money report covers, from what the caller asked for - the same shape the settlement pull's
    /// window resolves to (<see cref="RazorpaySettlements.ResolvePullDays"/>). Nothing asked for means the month
    /// the far end is in, up to that day (the shop's own month to date, which is what the dashboard's monthly
    /// tile reads); the far end is never later than today; a period wider than <see cref="MaxPeriodDays"/> is
    /// trimmed at its far end, so a screen never waits on years of rows; and a period that runs backwards is
    /// read as the single day it names. Pure, so the rule can be pinned without a database.
    /// </summary>
    public static (DateOnly From, DateOnly To) ResolvePeriod(DateOnly? from, DateOnly? to, DateOnly today)
    {
        var last = to ?? today;

        if (last > today)
            last = today;

        // The default period starts on the first of the month the far end is in, so opening the screen on any
        // day of a month reads that month so far - the same stretch the dashboard's monthly tile reports.
        var first = from ?? new DateOnly(last.Year, last.Month, 1);

        var oldest = last.AddDays(-(MaxPeriodDays - 1));
        if (first < oldest)
            first = oldest;

        if (first > last)
            first = last;

        return (first, last);
    }
}
