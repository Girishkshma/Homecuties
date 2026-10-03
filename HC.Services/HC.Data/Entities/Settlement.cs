using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

/// <summary>
/// One settlement Razorpay made to the shop's bank account (<c>setl_xxx</c>): the money that reached the
/// bank, the bank's own reference for it and Razorpay's status of it.
///
/// Until now nothing in the system knew what was settled: a payment was taken by the gateway, the customer
/// was told the order was paid, and the money turned up in the bank a day or two later as a round figure
/// nobody could break down - the gateway's own charge, the GST on it, a refund that netted off, a
/// chargeback. The settlement report is that breakdown, and this is its header row (the lines are
/// <see cref="SettlementItem"/>, written by <c>HC.Business.RazorpaySettlements.SyncAsync</c>).
///
/// A settlement is written from Razorpay's settlement list (<c>GET /v1/settlements/</c>), so an amount of 0
/// here is normal - most settlements have their fees taken per payment and report no totals of their own.
/// What is NOT normal is not seeing a settlement at all: money that left the gateway and has no row here is
/// money the books cannot account for, which is why the reconciliation screen counts the days it pulled.
/// </summary>
public partial class Settlement
{
    public long SettlementId { get; set; }

    /// <summary>Razorpay's own id of the settlement (<c>setl_xxx</c>), the id every line of it carries.</summary>
    public string RazorpaySettlementId { get; set; } = null!;

    /// <summary>
    /// The bank's reference for the transfer (Razorpay's <c>utr</c>) - the thread a figure on the bank
    /// statement is followed back to the payments behind it with. Null until Razorpay reports it.
    /// </summary>
    public string? Utr { get; set; }

    /// <summary>What was settled, as Razorpay speaks it (paise).</summary>
    public int? AmountInPaise { get; set; }

    /// <summary>What was settled, in rupees - the figure that appears in the bank.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Razorpay's own fee total for the settlement, in paise (0 for a normal settlement).</summary>
    public int? FeesInPaise { get; set; }

    /// <summary>Razorpay's own fee total for the settlement, in rupees.</summary>
    public decimal? Fees { get; set; }

    /// <summary>GST on the fee total above, in paise.</summary>
    public int? TaxInPaise { get; set; }

    /// <summary>GST on the fee total above, in rupees.</summary>
    public decimal? Tax { get; set; }

    public string? Currency { get; set; }

    /// <summary>Razorpay's status of the settlement: <c>created</c>, <c>processed</c> or <c>failed</c>.</summary>
    public string? Status { get; set; }

    /// <summary>When Razorpay created the settlement (its own <c>created_at</c>).</summary>
    public DateTime? GatewayCreatedOn { get; set; }

    /// <summary>
    /// The day its lines were settled to the bank - the earliest <c>settled_at</c> Razorpay reported for
    /// them (the settlement entity itself carries no settled date). Only ever moved earlier by a re-pull,
    /// so pulling the same days again cannot make the books jump.
    /// </summary>
    public DateTime? SettledOn { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public virtual ICollection<SettlementItem> SettlementItems { get; set; } = new List<SettlementItem>();
}
