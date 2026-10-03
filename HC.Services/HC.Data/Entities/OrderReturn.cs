using System;

namespace HC.Data.Entities;

/// <summary>
/// A return of an order: the customer asking to send something back, or the courier reporting a parcel
/// that was refused / came back on its own - the ask, the shop team's answer, the parcel coming back and
/// what the inspection found on it.
///
/// An order used to end at Delivered: a customer wanting a return had to e-mail the shop, so the return
/// lived in somebody's inbox and nothing on the order said a return was pending - the admin screen could
/// not tell a delivered order from one being sent back, and the money owed for it had to be remembered.
///
/// What this row deliberately does NOT hold:
///   * the money  - the refund belongs to the payment (<c>OrderPayments</c> status/refund columns, see
///                  <c>RazorpayRefunds</c>), so a refund cannot be described in two places and this row
///                  carries no amount of its own;
///   * the parcel - the reverse leg is a second <see cref="OrderShipment"/> row with
///                  <c>Direction = OrderShipment.DirectionReverse</c>, tracked by the same code as the
///                  one that went out;
///   * the story  - every step is written to <see cref="OrderHistory"/> as it happens, which both
///                  'My Orders' and the admin order screen already show.
///
/// At most one OPEN return per order (a filtered unique index in CreateOrderReturnsTable.sql enforces
/// it, not only this service): the rule 'a delivered order is returned once, refunded once' rests on it.
/// Closed/rejected/withdrawn rows stay for the record.
///
/// The vocabulary - <see cref="Status"/>, <see cref="ReasonCode"/>, <see cref="Origin"/> - lives in
/// HC.Business (OrderReturnStatus/OrderReturnReason/OrderReturnOrigin), the same split as
/// OrderPayments.Status and OrderPaymentStatus.
/// </summary>
public partial class OrderReturn
{
    public long ReturnId { get; set; }

    public long OrderId { get; set; }

    /// <summary>
    /// Where the ask came from: OrderReturnOrigin.Customer (asked from 'My Orders' on a delivered order)
    /// or OrderReturnOrigin.Courier (a refusal / return-to-origin the tracking pull reported - the
    /// courier raises the ask, it never decides it).
    /// </summary>
    public string Origin { get; set; } = null!;

    /// <summary>
    /// The reason in the customer's own words when they gave one, or the courier's wording when the
    /// parcel came back by itself. Always filled: a return is never recorded without a reason.
    /// </summary>
    public string Reason { get; set; } = null!;

    /// <summary>
    /// Which reason <see cref="Reason"/> is (OrderReturnReason.*), so returns can be counted by reason
    /// without reading the free text.
    /// </summary>
    public string ReasonCode { get; set; } = null!;

    /// <summary>
    /// Where the return has got to (OrderReturnStatus.*): Requested -> Arranged -> Received -> Closed,
    /// or Rejected/Withdrawn on the way out.
    /// </summary>
    public string Status { get; set; } = null!;

    public DateTime RequestedOn { get; set; }

    /// <summary>Who asked: the customer's login id, or null when the courier raised it.</summary>
    public string? RequestedBy { get; set; }

    /// <summary>When the shop team answered the ask. Null while it is still waiting for one.</summary>
    public DateTime? DecisionOn { get; set; }

    /// <summary>The admin login id that approved or rejected the ask.</summary>
    public string? DecisionBy { get; set; }

    /// <summary>The note the shop team had to give with their answer (why it was approved or refused).</summary>
    public string? DecisionComment { get; set; }

    /// <summary>
    /// When the parcel was physically back with the shop (the reverse leg's own DeliveredOn is when the
    /// courier says it handed it over; this is the shop confirming it).
    /// </summary>
    public DateTime? ReceivedOn { get; set; }

    /// <summary>
    /// When the return was closed: its units were put back on the shelf (or written off) and the order
    /// became 'Returned'.
    /// </summary>
    public DateTime? ClosedOn { get; set; }

    /// <summary>
    /// When the shop looked over the returned parcel. Null while nobody has - returned units go back on
    /// sale without waiting for it (see <c>SkuAvailability.ReleaseReturnedUnitsAsync</c>).
    /// </summary>
    public DateTime? InspectionOn { get; set; }

    /// <summary>The admin login id that inspected the parcel.</summary>
    public string? InspectionBy { get; set; }

    /// <summary>
    /// What the inspection found and why. Which units came back broken is the stock trail's business
    /// (SKUHistory rows moving those SKUs to the 'Damage' status, see
    /// <c>SkuAvailability.MarkUnitsDamagedAsync</c>); this is the sentence a human wrote with it.
    /// </summary>
    public string? InspectionComment { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public virtual Order Order { get; set; } = null!;
}
