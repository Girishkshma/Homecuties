using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

/// <summary>
/// The one writer of <c>OrderItemMoney</c>: what each order line really carried, money-wise.
///
/// It exists because the shop's money is recorded against the ORDER and read per LINE. One payment row holds what
/// the gateway took and what it kept for taking it, one parcel row holds what the courier billed, and the order's
/// lines hold the tax the checkout charged - and none of those can answer "what did this SKU make?", nor answer a
/// partner whose goods only some of an order's lines are. This class does the division once, by what each line was
/// worth (<see cref="OrderMoney.LineValue"/>, the same weight every partner's share has always been cut by) with
/// the odd paisa given to the largest remainders (<see cref="OrderMoney.Apportion"/>), and writes it down.
///
/// It is called at every point where one of those sources changes - a capture recording what the gateway kept, the
/// settlement pull correcting it with the figure the bank was paid on, a parcel's freight charge being typed in,
/// and the checkout recording its output GST - so all four land here without any of them knowing about the others.
/// It RECOMPUTES an order from those sources and never increments anything, which is what makes it safe to run
/// twice, to run over an order nothing has changed, and to run over many orders at once (the settlement pull
/// refreshes every order the day's lines touched, in one pass).
///
/// It is also why a partner's screen no longer has to guess at read time: the guess is now a record, and a
/// recorded split is something the shop can be shown and argue with. The read-time rule
/// (<see cref="OrderMoney.Share"/>) is deliberately kept beside it: the Finance screen's day-accurate summary
/// figures are still worked out that way, and so is an order whose rows have not been written yet.
///
/// Nothing here throws for missing data: an order with no lines has no rows to write, an order no gateway has
/// billed has no charge to split, and a parcel nobody has billed for yet has no freight to share - each of which
/// is a NULL in the table rather than a zero, because 'not known' and 'nothing' are different answers.
/// </summary>
public static class OrderItemMoneyWriter
{
    /// <summary>
    /// What one pass did, so a caller can say it in a log line - and so the settlement pull's answer can report
    /// the parcels it had to spread over a whole order because nobody had said what was in them.
    /// </summary>
    /// <param name="Orders">Orders whose rows were (re)written.</param>
    /// <param name="Rows">Order lines written, in total.</param>
    /// <param name="ParcelsWithoutItems">
    /// Parcels whose bill was charged to the whole order because no contents were recorded for them - a parcel
    /// written before the Shipment card asked, or one whose recorded goods are no longer on the order.
    /// </param>
    /// <param name="OrdersWithNoCharge">
    /// Orders refreshed that have no payment row carrying a charge, so there was nothing to split - a free order,
    /// or one whose fee the gateway has not worked out yet.
    /// </param>
    public readonly record struct Result(
        int Orders,
        int Rows,
        int ParcelsWithoutItems,
        int OrdersWithNoCharge);

    /// <summary>
    /// Recomputes one order's per-unit money and saves. This is what the order-side writes use - a capture, a
    /// parcel's freight charge, a checkout - and the many-order call below is what a pull uses.
    /// </summary>
    public static Task<Result> RefreshAsync(
        HomecutiesDbContext context,
        long orderId,
        CancellationToken cancellationToken = default) =>
        RefreshAsync(context, new[] { orderId }, cancellationToken);

