// ============================================================
// AdminDashboardService.Purchases.cs
// Partial class: AdminDashboardService - Purchases operations
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
    public async Task<List<AdminPurchaseListDto>> GetPurchasesAsync()
    {
        return await (
            from p in _context.Purchases
            join v in _context.Vendors on p.VendorId equals v.VendorId
            join pusr in _context.PartnersUsers on p.PurchaserId equals pusr.PartnerUserId
            join u in _context.Users on pusr.UserId equals u.UserId
            join s in _context.PurchaseStatuses on p.PurchaseStatusId equals s.PurchaseStatusId
            orderby p.PurchaseId descending
            select new AdminPurchaseListDto
            {
                PurchaseId = p.PurchaseId,
                PurchaseNumber = "PUR-" + p.PurchaseId.ToString("D6"),
                VendorId = p.VendorId,
                VendorName = v.VendorName,
                PurchaserName = u.FirstName + " " + (u.LastName ?? ""),
                PurchaseDate = p.PurchaseDate,
                PurchaseStatusId = p.PurchaseStatusId,
                Status = s.PurchaseStatusName,
                ItemCount = p.PurchaseDetails.Count,
                TotalAmount = p.PurchaseDetails.Sum(pd => pd.Quantity * pd.UnitPrice)
            })
            .ToListAsync();
    }

    public async Task<AdminPurchaseDetailDto?> GetPurchaseDetailAsync(long purchaseId)
    {
        return await (
            from p in _context.Purchases
            join v in _context.Vendors on p.VendorId equals v.VendorId
            join pusr in _context.PartnersUsers on p.PurchaserId equals pusr.PartnerUserId
            join u in _context.Users on pusr.UserId equals u.UserId
            join addedBy in _context.Users on p.AddedBy equals addedBy.UserId
            join modifiedBy in _context.Users on p.LastModifiedBy equals modifiedBy.UserId
            join s in _context.PurchaseStatuses on p.PurchaseStatusId equals s.PurchaseStatusId
            where p.PurchaseId == purchaseId
            select new AdminPurchaseDetailDto
            {
                PurchaseId = p.PurchaseId,
                PurchaseNumber = "PUR-" + p.PurchaseId.ToString("D6"),
                VendorId = p.VendorId,
                VendorName = v.VendorName,
                PurchaserName = u.FirstName + " " + (u.LastName ?? ""),
                PurchaseDate = p.PurchaseDate,
                PurchaseStatusId = p.PurchaseStatusId,
                Status = s.PurchaseStatusName,
                InvoicePath = p.InvoicePath,
                AddedByName = addedBy.FirstName + " " + (addedBy.LastName ?? ""),
                AddedOn = p.AddedOn,
                LastModifiedByName = modifiedBy.FirstName + " " + (modifiedBy.LastName ?? ""),
                LastModifiedOn = p.LastModifiedOn,
                Items = p.PurchaseDetails
                    .Select(pd => new AdminPurchaseItemDto
                    {
                        PurchaseDetailId = pd.PurchaseDetailId,
                        ProductId = pd.ProductId,
                        ProductName = pd.Product.ProductName,
                        Quantity = pd.Quantity,
                        UnitPrice = pd.UnitPrice,
                        Gst = pd.Gst,
                        LineTotal = pd.Quantity * pd.UnitPrice
                    })
                    .ToList(),
                Comments = p.PurchaseComments
                    .Select(pc => new AdminPurchaseCommentDto
                    {
                        PurchaseCommentId = pc.PurchaseCommentId,
                        Comments = pc.Comments,
                        AddedByName = pc.AddedByNavigation.FirstName + " " + (pc.AddedByNavigation.LastName ?? ""),
                        AddedOn = pc.AddedOn
                    })
                    .OrderByDescending(c => c.AddedOn)
                    .ToList()
            })
            .FirstOrDefaultAsync();
    }

}
