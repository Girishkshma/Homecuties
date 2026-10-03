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
    /// The value the GST is charged on for one unit: what the customer pays for it before tax - the price less both
    /// discounts, which is the sub-total the checkout itself arrives at for the cart (see CartService.Calculation).
    ///
    /// It is worked out here as well as there on purpose: by the time a report is run the cart is long gone, so the
    /// sale is read back from the order line's own snapshot of the price and the two discounts.
    /// </summary>
    public static decimal TaxableValue(decimal unitPrice, decimal discountPercent, decimal additionalDiscountPercent) =>
        unitPrice - (unitPrice * discountPercent / 100m) - (unitPrice * additionalDiscountPercent / 100m);

    /// <summary>The tax on a value at a rate - the one multiplication every GST figure the screens show is built from.</summary>
    public static decimal GstOn(decimal taxableValue, decimal gstPercent) => taxableValue * gstPercent / 100m;

    /// <summary>
    /// The rate the shop's checkout actually charged on a line, which is what the customer really paid and therefore
    /// what is held for the government: the CGST rate alone.
    ///
    /// SGST and IGST are recorded on the product and snapshotted onto the order line, but the cart's own calculation
    /// charges the CGST rate and ignores both - so counting them here would make the tax a report shows LARGER than
    /// the tax the customer paid, and the screen would stop tying to the money the gateway took. They are taken as
    /// parameters rather than left out so that a reader sees them being passed and passed over, and so that the day
    /// the checkout charges the other two as well, this is the one place that has to change.
    /// </summary>
    public static decimal ChargedGstRate(decimal cgstPercent, decimal sgstPercent, decimal igstPercent) =>
        cgstPercent;

    /// <summary>
    /// What one line was worth to the customer: its taxable value plus the tax the checkout charged on it - the
    /// money the customer really paid for that line, which is what the order's own totals are the sum of.
    ///
    /// It is the weight a partner's share of an order is taken by (<see cref="Share"/>): their lines' worth against
    /// the whole order's. It is worked out from the two rules above rather than restated, so it can never drift from
    /// the tax the other screens show - and it counts the line BEFORE any declared margin, which is the shop's own
    /// later reading of the goods rather than anything the customer paid.
    /// </summary>
    public static decimal LineValue(
        decimal unitPrice,
        decimal discountPercent,
        decimal additionalDiscountPercent,
        decimal cgstPercent,
        decimal sgstPercent,
        decimal igstPercent)
    {
        var taxableValue = TaxableValue(unitPrice, discountPercent, additionalDiscountPercent);
        return taxableValue + GstOn(taxableValue, ChargedGstRate(cgstPercent, sgstPercent, igstPercent));
    }

    /// <summary>
    /// The part of an amount that belongs to a slice of a whole - the one way this file divides money, so every
    /// share a screen shows is worked out the same way.
    ///
    /// It exists because one sale is not always one partner's: a partner's goods are named by the order's LINES (a
    /// line names a SKU, and the SKU sits in one partner's inventory - <c>Sku.Inventory.PartnerId</c>), while the
    /// order's money - what the gateway took, what it kept for taking it, what the courier billed - is recorded
    /// against the ORDER. Giving a partner their own lines' part of it, by what those lines were worth against the
    /// whole order, is what keeps one partner's screen free of another partner's money while their shares still
    /// add back up to the order they came from.
    ///
    /// A whole of nothing is zero rather than a division by zero, and a slice of nothing is nothing. A negative
    /// amount (a settlement line that took money out) gives a negative share, which is what the books need.
    /// </summary>
    public static decimal Share(decimal amount, decimal part, decimal whole) =>
        whole == 0m ? 0m : amount * part / whole;

    /// <summary>
    /// One order-level figure split across the order's own units - what the gateway kept for taking the money,
    /// the GST on that, the courier's bill for a parcel - so each line carries its own part of it
    /// (<c>OrderItemMoney</c>) and the shop can read what one SKU really made.
    ///
    /// The split is by weight, and the weight is what each line was worth to the customer
    /// (<see cref="LineValue"/>) - deliberately the same rule <see cref="Share"/> applies to one slice of an
    /// order, so a partner's per-line figures still add up to the share their screen has always shown.
    ///
    /// It is done here, rather than left to each caller, because of the last paisa: a plain division leaves a
    /// fraction of a paisa on every line and the parts stop adding up to the whole they came from. This works in
    /// whole paise and gives the odd ones to the largest remainders, so the parts always SUM EXACTLY to the
    /// amount - which is what makes a per-line report tie back to the order it was cut from.
    ///
    /// Every weight being zero (lines that were given away, say) is not a reason to lose the amount: the units
    /// then share it EQUALLY. There is no line worth more than another to charge it to, and dropping it would
    /// quietly shrink the shop's own costs.
    ///
    /// Returns one figure per weight, in the order the weights came in. No weights at all gives nothing back,
    /// and an amount of nothing gives a zero for each weight.
    /// </summary>
    public static decimal[] Apportion(decimal amount, IReadOnlyList<decimal> weights)
    {
        if (weights == null || weights.Count == 0)
            return Array.Empty<decimal>();

        // A negative weight is meaningless as a share - it would silently invert the split - so the weights
        // are floored at nothing. What the customer paid for a line is never negative in the first place; the
        // floor is here so a corrupted row costs the order a sane split rather than a wrong one.
        var usable = weights.Select(weight => weight > 0m ? weight : 0m).ToArray();
        var total = usable.Sum();

        if (total == 0m)
            usable = Enumerable.Repeat(1m, weights.Count).ToArray();

        var usableTotal = usable.Sum();
        var rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        var exact = new decimal[weights.Count];
        var parts = new decimal[weights.Count];

        for (var i = 0; i < weights.Count; i++)
        {
            exact[i] = rounded * usable[i] / usableTotal;

            // Truncated TOWARDS ZERO, so a part can only ever be short of its exact share and never over it -
            // which means the parts can only add up to less than the amount, never more.
            parts[i] = Math.Round(exact[i], 2, MidpointRounding.ToZero);
        }

        var toGive = rounded - parts.Sum();

        // At most one paisa per line, and always the same sign as the amount: a refund gives back in the same
        // coins it was taken in.
        var paise = (int)Math.Round(Math.Abs(toGive) * 100m, MidpointRounding.AwayFromZero);

        if (paise == 0)
            return parts;

        // The odd paise go to the lines that lost the most to truncation (the largest remainders). When two
        // remainders tie - or a line has to be given two paise, which only happens when every weight but one is
        // nothing - the line's own position breaks it, so the same order always splits the same way.
        var byRemainder = Enumerable.Range(0, weights.Count)
            .OrderByDescending(i => Math.Abs(exact[i] - parts[i]))
            .ThenBy(i => i)
            .ToArray();

        var step = toGive < 0m ? -0.01m : 0.01m;

        for (var given = 0; given < paise; given++)
            parts[byRemainder[given % byRemainder.Length]] += step;

        return parts;
    }

    /// <summary>
    /// The tax a refund gives back: the same share of what its order held as the refund is of what that order was
    /// charged - so a refund of half an order gives back half its tax and a full refund gives back all of it.
    ///
    /// An order that charged no tax has none to give back, and neither has a refund of nothing or an order whose
    /// charge is unknown: those are zero rather than a division by zero.
    /// </summary>
    public static decimal GstGivenBack(decimal refundAmount, decimal orderGst, decimal orderCharged) =>
        refundAmount <= 0m || orderGst <= 0m || orderCharged <= 0m
            ? 0m
            : Share(refundAmount, orderGst, orderCharged);

    /// <summary>What is still held for the government once the tax its refunds gave back is taken off.</summary>
    public static decimal GstHeld(decimal gstOnSales, decimal gstGivenBack) => gstOnSales - gstGivenBack;

    /// <summary>
    /// What a period owes the government: the tax it holds, less the tax paid on its own bills, which it can set
    /// against it (its input credit).
    ///
    /// A negative answer is not an error: a period carrying more credit than tax offsets the difference against a
    /// later one, which is a thing a screen shows rather than floors at zero.
    /// </summary>
    public static decimal NetGstPayable(decimal gstHeld, decimal creditableGst) => gstHeld - creditableGst;

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