    /// <summary>
    /// Recomputes the per-unit money of every order named, in one pass, and saves. An order with no lines, or one
    /// the shop has no record of, is simply left alone - the caller names what it has, not what must exist.
    /// </summary>
    public static async Task<Result> RefreshAsync(
        HomecutiesDbContext context,
        IReadOnlyCollection<long> orderIds,
        CancellationToken cancellationToken = default)
    {
        var ids = orderIds.Where(id => id > 0).Distinct().ToList();

        if (ids.Count == 0)
            return new Result(0, 0, 0, 0);

        // The order's own lines, one row per physical unit (OrderItems carries no quantity - the checkout writes a
        // row per unit), which is exactly the grain the money is split at and then added back up per SKU.
        var lines = (await context.OrderItems
                .AsNoTracking()
                .Where(item => ids.Contains(item.OrderId))
                .Select(item => new
                {
                    item.OrderId,
                    item.Sku,
                    item.UnitPrice,
                    item.DiscountPercent,
                    item.AdditionalDiscountPercent,
                    item.Cgstpercent,
                    item.Sgstpercent,
                    item.Igstpercent
                })
                .ToListAsync(cancellationToken))
            .Select(item => new Line(
                item.OrderId,
                item.Sku,
                item.UnitPrice,
                item.DiscountPercent,
                item.AdditionalDiscountPercent,
                item.Cgstpercent,
                item.Sgstpercent,
                item.Igstpercent))
            .ToList();

        // What the gateway took and kept, from the payment rows themselves - the same rows, and the same statuses,
        // the Finance screen and the dashboard read (OrderMoney), so a per-line fee can never describe a payment
        // the rest of the shop does not think happened.
        var payments = (await context.OrderPayments
                .AsNoTracking()
                .Where(payment => ids.Contains(payment.OrderId) && OrderMoney.TakenStatuses.Contains(payment.Status))
                .Select(payment => new
                {
                    payment.OrderId,
                    payment.FeeAmount,
                    payment.TaxAmount,
                    payment.ChargesSource
                })
                .ToListAsync(cancellationToken))
            .Select(payment => new Payment(
                payment.OrderId,
                payment.FeeAmount,
                payment.TaxAmount,
                payment.ChargesSource))
            .ToList();

        // The parcels that went out, and what went in them. A reverse leg is deliberately not read: a return's
        // pickup bill is the return's own cost, and the Finance screen has never counted it against the sale
        // (see AdminDashboardService.Finance) - counting it here would bill the same order twice.
        var parcels = (await context.OrderShipments
                .AsNoTracking()
                .Where(shipment => ids.Contains(shipment.OrderId) &&
                                   shipment.Direction == OrderShipment.DirectionForward)
                .Select(shipment => new
                {
                    shipment.ShipmentId,
                    shipment.OrderId,
                    shipment.FreightCharge
                })
                .ToListAsync(cancellationToken))
            .Select(parcel => new Parcel(parcel.ShipmentId, parcel.OrderId, parcel.FreightCharge))
            .ToList();

        var parcelIds = parcels.Select(parcel => parcel.ShipmentId).ToList();

        var parcelItems = parcelIds.Count == 0
            ? new List<ParcelItem>()
            : (await context.OrderShipmentItems
                    .AsNoTracking()
                    .Where(item => parcelIds.Contains(item.ShipmentId))
                    .Select(item => new { item.ShipmentId, item.Sku, item.Quantity })
                    .ToListAsync(cancellationToken))
                .Select(item => new ParcelItem(item.ShipmentId, item.Sku, item.Quantity))
                .ToList();

        var stored = await context.OrderItemMoney
            .Where(money => ids.Contains(money.OrderId))
            .ToListAsync(cancellationToken);

        return await WriteAsync(context, lines, payments, parcels, parcelItems, stored, cancellationToken);
    }

