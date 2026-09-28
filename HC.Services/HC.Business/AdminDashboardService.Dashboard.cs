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
    /// The statuses of an order the shop has actually been paid for: a Pending order has not been
    /// paid yet, and a cancelled order's money (if any) is refunded by the shop team - neither is
    /// revenue. See HC.Data/Scripts/SeedOrderStatuses.sql.
    /// </summary>
    private static readonly short[] RevenueOrderStatuses =
    {
        OrderStatusConfirmed, OrderStatusShipped, OrderStatusDelivered
    };

    /// <summary>
    /// The tiles of the admin dashboard. Cancelled orders are left out of the order count (they are
    /// reported on their own tile) and out of the revenue figures, and the revenue only adds up
    /// orders whose payment was captured - 'Today's revenue' is money in the bank, not a number of
    /// open orders.
    /// </summary>
    public async Task<DashboardStatsDto> GetDashboardStatsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);

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
            TodayRevenue = await _context.Orders
                .Where(o => o.OrderDate >= today && RevenueOrderStatuses.Contains(o.OrderStatusId))
                .SumAsync(o => (decimal?)o.OrderItems.Sum(oi => oi.UnitPrice)) ?? 0,
            MonthlyRevenue = await _context.Orders
                .Where(o => o.OrderDate >= monthStart && RevenueOrderStatuses.Contains(o.OrderStatusId))
                .SumAsync(o => (decimal?)o.OrderItems.Sum(oi => oi.UnitPrice)) ?? 0
        };

        return stats;
    }

}
