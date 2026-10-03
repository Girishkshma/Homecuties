// ============================================================
// AdminDashboardService.Finance.cs
// Partial class: AdminDashboardService - the shop's own books
// ============================================================

using HC.Business.Dtos;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

/// <summary>
/// The shop's books over a period: what the customers paid, what was given back, what the gateway kept, what the
/// couriers were paid and what the shop's own margin on the goods was - read from the rows this system already
/// keeps, never from an assumption (see HC.Business.OrderMoney, which owns every money rule here, and
/// HC.Business.OrderPaymentCharges for the charges).
///
/// It is deliberately read-only: nothing on this screen moves money, an order or a parcel. The settlement ledger
/// is written by the pull alone (see RazorpaySettlements), which is the only thing here that asks the gateway -
/// so this screen reports what has already been written down, and says so when the pull has not run for the days
/// asked about.
/// </summary>
public partial class AdminDashboardService
{
    /// <summary>
    /// The shop's books over the asked-for period - the month to date when none is given (see
    /// <see cref="OrderMoney.ResolvePeriod"/>, the same rule the settlement pull's window resolves to) - as one
    /// answer: the customers' money, what the gateway kept for taking it, what the parcels cost, the shop's own
    /// declared margin, the bank's side of it from the settlement ledger, and the findings that make the numbers
    /// read right (<see cref="AdminFinanceSummaryDto.Messages"/>).
    ///
    /// Every figure is read at the day it belongs to rather than the day it was written down: a payment counts on
    /// the day the gateway took it, a refund on the day it was sent back, and a settlement line on the day it was
    /// settled - which is why the customer side and the bank side of the same week legitimately differ.
    /// </summary>
    public async Task<AdminFinanceSummaryDto> GetFinanceSummaryAsync(DateTime? from, DateTime? to)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (first, last) = OrderMoney.ResolvePeriod(
            from.HasValue ? DateOnly.FromDateTime(from.Value) : null,
            to.HasValue ? DateOnly.FromDateTime(to.Value) : null,
            today);

        // The days are read as UTC days (the payment rows are written in UTC - see OrderPayment) and the far end
        // is exclusive, so a day is [start, next day) whatever time of day a row was written at.
        var start = first.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusive = last.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // The money the gateway took, and the money given back, are two separate windows on the same rows on
        // purpose: a refund of an order paid before this period is money that left in this one, exactly as the
        // bank statement reads. Both windows are the rules OrderMoney owns (which states count as taken, which
        // refunds count as gone), so the Finance screen and the dashboard tiles can never disagree.
        var taken = _context.OrderPayments
            .AsNoTracking()
            .Where(p => OrderMoney.TakenStatuses.Contains(p.Status))
            .Where(p => (p.GatewayChargedOn ?? p.CreatedOn) >= start)
            .Where(p => (p.GatewayChargedOn ?? p.CreatedOn) < endExclusive);

        // Why the charges are counted by source rather than only summed: a charge can be the capture's own estimate
        // or the settlement recon figure the bank was actually paid on, and only the second is final (see
        // OrderPaymentCharges). The screen shows both counts, so a fee total with an estimate behind it is read
        // rather than trusted.
        var chargesBySource = await taken
            .GroupBy(p => p.ChargesSource)
            .Select(g => new { Source = g.Key, Count = g.Count() })
            .ToListAsync();

        var capturedCount = await taken.CountAsync();
        var grossSales = await taken.SumAsync(p => (decimal?)p.Amount) ?? 0;
        var gatewayFee = await taken.SumAsync(p => p.FeeAmount) ?? 0;
        var gatewayTax = await taken.SumAsync(p => p.TaxAmount) ?? 0;

        var refundsGiven = await _context.OrderPayments
            .AsNoTracking()
            .Where(p => p.RefundedOn != null && p.RefundAmount > 0)
            .Where(p => p.RefundedOn >= start && p.RefundedOn < endExclusive)
            .Select(p => p.RefundAmount!.Value)
            .ToListAsync();

        var netSales = OrderMoney.Net(grossSales, refundsGiven.Sum());
        var gatewayCharges = gatewayFee + gatewayTax;
        var fromTheGateway = OrderMoney.Net(netSales, gatewayCharges);

        // What the parcels of this period's money cost. The courier's own bill is kept on the parcel
        // (OrderShipments.FreightCharge - never shown to the customer), so an order whose money is in this period
        // brings its freight into it. Only the forward legs: a return carries a second parcel, and counting that
        // here would bill the same order twice.
        var freight = await _context.OrderShipments
            .AsNoTracking()
            .Where(s => s.Direction == OrderShipment.DirectionForward)
            .Where(s => s.Order.OrderPayments.Any(p =>
                OrderMoney.TakenStatuses.Contains(p.Status) &&
                (p.GatewayChargedOn ?? p.CreatedOn) >= start &&
                (p.GatewayChargedOn ?? p.CreatedOn) < endExclusive))
            .Select(s => s.FreightCharge)
            .ToListAsync();

        var freightCost = freight.Sum(amount => amount.GetValueOrDefault());
        var parcelsWithoutFreight = freight.Count(amount => amount == null);
        var afterCourier = OrderMoney.Net(fromTheGateway, freightCost);