    /// <summary>
    /// The arithmetic, over rows already read: one pass per order, recomputing every line's part of the order's
    /// money and writing the rows down. Kept separate from the reads above only so the shape of the two is
    /// readable - this is where the rules are (a split by what each line was worth, a NULL for a figure nothing
    /// has reported, a parcel's bill charged to the units that were in it).
    /// </summary>
    private static async Task<Result> WriteAsync(
        HomecutiesDbContext context,
        List<Line> lines,
        List<Payment> payments,
        List<Parcel> parcels,
        List<ParcelItem> parcelItems,
        List<OrderItemMoney> stored,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var parcelsByOrder = parcels
            .GroupBy(parcel => parcel.OrderId)
            .ToDictionary(group => group.Key, group => group.OrderBy(parcel => parcel.ShipmentId).ToList());

        var itemsByParcel = parcelItems
            .GroupBy(item => item.ShipmentId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var storedByKey = stored.ToDictionary(money => (money.OrderId, money.Sku));

        var written = new HashSet<(long OrderId, string Sku)>();
        var orders = 0;
        var rows = 0;
        var parcelsWithoutItems = 0;
        var ordersWithNoCharge = 0;

        foreach (var order in lines.GroupBy(line => line.OrderId))
        {
            var orderId = order.Key;

            // One entry per physical unit, in a fixed order (by SKU) so that the odd paisa of a split always lands
            // on the same line for the same order - which is what makes two runs of this writer produce the same
            // rows, and what lets the backfill script's set-based version agree with it.
            var units = order
                .Select(line => new Unit(
                    line.Sku,
                    OrderMoney.LineValue(
                        line.UnitPrice, line.DiscountPercent, line.AdditionalDiscountPercent,
                        line.CgstPercent, line.SgstPercent, line.IgstPercent),
                    OrderMoney.GstOn(
                        OrderMoney.TaxableValue(line.UnitPrice, line.DiscountPercent, line.AdditionalDiscountPercent),
                        OrderMoney.ChargedGstRate(line.CgstPercent, line.SgstPercent, line.IgstPercent))))
                .OrderBy(unit => unit.Sku, StringComparer.Ordinal)
                .ToList();

            var weights = units.Select(unit => unit.Value).ToArray();

            var weightBySku = units
                .GroupBy(unit => unit.Sku, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal);

            // What the gateway kept for taking this order's money. A payment row that took money but has had no
            // charge reported yet contributes nothing and leaves the figure unknown (null) rather than free (0) -
            // a capture's fee is worked out shortly afterwards, and the settlement pull is what makes it final
            // (see OrderPaymentCharges).
            var charges = payments
                .Where(payment => payment.OrderId == orderId &&
                                  (payment.FeeAmount != null || payment.TaxAmount != null))
                .ToList();

            decimal? gatewayFee = null;
            decimal? gatewayTax = null;
            string? chargesSource = null;

            if (charges.Count == 0)
            {
                ordersWithNoCharge++;
            }
            else
            {
                gatewayFee = charges.Sum(payment => payment.FeeAmount ?? 0m);
                gatewayTax = charges.Sum(payment => payment.TaxAmount ?? 0m);

                // Whether the figure is the bank's own or still the capture's estimate - the same vocabulary the
                // payment row carries, so a per-line fee is read with the same trust as the payment it came from.
                chargesSource = charges.Any(payment => OrderPaymentCharges.IsAuthoritative(payment.ChargesSource))
                    ? OrderPaymentCharges.FromRecon
                    : OrderPaymentCharges.FromPayment;
            }

            var feePerUnit = gatewayFee == null ? null : OrderMoney.Apportion(gatewayFee.Value, weights);
            var taxPerUnit = gatewayTax == null ? null : OrderMoney.Apportion(gatewayTax.Value, weights);

            // What the couriers billed for the parcels that carried these units. Each parcel's bill is split
            // across the units that were in IT - which is why the contents are recorded at all - and a parcel
            // nobody recorded contents for is spread over the whole order rather than lost (and counted, so the
            // shop can tell the two apart and go and say what was in it).
            var freightBySku = new Dictionary<string, decimal>(StringComparer.Ordinal);

            foreach (var parcel in parcelsByOrder.GetValueOrDefault(orderId, new List<Parcel>()))
            {
                if (parcel.FreightCharge is not { } bill)
                    continue;

                var contents = itemsByParcel
                    .GetValueOrDefault(parcel.ShipmentId, new List<ParcelItem>())
                    .Where(item => weightBySku.ContainsKey(item.Sku))
                    .ToList();

                if (contents.Count == 0)
                {
                    // Nobody said what was in it (or what was said names goods this order no longer has): the
                    // order's own units are the only fair split there is, and dropping the bill would shrink the
                    // shop's own costs.
                    parcelsWithoutItems++;

                    contents = weightBySku.Keys
                        .OrderBy(sku => sku, StringComparer.Ordinal)
                        .Select(sku => new ParcelItem(
                            parcel.ShipmentId,
                            sku,
                            (short)units.Count(unit => unit.Sku == sku)))
                        .ToList();
                }

                // One weight per unit the parcel carries: three of a thing weigh three times one of it, and every
                // unit of a SKU weighs the same (same price, same discounts, same rate) because they are all rows
                // of the same order line's snapshot.
                var carriedWeights = new List<decimal>();

                foreach (var item in contents)
                {
                    for (var unit = 0; unit < item.Quantity; unit++)
                        carriedWeights.Add(weightBySku[item.Sku]);
                }

                var shares = OrderMoney.Apportion(bill, carriedWeights);

                var at = 0;

                foreach (var item in contents)
                {
                    for (var unit = 0; unit < item.Quantity; unit++)
                        freightBySku[item.Sku] =
                            freightBySku.GetValueOrDefault(item.Sku) + shares[at++];
                }
            }

            // One row per line: the units of a SKU add up, because the table is per line and the split was per unit.
            var bySku = new Dictionary<string, OrderItemMoney>(StringComparer.Ordinal);

            for (var unit = 0; unit < units.Count; unit++)
            {
                if (!bySku.TryGetValue(units[unit].Sku, out var money))
                {
                    money = new OrderItemMoney { OrderId = orderId, Sku = units[unit].Sku };
                    bySku[units[unit].Sku] = money;
                }

                money.OutputGst += units[unit].Gst;

                if (feePerUnit != null)
                    money.GatewayFee = (money.GatewayFee ?? 0m) + feePerUnit[unit];

                if (taxPerUnit != null)
                    money.GatewayTax = (money.GatewayTax ?? 0m) + taxPerUnit[unit];
            }

            foreach (var (sku, share) in freightBySku)
                bySku[sku].FreightShare = share;

            foreach (var (sku, money) in bySku)
            {
                written.Add((orderId, sku));

                if (storedByKey.TryGetValue((orderId, sku), out var row))
                {
                    row.OutputGst = money.OutputGst;
                    row.GatewayFee = money.GatewayFee;
                    row.GatewayTax = money.GatewayTax;
                    row.FreightShare = money.FreightShare;
                    row.ChargesSource = chargesSource;
                    row.UpdatedOn = now;
                }
                else
                {
                    money.ChargesSource = chargesSource;
                    money.CreatedOn = now;
                    context.OrderItemMoney.Add(money);
                }

                rows++;
            }

            orders++;
        }

        // A line the order no longer has takes its money row with it: the rows ARE the order's lines, so a stale one
        // would go on reporting money for goods that are not there.
        foreach (var row in stored.Where(row => !written.Contains((row.OrderId, row.Sku))))
            context.OrderItemMoney.Remove(row);

        await context.SaveChangesAsync(cancellationToken);

        return new Result(orders, rows, parcelsWithoutItems, ordersWithNoCharge);
    }

    /// <summary>One order line as it was snapshotted: what the split needs from it, and nothing else.</summary>
    private readonly record struct Line(
        long OrderId,
        string Sku,
        decimal UnitPrice,
        decimal DiscountPercent,
        decimal AdditionalDiscountPercent,
        decimal CgstPercent,
        decimal SgstPercent,
        decimal IgstPercent);

    /// <summary>One payment row that holds money the gateway took, with the charge it reported for taking it.</summary>
    private readonly record struct Payment(
        long OrderId,
        decimal? FeeAmount,
        decimal? TaxAmount,
        string? ChargesSource);

    /// <summary>One physical unit of an order, with the two figures the split needs from it.</summary>
    /// <param name="Sku">The order line it belongs to.</param>
    /// <param name="Value">What it was worth to the customer - the weight every order-level figure is split by.</param>
    /// <param name="Gst">The output GST inside that value: the tax this unit holds for the government.</param>
    private readonly record struct Unit(string Sku, decimal Value, decimal Gst);

    /// <summary>One SKU of one parcel, with how many of its units are in it.</summary>
    private readonly record struct ParcelItem(long ShipmentId, string Sku, short Quantity);

    /// <summary>A parcel that went out, and the bill for it (null while nobody has recorded one).</summary>
    private readonly record struct Parcel(long ShipmentId, long OrderId, decimal? FreightCharge);
}
