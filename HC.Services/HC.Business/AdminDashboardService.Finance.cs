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
/// couriers were paid, what the shop's own margin on the goods was, and what of that money is held for the
/// government as GST - read from the rows this system already keeps, never from an assumption (see
/// HC.Business.OrderMoney, which owns every money rule here, and HC.Business.OrderPaymentCharges for the charges).
///
/// It is deliberately read-only: nothing on this screen moves money, an order or a parcel. The settlement ledger
/// is written by the pull alone (see RazorpaySettlements), which is the only thing here that asks the gateway -
/// so this screen reports what has already been written down, and says so when the pull has not run for the days
/// asked about.
///
/// Whose books come back is the acting admin's own business rather than a parameter of the period: an admin or a
/// super admin is given the whole shop's, and a user the shop has linked to a partner that partner's own sales
/// alone. Only the one method below concerns itself with that - it is one rule, stated where the reads are, and the
/// dashboard tiles are deliberately shop-wide.
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
    ///
    /// WHOSE BOOKS these are is decided by the acting admin and never by the caller: an admin or a super admin is
    /// answered the whole shop's, and a user the shop has linked to a partner that partner's own sales alone
    /// (<see cref="ResolveFinanceScopeAsync"/> says how the two are told apart, without this file having to know what
    /// a role is called). A partner's sale is the goods their own stock supplied - an order line names a SKU, and the
    /// SKU sits in one of their inventories (<c>Sku.Inventory.PartnerId</c>) - and NOT the order's own seller,
    /// because the checkout writes every order as partner 1 (see OrderService.Orders, 'Default seller'), so reading
    /// the seller would hand every partner the whole shop.
    ///
    /// One order can carry two partners' goods, so a partner's money is not read off the order but worked out from
    /// it: what the customer paid, what was given back, the gateway's charge for taking it and the courier's bill are
    /// all recorded against the ORDER, and each partner is given their own lines' share of them by what those lines
    /// were worth against the whole order (<see cref="OrderMoney.Share"/>). A partner's own lines are not shared at
    /// all - their sales value, tax and declared margin are their lines' own figures - and nothing of another
    /// partner's shows on their screen while the two screens between them still account for the order.
    /// </summary>
    public async Task<AdminFinanceSummaryDto> GetFinanceSummaryAsync(DateTime? from, DateTime? to, long adminUserId)
    {
        // Who is asking decides what can be answered, so it is resolved first - from the acting admin's own link to
        // a partner and never from the request (see AdminController, which reads the id off the validated token).
        var scope = await ResolveFinanceScopeAsync(adminUserId);
        var partnerIds = scope?.PartnerIds;

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

        // The payments are read whole rather than summed in the database, because how much of one of them is these
        // books' money is decided by the order it was taken against - a figure the database cannot work out without
        // being told whose books these are, which is the one thing this read knows and the query does not. For the
        // whole shop's books every figure below is the row's own, exactly as a sum would have been.
        var payments = await taken
            .Select(p => new { p.OrderId, p.Amount, Fee = p.FeeAmount, Tax = p.TaxAmount, p.ChargesSource })
            .ToListAsync();

        // Each refund is read with the order it belongs to as well as its figure: the tax a refund gives back is
        // that order's own share of the GST (see OrderMoney.GstGivenBack), which needs the order it came from - and
        // so does the partner's own share of the refund.
        var refundsGiven = await _context.OrderPayments
            .AsNoTracking()
            .Where(p => p.RefundedOn != null && p.RefundAmount > 0)
            .Where(p => p.RefundedOn >= start && p.RefundedOn < endExclusive)
            .Select(p => new { p.OrderId, Amount = p.RefundAmount!.Value })
            .ToListAsync();

        // The bank's side, read by the day each line was settled to the account (the pull writes them - see
        // RazorpaySettlements). Empty until the gateway's books have been pulled for these days, which is a thing
        // the screen says rather than a zero it shows.
        //
        // Read here rather than with the figures further down because a line names the gateway's payment and the
        // payment names our order, and for a partner that is the only thing that can show a bank line to be theirs -
        // so which of the lines count depends on this read, while the line figures themselves come later.
        var settledRows = await _context.SettlementItems
            .AsNoTracking()
            .Where(i => i.SettledOn != null && i.SettledOn >= start && i.SettledOn < endExclusive)
            .Select(i => new
            {
                i.RazorpayPaymentId,
                i.Credit,
                i.Debit,
                i.FeeAmount,
                i.TaxAmount,
                i.IsOnHold
            })
            .ToListAsync();

        // Which of our orders the period's bank lines came from - the gateway's payment id as the pull wrote it into
        // the settlement line (<see cref="SettlementItem.RazorpayPaymentId"/>). Only a partner's books need it: the
        // shop's own answer takes every line as it stands, without asking whose goods it was for.
        var orderByGatewayPayment = new Dictionary<string, long>(StringComparer.Ordinal);

        if (scope != null)
        {
            var gatewayPaymentIds = settledRows
                .Select(line => line.RazorpayPaymentId)
                .Where(id => id != null)
                .Select(id => id!)
                .Distinct()
                .ToList();

            orderByGatewayPayment = await _context.OrderPayments
                .AsNoTracking()
                .Where(p => p.RazorpayPaymentId != null && gatewayPaymentIds.Contains(p.RazorpayPaymentId))
                .Select(p => new { Payment = p.RazorpayPaymentId!, p.OrderId })
                .ToDictionaryAsync(p => p.Payment, p => p.OrderId, StringComparer.Ordinal);
        }

        // The order lines every figure below is worked out from, read once for every order any of those figures
        // names: the orders this period's money was taken for, the orders this period's refunds came back from (they
        // can be older than the period), and, for a partner, the orders the period's bank lines came from.
        //
        // They answer two questions with the same rows, which is why they are read together: what these books' own
        // sales were (the lines whose SKU sits in the partner's own inventories) and what share of each order was
        // theirs (their lines' worth against the whole order's lines' worth).
        var periodOrderIds = payments.Select(payment => payment.OrderId).Distinct().ToList();
        var refundedOrderIds = refundsGiven.Select(refund => refund.OrderId).Distinct().ToList();

        var ordersToRead = periodOrderIds
            .Concat(refundedOrderIds)
            .Concat(orderByGatewayPayment.Values)
            .Distinct()
            .ToList();

        // The SKU is read as its own code and deliberately NOT joined to its inventory here: a line whose SKU row has
        // gone would be dropped by such a join, and a dropped line quietly shrinks the order's own worth - and with it
        // every share cut from the order, including the other partner's. The code is mapped to its partner below
        // instead, for the partner's books alone, which are the only ones that ask the question.
        var orderLines = await _context.OrderItems
            .AsNoTracking()
            .Where(oi => ordersToRead.Contains(oi.OrderId))
            .Select(oi => new
            {
                oi.OrderId,
                oi.Sku,
                oi.UnitPrice,
                oi.ProfitMarginPercent,
                oi.DiscountPercent,
                oi.AdditionalDiscountPercent,
                oi.Cgstpercent,
                oi.Sgstpercent,
                oi.Igstpercent
            })
            .ToListAsync();

        // Which partner each of those SKUs belongs to: a SKU sits in one inventory and the inventory belongs to one
        // partner (<c>Sku.InventoryId</c> -> <c>Inventory.PartnerId</c>), and it is that link - never the order's own
        // seller - that makes a line the partner's own goods. Read only for a partner's books; the whole shop's need
        // no mapping.
        var partnerBySku = new Dictionary<string, int>(StringComparer.Ordinal);

        if (scope != null)
        {
            var skuCodes = orderLines.Select(line => line.Sku).Distinct().ToList();

            partnerBySku = await _context.Skus
                .AsNoTracking()
                .Where(sku => skuCodes.Contains(sku.Sku1))
                .Select(sku => new { sku.Sku1, sku.Inventory.PartnerId })
                .ToDictionaryAsync(sku => sku.Sku1, sku => sku.PartnerId, StringComparer.Ordinal);
        }

        // Whether a line's goods are these books' own. A SKU the shop has no row for belongs to nobody, so it is not
        // theirs - the line still counts towards the order's own worth, which is what every share is cut against.
        bool IsTheirLine(string sku) =>
            partnerIds != null && partnerBySku.TryGetValue(sku, out var skuPartner) && partnerIds.Contains(skuPartner);

        // What each order's lines were worth to the customer, and the part of that which is these books' own - the
        // weight every share below is taken by. Nothing is worked out for the whole shop's books: there is no share to
        // take, and every figure is the row's own.
        var lineValues = new Dictionary<long, (decimal Whole, decimal Part)>();

        if (partnerIds != null)
        {
            foreach (var order in orderLines.GroupBy(line => line.OrderId))
            {
                var whole = order.Sum(line => OrderMoney.LineValue(
                    line.UnitPrice, line.DiscountPercent, line.AdditionalDiscountPercent,
                    line.Cgstpercent, line.Sgstpercent, line.Igstpercent));

                var part = order
                    .Where(line => IsTheirLine(line.Sku))
                    .Sum(line => OrderMoney.LineValue(
                        line.UnitPrice, line.DiscountPercent, line.AdditionalDiscountPercent,
                        line.Cgstpercent, line.Sgstpercent, line.Igstpercent));

                lineValues[order.Key] = (whole, part);
            }
        }

        // These books' part of an order-level figure: all of it for the whole shop's books, and a partner's own
        // lines' share of it for a partner - the one division this screen does (OrderMoney.Share). An order whose
        // lines were not read above cannot be shown to be these books' at all, so its money is nothing rather than a
        // guess; and an order worth nothing divides by zero, which Share answers with nothing as well.
        decimal TheirPartOf(long orderId, decimal amount) =>
            partnerIds == null
                ? amount
                : lineValues.TryGetValue(orderId, out var values)
                    ? OrderMoney.Share(amount, values.Part, values.Whole)
                    : 0m;

        // Whether an order carries any of these books' goods at all. An order that carries none is not a sale of
        // theirs, so its payment and its parcel are left out of their figures rather than counted as a nothing - which
        // is what keeps their counts of sold orders and of refunds plain.
        bool CarriesTheseGoods(long orderId) =>
            partnerIds == null ||
            (lineValues.TryGetValue(orderId, out var values) && values.Part > 0m);

        // Why the charges are counted by source rather than only summed: a charge can be the capture's own estimate
        // or the settlement recon figure the bank was actually paid on, and only the second is final (see
        // OrderPaymentCharges). The screen shows both counts, so a fee total with an estimate behind it is read
        // rather than trusted. A partner's own fees are the charges on their own lines' orders only.
        var chargesBySource = payments
            .Where(payment => CarriesTheseGoods(payment.OrderId))
            .GroupBy(payment => payment.ChargesSource)
            .Select(group => (Source: group.Key, Count: group.Count()))
            .ToList();

        var capturedCount = payments.Count(payment => CarriesTheseGoods(payment.OrderId));
        var grossSales = payments.Sum(payment => TheirPartOf(payment.OrderId, payment.Amount));
        var gatewayFee = payments.Sum(payment => TheirPartOf(payment.OrderId, payment.Fee.GetValueOrDefault()));
        var gatewayTax = payments.Sum(payment => TheirPartOf(payment.OrderId, payment.Tax.GetValueOrDefault()));

        // Only the refunds on their own orders are theirs, and each of those only to the extent of their own lines in
        // it - the same rule the money taken above is cut by.
        var refundedAmount = refundsGiven.Sum(refund => TheirPartOf(refund.OrderId, refund.Amount));
        var refundCount = refundsGiven.Count(refund => CarriesTheseGoods(refund.OrderId));

        var netSales = OrderMoney.Net(grossSales, refundedAmount);
        var gatewayCharges = gatewayFee + gatewayTax;
        var fromTheGateway = OrderMoney.Net(netSales, gatewayCharges);

        // What the parcels of this period's money cost. The courier's own bill is kept on the parcel
        // (OrderShipments.FreightCharge - never shown to the customer), so an order whose money is in this period
        // brings its freight into it. Only the forward legs: a return carries a second parcel, and counting that
        // here would bill the same order twice.
        //
        // The parcel is the ORDER's, so a partner is given the courier's bill for their own lines' share of it - the
        // same rule as every other order-level figure here - and a parcel whose order carries none of their goods is
        // not counted as one of theirs at all.
        var freight = await _context.OrderShipments
            .AsNoTracking()
            .Where(s => s.Direction == OrderShipment.DirectionForward)
            .Where(s => s.Order.OrderPayments.Any(p =>
                OrderMoney.TakenStatuses.Contains(p.Status) &&
                (p.GatewayChargedOn ?? p.CreatedOn) >= start &&
                (p.GatewayChargedOn ?? p.CreatedOn) < endExclusive))
            .Select(s => new { s.OrderId, s.FreightCharge })
            .ToListAsync();

        // The parcels of these books' own sales - the whole period's for the shop, and only those whose order carries
        // the partner's goods for a partner.
        var parcels = freight.Where(parcel => CarriesTheseGoods(parcel.OrderId)).ToList();

        var freightCost = parcels.Sum(parcel => TheirPartOf(parcel.OrderId, parcel.FreightCharge.GetValueOrDefault()));
        var parcelsWithoutFreight = parcels.Count(parcel => parcel.FreightCharge == null);
        var afterCourier = OrderMoney.Net(fromTheGateway, freightCost);

        // The shop's own margin on the goods of those same orders - the figure the shop typed as 'Profit margin %'
        // on each product (HC.Business.OrderMoney.DeclaredProfit), which is a declared share of what the customer
        // paid and NOT a purchase price: nothing in this system records what the goods cost to buy.
        //
        // The same rows carry the two rates the checkout charged the customer at and both discounts, so the tax
        // inside that money is worked out from them below rather than asked for again.
        //
        // They were already read above, with the period's other lines, and are taken from there rather than asked for
        // a second time: one read means the sales the margin is worked out on and the sales the shares are cut from
        // can never turn out to be two different sets of rows.
        //
        // What is these books' own is decided here: the period's orders (never the older orders a refund came from -
        // those are read for their tax below), and for a partner only their own lines within them. Another partner's
        // goods in a shared order are not their sales.
        var periodOrderIdSet = periodOrderIds.ToHashSet();
        var periodLines = orderLines.Where(line => periodOrderIdSet.Contains(line.OrderId)).ToList();
        var scopeLines = partnerIds == null
            ? periodLines
            : periodLines.Where(line => IsTheirLine(line.Sku)).ToList();

        var declaredMargin = scopeLines.Sum(l => OrderMoney.DeclaredProfit(l.UnitPrice, l.ProfitMarginPercent));
        var declaredCost = scopeLines.Sum(l => OrderMoney.DeclaredCost(l.UnitPrice, l.ProfitMarginPercent));

        // What the customers paid includes the GST the shop is holding for the government - it was never the shop's
        // money, and until now the screen had no line for it. (The one GST line it did have is the opposite way
        // round: the GST the GATEWAY charged on its fee, which is a credit the shop can claim.)
        //
        // It is read line by line from the rates the order snapshotted, at the rate the checkout actually charged
        // (OrderMoney.ChargedGstRate - the CGST rate alone), because that is the tax the customer really paid. It is
        // these books' own lines it is read from, so a partner holds the tax on their own goods: the other partner's
        // share of a shared order is that partner's to hold, and never appears twice.
        var taxableValue = 0m;
        var outputGst = 0m;
        var linesWithUnchargedRates = 0;

        foreach (var line in scopeLines)
        {
            var taxable = OrderMoney.TaxableValue(line.UnitPrice, line.DiscountPercent, line.AdditionalDiscountPercent);
            taxableValue += taxable;
            outputGst += OrderMoney.GstOn(taxable, OrderMoney.ChargedGstRate(line.Cgstpercent, line.Sgstpercent, line.Igstpercent));

            if (line.Sgstpercent > 0m || line.Igstpercent > 0m)
                linesWithUnchargedRates++;
        }

        // A refund gives the tax back with the goods, so the orders its refunds came from are read too - they can be
        // older than this period, and a refund of a sale made last month still gives its tax back in this one. The
        // lines are grouped by order so each refund gives back its own order's share.
        //
        // Their lines were read with the rest above (the orders can be older, but the rows are the same ones), so the
        // two answers cannot be read from two different sets of lines.
        var refundedOrderIdSet = refundedOrderIds.ToHashSet();
        var linesOfRefundedOrders = orderLines
            .Where(line => refundedOrderIdSet.Contains(line.OrderId))
            .GroupBy(line => line.OrderId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var gstOnRefunds = refundsGiven.Sum(refund =>
        {
            if (!linesOfRefundedOrders.TryGetValue(refund.OrderId, out var linesOfOrder))
                return 0m;

            var orderTaxable = linesOfOrder.Sum(l => OrderMoney.TaxableValue(l.UnitPrice, l.DiscountPercent, l.AdditionalDiscountPercent));
            var orderGst = linesOfOrder.Sum(l => OrderMoney.GstOn(OrderMoney.TaxableValue(l.UnitPrice, l.DiscountPercent, l.AdditionalDiscountPercent), l.Cgstpercent));

            // What that order was charged: its value at the rate the checkout charged, which is what the refund is
            // a share of (the payment row's own amount would do for a single payment, but this holds for any).
            //
            // The tax a refund gives back belongs to the ORDER, so a partner's books give back their own lines' share
            // of it - the same rule as every other order-level figure - and a refund on an order carrying none of
            // their goods gives back nothing of theirs.
            return TheirPartOf(refund.OrderId, OrderMoney.GstGivenBack(refund.Amount, orderGst, orderTaxable + orderGst));
        });

        var gstHeld = OrderMoney.GstHeld(outputGst, gstOnRefunds);

        // The only credit the books can count: the GST the gateway charged on its own fee, recorded on the payment
        // row (and corrected by the settlement pull). The couriers' bills carry no tax split, so theirs is not here.
        var netGstPayable = OrderMoney.NetGstPayable(gstHeld, gatewayTax);

        // The bank's side of the period. The rows were read above (the pull writes them - see RazorpaySettlements), and
        // what of them is these books' is decided here: the shop's own answer takes every line as it stands, while a
        // partner's takes only the lines the gateway took against their own goods, each cut to their share of the order
        // it came from.
        //
        // A line names the gateway's payment and the payment names our order, which is the only thing that can show a
        // bank line to be theirs at all. A line whose payment this shop has no row for, or whose order carries none of
        // their goods, cannot be shown to be theirs: it is counted as unattributed rather than guessed at, and the
        // screen says so rather than let their bank side read as short without explanation.
        var bankLines = new List<(long OrderId, decimal Credit, decimal Debit, decimal Fee, decimal Tax, bool IsOnHold)>();
        var unattributedSettlementLines = 0;

        foreach (var line in settledRows)
        {
            // The order the line came from. Left at zero for the shop's own books, whose figures are the rows' own and
            // never cut (TheirPartOf returns the amount as it stands there).
            var orderId = 0L;

            if (scope != null)
            {
                if (line.RazorpayPaymentId == null ||
                    !orderByGatewayPayment.TryGetValue(line.RazorpayPaymentId, out orderId) ||
                    !CarriesTheseGoods(orderId))
                {
                    unattributedSettlementLines++;
                    continue;
                }
            }

            bankLines.Add((
                orderId,
                line.Credit.GetValueOrDefault(),
                line.Debit.GetValueOrDefault(),
                line.FeeAmount.GetValueOrDefault(),
                line.TaxAmount.GetValueOrDefault(),
                line.IsOnHold == true));
        }

        var onHoldLines = bankLines.Count(line => line.IsOnHold);

        // The counts behind the fee and courier lines, so a figure that is short is read rather than guessed at.
        // Any other spelling of the source (including none at all) is a charge nothing has recorded.
        var chargeSources = chargesBySource.Select(g => (g.Source, g.Count)).ToList();
        var fromRecon = ChargeCount(chargeSources, OrderPaymentCharges.FromRecon);
        var estimated = ChargeCount(chargeSources, OrderPaymentCharges.FromPayment);
        var unknown = capturedCount - fromRecon - estimated;

        var messages = new List<string>();

        // Whose books these are is said first, so a figure is never read as the whole shop's when it is one partner's.
        // (The shop's own answer says nothing here: it is the one every other screen already speaks in.)
        if (scope != null)
        {
            messages.Add(
                $"These are {scope.PartnerName}'s own books: every figure is their share of the sales their own goods supplied, " +
                "not the whole order's, and the bank side counts only what the gateway settled against their own goods. A shared " +
                "order's other partner reads that partner's own share.");

            if (unattributedSettlementLines > 0)
            {
                messages.Add(
                    $"{unattributedSettlementLines} settlement line(s) in this period could not be tied to their own goods, so the bank " +
                    "side of this answer is short by whatever the gateway settled on them - the shop-wide report still counts them.");
            }
        }

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
                $"{parcelsWithoutFreight} of {parcels.Count} parcels have no courier bill recorded, so the courier cost is short by " +
                "those - the figure goes on the parcel, from its Shipment card on the order screen.");
        }

        if (bankLines.Count == 0)
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

        // The GST findings. Each one is a way the tax line can be short or absent, said in words so a shop keeper
        // reads a zero as a fact about the data rather than as no tax being owed.
        if (scopeLines.Count > 0 && outputGst == 0m)
        {
            messages.Add(
                "No GST is recorded on this period's order lines, so nothing here is held for the government - either the " +
                "shop is not registered and its products carry no CGST, or the rate is missing on the product form.");
        }

        if (linesWithUnchargedRates > 0)
        {
            messages.Add(
                $"{linesWithUnchargedRates} of {scopeLines.Count} lines in this period record an SGST or IGST rate beside their CGST " +
                "rate, but the checkout charges the CGST rate alone - so the GST here is the CGST the customer actually paid. " +
                "If a line's tax is meant to be CGST + SGST, more is owed than was collected.");
        }

        if (freightCost > 0m)
        {
            messages.Add(
                "The couriers' bills are recorded without a GST split, so the tax on them is not counted as input credit here - " +
                "the GST on the gateway's fee is the only credit the net figure sets off.");
        }

        // The money and the lines are two views of one period's sales and are expected to agree. They can legitimately
        // differ, though: the checkout charges ONE GST rate across the whole cart (the first line's - see
        // CartService.Calculation), so a cart that mixed rates cannot be re-added line by line to the same rupee, and a
        // payment recorded at what was really taken after a short capture is smaller than its own lines. Either way the
        // difference is said rather than smoothed over.
        var moneyVersusLines = grossSales - (taxableValue + outputGst);

        if (capturedCount > 0 && Math.Abs(moneyVersusLines) > 1m)
        {
            messages.Add(
                $"The money taken and the sales worked out line by line differ by {moneyVersusLines:N2}: the checkout charges " +
                "one GST rate across the whole cart, and a payment whose captured amount was short is recorded at what was " +
                "really taken - either can put the two apart.");
        }

        // The per-unit breakdown: what each SKU of this period's sales really carried, read from the rows the writer
        // keeps (OrderItemMoney - see OrderItemMoneyWriter) rather than worked out again here. The orders are the
        // ones this period's money was taken for - the same set every figure above is about - and, for a partner,
        // only their own goods among them, so this table is cut to the same books the summary is.
        //
        // The figure is what the shop RECORDS now rather than a read-time guess, which is what makes it worth
        // showing per SKU: it is the split the books were written with, and it adds back up to the order-level
        // totals to the paisa (OrderMoney.Apportion).
        var lineMoneyRows = await _context.OrderItemMoney
            .AsNoTracking()
            .Where(money => periodOrderIdSet.Contains(money.OrderId))
            .Select(money => new
            {
                money.OrderId,
                money.Sku,
                money.OutputGst,
                money.GatewayFee,
                money.GatewayTax,
                money.FreightShare,
                money.ChargesSource
            })
            .ToListAsync();

        var moneyRows = lineMoneyRows
            .Where(money => partnerIds == null || IsTheirLine(money.Sku))
            .ToList();

        // What each SKU's units were worth to the customer in this period - the same weights the summary's shares
        // were cut by, read from the very same lines - so the breakdown and the summary can never be about two
        // different sets of goods.
        var weightBySku = scopeLines
            .GroupBy(line => line.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (
                    Units: group.Count(),
                    Value: group.Sum(line => OrderMoney.LineValue(
                        line.UnitPrice, line.DiscountPercent, line.AdditionalDiscountPercent,
                        line.Cgstpercent, line.Sgstpercent, line.Igstpercent))),
                StringComparer.OrdinalIgnoreCase);

        var lineItems = moneyRows
            .GroupBy(money => money.Sku, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var weight = weightBySku.TryGetValue(group.Key, out var found) ? found : (Units: 0, Value: 0m);

                return new AdminFinanceLineDto
                {
                    Sku = group.Key,
                    Units = weight.Units,
                    LineValue = weight.Value,
                    OutputGst = group.Sum(money => money.OutputGst),
                    GatewayFee = SumOfWhatIsKnown(group.Select(money => money.GatewayFee)),
                    GatewayTax = SumOfWhatIsKnown(group.Select(money => money.GatewayTax)),
                    FreightShare = SumOfWhatIsKnown(group.Select(money => money.FreightShare)),
                    ChargesSource = WeakestChargesSource(group.Select(money => money.ChargesSource))
                };
            })
            .OrderByDescending(row => row.LineValue)
            .ThenBy(row => row.Sku, StringComparer.Ordinal)
            .ToList();

        // The orders these books' breakdown cannot speak for: the ones this period's money was taken for that have
        // no per-unit rows of their own goods, because nothing has touched them since the table was created. The
        // summary figures above are read from the payment rows and are unaffected - it is only the breakdown that
        // needs saying.
        var ordersInScope = periodOrderIds.Where(CarriesTheseGoods).Distinct().ToList();
        var ordersWithTheirOwnRows = moneyRows.Select(money => money.OrderId).Distinct().ToHashSet();
        var lineItemsWithoutMoney = ordersInScope.Count(orderId => !ordersWithTheirOwnRows.Contains(orderId));

        if (lineItemsWithoutMoney > 0)
        {
            messages.Add(
                $"{lineItemsWithoutMoney} of {ordersInScope.Count} order(s) in this period have no per-unit figures " +
                "written yet, so the breakdown below is short by their lines - they were sold before the shop began " +
                "recording them. Run BackfillOrderItemMoney.sql (HC.Services/HC.Data/Scripts) once, and they will " +
                "be in it on the next read.");
        }

        // Which partner the answer names, when there is exactly one to name: a person the shop has linked to several
        // partners reads them together, and the name below is then the several of them rather than a single id.
        var namedPartnerId = scope != null && scope.PartnerIds.Count == 1 ? scope.PartnerIds.First() : (int?)null;

        return new AdminFinanceSummaryDto
        {
            From = start,
            To = last.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            PartnerId = namedPartnerId,
            PartnerName = scope?.PartnerName,
            CapturedCount = capturedCount,
            GrossSales = grossSales,
            RefundCount = refundCount,
            RefundsGiven = refundedAmount,
            NetSales = netSales,
            GatewayFee = gatewayFee,
            GatewayTax = gatewayTax,
            GatewayCharges = gatewayCharges,
            FromTheGateway = fromTheGateway,
            SettledChargeCount = fromRecon,
            EstimatedChargeCount = estimated,
            UnknownChargeCount = unknown,
            ParcelsShipped = parcels.Count,
            ParcelsWithoutFreight = parcelsWithoutFreight,
            FreightCost = freightCost,
            AfterCourier = afterCourier,
            DeclaredMargin = declaredMargin,
            DeclaredCost = declaredCost,
            TaxableValue = taxableValue,
            OutputGst = outputGst,
            GstOnRefunds = gstOnRefunds,
            GstHeld = gstHeld,
            NetGstPayable = netGstPayable,
            SettledLines = bankLines.Count,
            SettledCredit = bankLines.Sum(line => TheirPartOf(line.OrderId, line.Credit)),
            SettledDebit = bankLines.Sum(line => TheirPartOf(line.OrderId, line.Debit)),
            SettledFee = bankLines.Sum(line => TheirPartOf(line.OrderId, line.Fee)),
            SettledTax = bankLines.Sum(line => TheirPartOf(line.OrderId, line.Tax)),
            SettledOnHold = onHoldLines,
            LineItemsWithoutMoney = lineItemsWithoutMoney,
            LineItems = lineItems,
            Messages = messages.ToArray()
        };
    }

    /// <summary>
    /// Whose books the acting admin may read: the partners the shop has linked them to, or the whole shop's when the
    /// shop has linked them to none - which is what an admin or a super admin is, and the only thing that makes an
    /// answer shop-wide.
    ///
    /// The link is the shop's own row (<c>PartnersUser</c>) and its <c>IsActive</c> flag, and NOT a role name: who
    /// reads whose books is then decided by what the shop has written down about the person, so a new role or a
    /// renamed one cannot quietly change it, and a deactivated link is a link nobody holds.
    ///
    /// The id must be the acting admin's own, read from the validated token (see AdminController.GetFinanceSummary):
    /// a caller that could name the user could name any partner and read that partner's books.
    /// </summary>
    private async Task<FinanceScope?> ResolveFinanceScopeAsync(long adminUserId)
    {
        var links = await _context.PartnersUsers
            .AsNoTracking()
            .Where(link => link.UserId == adminUserId && link.IsActive)
            .Select(link => new { link.PartnerId, link.Partner.PartnerName })
            .ToListAsync();

        if (links.Count == 0)
            return null;

        // Ordered and de-duplicated by name so the label reads the same on every call.
        var partners = links
            .GroupBy(link => link.PartnerId)
            .Select(group => (PartnerId: group.Key, PartnerName: group.First().PartnerName))
            .OrderBy(partner => partner.PartnerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var label = partners.Count == 1
            ? partners[0].PartnerName
            : string.Join(", ", partners.Select(partner => partner.PartnerName));

        return new FinanceScope(partners.Select(partner => partner.PartnerId).ToHashSet(), label);
    }

    /// <summary>
    /// The sum of the figures that are actually known, or null when none of them is - never a 0 standing in for
    /// 'nobody has told us' (a capture's fee is worked out a little after the capture, and a parcel is billed when
    /// the courier gets round to it). One row knowing its figure and another not is summed for what IS known, which
    /// is the best answer there is - the charge counts and messages beside it are what say how thin it is.
    /// </summary>
    private static decimal? SumOfWhatIsKnown(IEnumerable<decimal?> figures)
    {
        var known = figures.Where(figure => figure != null).ToList();

        return known.Count == 0 ? null : known.Sum(figure => figure!.Value);
    }

    /// <summary>
    /// The weakest word among the sources behind a group of rows - what the whole total has to be trusted as far as
    /// (see OrderPaymentCharges). One figure still carrying the capture's own estimate makes the group's total an
    /// estimate however many of the rows beside it are the settlement recon's; none of them knowing means the group
    /// has no recorded gateway charge at all, which is said as null rather than as one of the two.
    /// </summary>
    private static string? WeakestChargesSource(IEnumerable<string?> sources)
    {
        var known = sources.Where(source => !string.IsNullOrEmpty(source)).ToList();

        if (known.Count == 0)
            return null;

        return known.Any(source => !OrderPaymentCharges.IsAuthoritative(source))
            ? OrderPaymentCharges.FromPayment
            : OrderPaymentCharges.FromRecon;
    }

    /// <summary>
    /// Whose books an answer is for: the partners the acting admin is linked to (one or more), or - when there are
    /// none - nothing at all, which is what makes the answer the whole shop's.
    /// </summary>
    private sealed record FinanceScope(IReadOnlySet<int> PartnerIds, string PartnerName);

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
