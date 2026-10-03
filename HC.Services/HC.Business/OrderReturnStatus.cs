namespace HC.Business;

/// <summary>
/// Where a return has got to - <c>OrderReturns.Status</c>, the single place the admin screen, 'My
/// Orders' and the service that moves it all read from.
///
///   Requested -> Arranged -> Received -> Closed     the return that goes through
///            \-&gt; Rejected                          the shop team said no
///            \-&gt; Withdrawn                         the customer took the ask back before a decision
///
/// <see cref="Requested"/>  the ask is with the shop team (a customer's request, or a refusal/return the
///                          courier reported - the latter only ever asks, it never decides).
/// <see cref="Arranged"/>   the ask was approved and the parcel is on its way back: a pickup the shop
///                          booked for a customer return (the reverse <c>OrderShipments</c> leg), or the
///                          courier's own return-to-origin. What was decided is read off the status, so
///                          it is not stored twice (<c>DecisionOn/By/Comment</c> carry the who/when/why).
/// <see cref="Received"/>   the parcel is physically back with the shop: the units leave the delivery pools
///                          for the shop's own 'Returned' one (off sale), the return may be looked over
///                          (<c>MarkUnitsDamagedAsync</c> writes off whatever came back broken) and the money
///                          is owed back.
/// <see cref="Closed"/>     done - the order is 'Returned', the refund is the payment's business and the
///                          units are back in the stock pools. Terminal.
/// <see cref="Rejected"/>   the shop team refused the ask; the order stays exactly as it was. Terminal.
/// <see cref="Withdrawn"/>  the customer changed their mind before anybody decided. Terminal.
///
/// The first three are the OPEN ones: a database index (CreateOrderReturnsTable.sql) allows an order at
/// most one of them, which is what makes 'a delivered order is returned once, refunded once' a fact
/// rather than a hope - <see cref="Open"/> must therefore stay in step with that index.
/// </summary>
public static class OrderReturnStatus
{
    public const string Requested = "Requested";
    public const string Arranged = "Arranged";
    public const string Received = "Received";
    public const string Closed = "Closed";
    public const string Rejected = "Rejected";
    public const string Withdrawn = "Withdrawn";

    /// <summary>
    /// The statuses that count as a live return - must match the filter of
    /// <c>IX_OrderReturns_OrderID_Open</c> (see CreateOrderReturnsTable.sql).
    /// </summary>
    public static readonly string[] Open = { Requested, Arranged, Received };

    /// <summary>True while this return is live (waiting for a decision, or on its way back).</summary>
    public static bool IsOpen(string statusId) => Open.Contains(statusId);

    /// <summary>True once the shop team has said yes: the parcel is coming back (or already is).</summary>
    public static bool WasApproved(string statusId) => statusId is Arranged or Received or Closed;

    /// <summary>True while the ask is still waiting for the shop team's answer.</summary>
    public static bool CanDecide(string statusId) => statusId == Requested;

    /// <summary>True while the customer may still take their ask back (nothing has been decided).</summary>
    public static bool CanWithdraw(string statusId) => statusId == Requested;

    /// <summary>True once the parcel may be booked back in by hand (the shop team has it).</summary>
    public static bool CanMarkReceived(string statusId) => statusId == Arranged;

    /// <summary>
    /// True while the returned parcel may be looked over: it is back with the shop and the return has not been
    /// closed, so a unit found broken can still be written off before its units go back on sale. The inspection
    /// is per unit and is what stops a damaged unit from being sold again
    /// (see <see cref="SkuAvailability.MarkUnitsDamagedAsync"/>); a return with nothing wrong with it is closed
    /// without one.
    /// </summary>
    public static bool CanInspect(string statusId) => statusId == Received;

    /// <summary>True once the return may be closed: the units go back on the shelf and the order is 'Returned'.</summary>
    public static bool CanClose(string statusId) => statusId == Received;
}
