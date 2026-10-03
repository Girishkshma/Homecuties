// ============================================================
// AdminDashboardService.Dashboard.cs
// Partial class: AdminDashboardService - Dashboard operations
// ============================================================

using System.Security.Cryptography;
using System.Text;
using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HC.Business;

public partial class AdminDashboardService : IAdminDashboardService
{
    /// <summary>
    /// The tiles of the admin dashboard. Cancelled orders are left out of the order count (they are
    /// reported on their own tile) and out of the revenue figures, and the revenue is read from the
    /// payment rows - what the gateway actually took, less the refunds sent back - instead of from the
    /// order lines, so 'Today's revenue' is money in the bank and not a number of open orders (see
    /// HC.Business.OrderMoney, the one definition the Finance screen reports on as well).
    ///
    /// The order/shipment tiles come from the same two maps the order screens read - the lifecycle
    /// (<see cref="OrderStatusFlow"/>) for the order counts and the parcel mapping
    /// (<see cref="ShipmentStatusFlow"/>) for where the parcels have got to - so an order can never be
    /// counted as 'to dispatch' here while its own screen shows it shipped.
    /// </summary>
    public async Task<DashboardStatsDto> GetDashboardStatsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        // Where the parcels of live orders have got to. The courier's own wording is what is stored
        // (OrderShipments.ProviderStatus) and the shop's stage is derived from it in C# (ShipmentStatusFlow),
        // so the distinct wordings are counted in the database and mapped here: one small query however many
        // parcels the shop has, and the same mapping the screens read.
        //
        // Only the legs that went out: a return carries a second row for the parcel coming back, and counting
        // it here would show one order as two parcels. What the shop has coming back is the returns' own
        // business (their card, and the returns tiles).
        var parcelWordings = await _context.OrderShipments
            .AsNoTracking()
            .Where(s => s.Direction == OrderShipment.DirectionForward)

            // Parcels of orders that have given their units back are left out - a cancelled order, and now a
            // returned one too, are not work the shop still has to do.
            .Where(s => s.Order.OrderStatusId != OrderStatusCancelled &&
                        s.Order.OrderStatusId != SkuAvailability.ReturnedOrderStatusId)
            .GroupBy(s => s.ProviderStatus)
            .Select(g => new { ProviderStatus = g.Key, Count = g.Count() })
            .ToListAsync();

        var parcelsByStage = new Dictionary<ShipmentStage, int>();
        foreach (var row in parcelWordings)
        {
            var stage = ShipmentStatusFlow.FromProviderText(row.ProviderStatus);
            parcelsByStage[stage] = parcelsByStage.GetValueOrDefault(stage) + row.Count;
        }

        // How many parcels are in the given stages - the shop's own answer, whatever words the courier used.
        int ParcelsIn(params ShipmentStage[] stages) => stages.Sum(stage => parcelsByStage.GetValueOrDefault(stage));

        // What the gateway took from a day on, and what was sent back from it. Both are read from the payment
        // rows: a row's Amount is what the gateway really took (the order's line prices only say what the
        // checkout asked for), and its own refund columns say what the shop gave back. The two rules - which
        // states count as money taken, and which refunds count as gone - live in HC.Business.OrderMoney, so
        // these tiles and the Finance screen can never describe the same day differently.
        async Task<decimal> TakenSince(DateTime from) =>
            await _context.OrderPayments
                .AsNoTracking()
                .Where(p => OrderMoney.TakenStatuses.Contains(p.Status))
                .Where(p => (p.GatewayChargedOn ?? p.CreatedOn) >= from)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

        async Task<decimal> GivenBackSince(DateTime from) =>
            await _context.OrderPayments
                .AsNoTracking()
                .Where(p => p.RefundedOn != null && p.RefundAmount > 0)
                .Where(p => p.RefundedOn >= from)
                .SumAsync(p => p.RefundAmount) ?? 0;