        // The shop's own margin on the goods of those same orders - the figure the shop typed as 'Profit margin %'
        // on each product (HC.Business.OrderMoney.DeclaredProfit), which is a declared share of what the customer
        // paid and NOT a purchase price: nothing in this system records what the goods cost to buy.
        var lines = await _context.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Order.OrderPayments.Any(p =>
                OrderMoney.TakenStatuses.Contains(p.Status) &&
                (p.GatewayChargedOn ?? p.CreatedOn) >= start &&
                (p.GatewayChargedOn ?? p.CreatedOn) < endExclusive))
            .Select(oi => new { oi.UnitPrice, oi.ProfitMarginPercent })
            .ToListAsync();

        var declaredMargin = lines.Sum(l => OrderMoney.DeclaredProfit(l.UnitPrice, l.ProfitMarginPercent));
        var declaredCost = lines.Sum(l => OrderMoney.DeclaredCost(l.UnitPrice, l.ProfitMarginPercent));

        // The bank's side, read by the day each line was settled to the account (the pull writes them - see
        // RazorpaySettlements). Empty until the gateway's books have been pulled for these days, which is a thing
        // the screen says rather than a zero it shows.
        var settled = await _context.SettlementItems
            .AsNoTracking()
            .Where(i => i.SettledOn != null && i.SettledOn >= start && i.SettledOn < endExclusive)
            .Select(i => new { i.Credit, i.Debit, i.FeeAmount, i.TaxAmount, i.IsOnHold })
            .ToListAsync();

        var onHoldLines = settled.Count(i => i.IsOnHold == true);

        // The counts behind the fee and courier lines, so a figure that is short is read rather than guessed at.
        // Any other spelling of the source (including none at all) is a charge nothing has recorded.
        var chargeSources = chargesBySource.Select(g => (g.Source, g.Count)).ToList();
        var fromRecon = ChargeCount(chargeSources, OrderPaymentCharges.FromRecon);
        var estimated = ChargeCount(chargeSources, OrderPaymentCharges.FromPayment);
        var unknown = capturedCount - fromRecon - estimated;

        var messages = new List<string>();

        if (estimated > 0)
        {
            messages.Add(
                $"{estimated} of {capturedCount} payments in this period still carry the charge the capture itself reported; " +
                "the settlement pull has not corrected them to what the bank was settled on yet, so the two fee lines can still move. " +
                "Pull the gateway's books from the Dashboard to bring them up to date.");
        }

        if (unknown > 0)
        {
            messages.Add(
                $"{unknown} of {capturedCount} payments in this period have no charge recorded at all, so the two fee lines are " +
                "short by whatever the gateway kept for them.");
        }

        if (parcelsWithoutFreight > 0)
        {
            messages.Add(
                $"{parcelsWithoutFreight} of {freight.Count} parcels have no courier bill recorded, so the courier cost is short by " +
                "those - the figure goes on the parcel, from its Shipment card on the order screen.");
        }

        if (settled.Count == 0)
        {
            messages.Add(
                "No settlement lines are recorded as settled between these days, so the bank side of this period is empty - pull the " +
                "gateway's books from the Dashboard to see what really reached the account.");
        }
        else if (onHoldLines > 0)
        {
            messages.Add(
                $"{onHoldLines} of those settlement lines are on hold at Razorpay and have not been paid into the bank account yet.");
        }

        if (capturedCount == 0)
        {
            messages.Add("No payment was taken between these days, so every figure here is zero because nothing was sold.");
        }

        return new AdminFinanceSummaryDto
        {
            From = start,
            To = last.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            CapturedCount = capturedCount,
            GrossSales = grossSales,
            RefundCount = refundsGiven.Count,
            RefundsGiven = refundsGiven.Sum(),
            NetSales = netSales,
            GatewayFee = gatewayFee,
            GatewayTax = gatewayTax,
            GatewayCharges = gatewayCharges,
            FromTheGateway = fromTheGateway,
            SettledChargeCount = fromRecon,
            EstimatedChargeCount = estimated,
            UnknownChargeCount = unknown,
            ParcelsShipped = freight.Count,
            ParcelsWithoutFreight = parcelsWithoutFreight,
            FreightCost = freightCost,
            AfterCourier = afterCourier,
            DeclaredMargin = declaredMargin,
            DeclaredCost = declaredCost,
            SettledLines = settled.Count,
            SettledCredit = settled.Sum(i => i.Credit.GetValueOrDefault()),
            SettledDebit = settled.Sum(i => i.Debit.GetValueOrDefault()),
            SettledFee = settled.Sum(i => i.FeeAmount.GetValueOrDefault()),
            SettledTax = settled.Sum(i => i.TaxAmount.GetValueOrDefault()),
            SettledOnHold = onHoldLines,
            Messages = messages.ToArray()
        };
    }

    /// <summary>
    /// How many of a period's payments carry their charge from the given source (see OrderPaymentCharges) - the
    /// count that lets the screen say whether its fee lines are the settlement's own figures or still the
    /// captures' estimates, and how many rows have nothing recorded at all.
    /// </summary>
    private static int ChargeCount(IEnumerable<(string? Source, int Count)> chargesBySource, string source)
    {
        foreach (var (chargeSource, count) in chargesBySource)
        {
            if (string.Equals(chargeSource, source, StringComparison.OrdinalIgnoreCase))
                return count;
        }

        return 0;
    }
}
