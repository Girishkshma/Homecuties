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
    public async Task<DashboardStatsDto> GetDashboardStatsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var stats = new DashboardStatsDto
        {
            TotalProducts = await _context.Products.CountAsync(),
            TotalOrders = await _context.Orders.CountAsync(),
            TotalCustomers = await _context.Customers.CountAsync(),
            TotalPartners = await _context.Partners.CountAsync(),
            TotalVendors = await _context.Vendors.CountAsync(),
            PendingOrders = await _context.Orders.CountAsync(o => o.OrderStatusId == 1), // Assuming 1 = Pending
            TodayRevenue = await _context.Orders
                .Where(o => o.OrderDate >= today)
                .SumAsync(o => (decimal?)o.OrderItems.Sum(oi => oi.UnitPrice)) ?? 0,
            MonthlyRevenue = await _context.Orders
                .Where(o => o.OrderDate >= monthStart)
                .SumAsync(o => (decimal?)o.OrderItems.Sum(oi => oi.UnitPrice)) ?? 0
        };

        return stats;
    }

}
