using System;

namespace HC.Data.Entities;

/// <summary>
/// What one order line really carried, money-wise: its own part of what the gateway kept for taking the order's
/// money and the GST on that, its own part of what the couriers billed for the parcels that carried it, and the
/// output GST inside what the customer paid for it.
///
/// The order-level figures were never enough to answer a shop keeper's question - "what did this SKU make?" -
/// and they could not answer a partner at all: one order can carry two partners' goods, so a partner's part of the
/// order's money has always been worked out at READ time, by what their lines were worth against the whole order
/// (HC.Business.OrderMoney.Share). That is a way of guessing the split. This table is the split WRITTEN DOWN.
///
/// It is derived, and deliberately not maintained by whoever changes the money: one writer
/// (HC.Business.OrderItemMoneyWriter) recomputes an order's rows from the sources of truth - the payment row,
/// the parcels and their contents, and the order's own lines - whenever one of them changes, and the parts are
/// cut by what each line was worth with the odd paisa given to the largest remainders
/// (HC.Business.OrderMoney.Apportion), so they always add back up to the order figure they came from. Nothing is
/// ever incremented here: a capture, a settlement recon correction and a parcel's freight bill all produce the
/// same rows when recomputed, which is what makes running the writer twice harmless.
///
/// The gateway's charge is only known once the gateway has said it, so <see cref="GatewayFee"/>,
/// <see cref="GatewayTax"/> and <see cref="FreightShare"/> are null when nothing has reported them - unknown is
/// not the same as free, and a report that showed 0.00 for a parcel nobody has billed yet would be a lie.
/// <see cref="OutputGst"/> is not nullable because the tax inside what the customer paid is worked out from the
/// order line itself and is therefore always known.
/// </summary>
public partial class OrderItemMoney
{
    /// <summary>The order this line belongs to (the first half of the key, with <see cref="Sku"/>).</summary>
    public long OrderId { get; set; }

    /// <summary>The order line's SKU (<c>OrderItems.SKU</c> for this order) - the second half of the key.</summary>
    public string Sku { get; set; } = null!;

    /// <summary>
    /// The output GST this line carried: the tax inside what the customer paid for it, worked out from the line's
    /// own price, both discounts and the rate the checkout charged (the same arithmetic the Finance screen's
    /// OutputGst total is the sum of). Never null - a line the checkout charged no tax on really did carry none.
    /// </summary>
    public decimal OutputGst { get; set; }

    /// <summary>
    /// This line's part of what the gateway kept for taking the order's money (the MDR), or null when no payment
    /// of this order has a charge recorded.
    /// </summary>
    public decimal? GatewayFee { get; set; }

    /// <summary>
    /// This line's part of the GST the gateway charged on its own fee - input credit the shop can set against what
    /// it owes, or null when the gateway has not reported a charge for this order.
    /// </summary>
    public decimal? GatewayTax { get; set; }

    /// <summary>
    /// This line's part of what the couriers billed for the parcels that carried it. Null when none of those
    /// parcels has a bill recorded yet, which reads as 'not known' rather than 'carried for free'.
    /// </summary>
    public decimal? FreightShare { get; set; }

    /// <summary>
    /// Where <see cref="GatewayFee"/> and <see cref="GatewayTax"/> came from - the same vocabulary the payment row
    /// uses (<c>OrderPaymentCharges.FromPayment</c> for a capture's own report, <c>OrderPaymentCharges.FromRecon</c>
    /// for the settlement row the bank was actually paid on). Null when the order has no recorded gateway charge
    /// at all. Held here so a per-line figure can be read with the same trust as the payment it was cut from: a
    /// fee that is still the capture's estimate must not read like the bank's own figure.
    /// </summary>
    public string? ChargesSource { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public virtual Order Order { get; set; } = null!;
}
