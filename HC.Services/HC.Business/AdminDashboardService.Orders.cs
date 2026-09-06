// ============================================================
// AdminDashboardService.Orders.cs
// Partial class: AdminDashboardService - Orders operations
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
    public async Task<List<AdminOrderListDto>> GetOrdersAsync()
    {
        return await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderStatus)
            .Include(o => o.OrderItems)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new AdminOrderListDto
            {
                OrderId = o.OrderId,
                OrderNumber = "ORD-" + o.OrderId.ToString("D6"),
                OrderDate = o.OrderDate,
                CustomerName = o.Customer.FirstName + " " + (o.Customer.LastName ?? ""),
                Status = o.OrderStatus.Status,
                TotalAmount = o.OrderItems.Sum(oi => oi.UnitPrice),
                ItemCount = o.OrderItems.Count
            })
            .ToListAsync();
    }

    public async Task<AdminOrderDetailDto?> GetOrderDetailAsync(long orderId)
    {
        return await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderStatus)
            .Include(o => o.Seller)
            .Include(o => o.BillingAddress)
            .Include(o => o.ShippingAddress)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.SkuNavigation)
            .Include(o => o.OrderHistories)
                .ThenInclude(oh => oh.Order)
            .Where(o => o.OrderId == orderId)
            .Select(o => new AdminOrderDetailDto
            {
                OrderId = o.OrderId,
                OrderNumber = "ORD-" + o.OrderId.ToString("D6"),
                OrderDate = o.OrderDate,
                CustomerName = o.Customer.FirstName + " " + (o.Customer.LastName ?? ""),
                CustomerEmail = o.Customer.EmailId,
                Status = o.OrderStatus.Status,
                SellerName = o.Seller.PartnerName,
                BillingAddress = new AdminAddressDto
                {
                    AddressTitle = o.BillingAddress.AddressTitle,
                    ContactName = o.BillingAddress.ContactName,
                    AddressLine1 = o.BillingAddress.AddressLine1,
                    AddressLine2 = o.BillingAddress.AddressLine2,
                    City = o.BillingAddress.City,
                    State = o.BillingAddress.State,
                    Zipcode = o.BillingAddress.Zipcode,
                    MobileNumber = o.BillingAddress.MobileNumber
                },
                ShippingAddress = new AdminAddressDto
                {
                    AddressTitle = o.ShippingAddress.AddressTitle,
                    ContactName = o.ShippingAddress.ContactName,
                    AddressLine1 = o.ShippingAddress.AddressLine1,
                    AddressLine2 = o.ShippingAddress.AddressLine2,
                    City = o.ShippingAddress.City,
                    State = o.ShippingAddress.State,
                    Zipcode = o.ShippingAddress.Zipcode,
                    MobileNumber = o.ShippingAddress.MobileNumber
                },
                Items = o.OrderItems.Select(oi => new AdminOrderItemDto
                {
                    Sku = oi.Sku,
                    ProductName = oi.ProductName,
                    ProductTitle = oi.ProductTitle,
                    UnitPrice = oi.UnitPrice,
                    DiscountPercent = oi.DiscountPercent,
                    AdditionalDiscountPercent = oi.AdditionalDiscountPercent,
                    DeliveryCharge = oi.DeliveryCharge,
                    PackagingCharge = oi.PackagingCharge,
                    StorageCharge = oi.StorageCharge,
                    ProfitMarginPercent = oi.ProfitMarginPercent,
                    Cgstpercent = oi.Cgstpercent,
                    Sgstpercent = oi.Sgstpercent,
                    Igstpercent = oi.Igstpercent
                }).ToList(),
                History = o.OrderHistories.Select(oh => new AdminOrderHistoryDto
                {
                    HistoryDate = oh.HistoryDate,
                    Status = oh.Order.OrderStatus.Status,
                    Comments = oh.Comments
                }).ToList()
            })
            .FirstOrDefaultAsync();
    }

}