        var stats = new DashboardStatsDto
        {
            TotalProducts = await _context.Products.CountAsync(),

            // A cancelled order is not work the shop still has to do, so it is counted on the
            // 'Cancelled Orders' tile instead of in 'Total Orders'.
            TotalOrders = await _context.Orders.CountAsync(o => o.OrderStatusId != OrderStatusCancelled),
            CancelledOrders = await _context.Orders.CountAsync(o => o.OrderStatusId == OrderStatusCancelled),
            TotalCustomers = await _context.Customers.CountAsync(),
            TotalPartners = await _context.Partners.CountAsync(),
            TotalVendors = await _context.Vendors.CountAsync(),
            PendingOrders = await _context.Orders.CountAsync(o => o.OrderStatusId == OrderStatusPending),

            // Paid but not yet handed to a courier: the shop's packing/booking backlog.
            OrdersToDispatch = await _context.Orders.CountAsync(o => o.OrderStatusId == OrderStatusConfirmed),

            // Parcels on the way (a booked-but-not-collected parcel is still with us, so it is not here).
            ShipmentsInProgress = ParcelsIn(ShipmentStage.InTransit, ShipmentStage.OutForDelivery),
            OutForDelivery = ParcelsIn(ShipmentStage.OutForDelivery),

            // The end of the order lifecycle.
            DeliveredOrders = await _context.Orders.CountAsync(o => o.OrderStatusId == OrderStatusDelivered),

            // Failed deliveries, refusals and returns: flagged for the shop team, never acted on by the courier
            // (a refusal and an RTO also raise a return ask for them - see ShipmentStatusFlow.ReturnReasonFor).
            ShipmentsNeedingAttention = ParcelsIn(
                ShipmentStage.Undelivered, ShipmentStage.Refused, ShipmentStage.Rto),

            // The money owed back - the same rule the order list flags as 'Refund due' and the order screen's
            // Refund card shows: a refund that has been asked for, one Razorpay refused, or a capture on an order
            // that gave its units back (cancelled, or returned) whose refund was never asked for.
            RefundsDue = await _context.Orders.CountAsync(o => o.OrderPayments.Any(p =>
                p.Status == OrderPaymentStatus.RefundRequested ||
                p.Status == OrderPaymentStatus.RefundFailed ||
                ((o.OrderStatusId == OrderStatusCancelled ||
                  o.OrderStatusId == SkuAvailability.ReturnedOrderStatusId) &&
                 p.Status == OrderPaymentStatus.Captured))),

            // The returns side of the same 'what needs doing now' work, in the vocabulary the Return card uses
            // (OrderReturnStatus): an ask waiting for an answer, a parcel on its way back, and one that is back
            // with the shop waiting to be looked over and closed. ReturnedOrders is the other end of it - the
            // orders whose units came back - and is counted on its own tile, exactly like a cancelled order.
            ReturnsAwaitingDecision = await _context.OrderReturns
                .CountAsync(r => r.Status == OrderReturnStatus.Requested),
            ReturnsComingBack = await _context.OrderReturns
                .CountAsync(r => r.Status == OrderReturnStatus.Arranged),
            ReturnsReceived = await _context.OrderReturns
                .CountAsync(r => r.Status == OrderReturnStatus.Received),
            ReturnedOrders = await _context.Orders
                .CountAsync(o => o.OrderStatusId == SkuAvailability.ReturnedOrderStatusId),

            // Money in the bank for the day and for the month so far: what the gateway took over that stretch,
            // less the refunds sent back over it (see TakenSince/GivenBackSince above). A refund of an order
            // paid before the stretch is therefore a minus on the day it went out, exactly as the bank statement
            // reads - which is why the tiles are named 'net of refunds'.
            TodayRevenue = OrderMoney.Net(await TakenSince(today), await GivenBackSince(today)),
            MonthlyRevenue = OrderMoney.Net(await TakenSince(monthStart), await GivenBackSince(monthStart))
        };

        return stats;
    }

}
