namespace HC.Business;

/// <summary>
/// The life of a single payment attempt, written to <c>OrderPayments.Status</c> by
/// <see cref="OrderService"/> (and read back by the admin order screen). The names are the single
/// definition used everywhere, so a status filter cannot drift apart from what is stored.
/// </summary>
public static class OrderPaymentStatus
{
    /// <summary>A payment was started (the Razorpay order exists) but no result has come back yet.</summary>
    public const string Created = "Created";

    /// <summary>The bank authorised the money - it still has to be captured.</summary>
    public const string Authorized = "Authorized";

    /// <summary>The money is ours: the order may be confirmed.</summary>
    public const string Captured = "Captured";

    /// <summary>The attempt was refused or abandoned; the customer can start a new one.</summary>
    public const string Failed = "Failed";

    /// <summary>
    /// The money was taken and is owed back: the customer cancelled a paid order and the shop team
    /// still has to approve the refund (<c>RefundRequestedOn</c> says when it was asked for). The
    /// payment itself is still <see cref="Captured"/> - this status only means "a refund is due", so
    /// nothing is given back until the shop approves it.
    /// </summary>
    public const string RefundRequested = "RefundRequested";

    /// <summary>The money was given back - <c>RefundID</c> holds the Razorpay refund.</summary>
    public const string Refunded = "Refunded";

    /// <summary>The refund was refused - a human has to retry it in the Razorpay dashboard.</summary>
    public const string RefundFailed = "RefundFailed";

    /// <summary>Razorpay's own payment status (created/authorized/captured/failed/refunded) as one of the values above.</summary>
    public static string FromRazorpay(string? razorpayStatus) => razorpayStatus switch
    {
        "authorized" => Authorized,
        "captured" => Captured,
        "refunded" => Refunded,
        "failed" => Failed,
        _ => Created
    };

    /// <summary>
    /// True when this payment's money is owed back to the customer: a refund that has been asked for
    /// (<see cref="RefundRequested"/>, waiting for the shop team's approval), one Razorpay refused
    /// (<see cref="RefundFailed"/>, retried from the admin order screen), or a capture on an order that gave its
    /// units back - cancelled, or returned once its parcel came back - whose refund was never asked for.
    ///
    /// The order's status is passed in as a plain value, because the order list and the dashboard read this rule
    /// inside a database query (EF translates members, not method calls - see the note in
    /// AdminDashboardService.GetOrdersAsync, which writes the same rule as SQL). It is one rule with two
    /// spellings, and they are checked against each other by the tests.
    /// </summary>
    public static bool IsRefundOwed(short orderStatusId, string? paymentStatus) =>
        paymentStatus == RefundRequested ||
        paymentStatus == RefundFailed ||
        (SkuAvailability.ReleasesUnits(orderStatusId) && paymentStatus == Captured);
}
