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
                PurchaserId = p.PurchaserId,
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

    public async Task<List<AdminPurchaserDto>> GetPurchasersAsync()
    {
        return await (
            from pu in _context.PartnersUsers
            join u in _context.Users on pu.UserId equals u.UserId
            join pa in _context.Partners on pu.PartnerId equals pa.PartnerId
            where pu.IsActive
            orderby u.FirstName
            select new AdminPurchaserDto
            {
                PurchaserId = pu.PartnerUserId,
                PurchaserName = u.FirstName + " " + (u.LastName ?? ""),
                PartnerName = pa.PartnerName
            })
            .ToListAsync();
    }

    public async Task<AdminResultDto> CreatePurchaseAsync(AdminPurchaseCreateRequest request, long currentUserId)
    {
        if (request.VendorId <= 0)
            return Error("Vendor is required.");
        if (request.PurchaserId <= 0)
            return Error("Purchaser is required.");
        if (request.Items is not { Count: > 0 })
            return Error("At least one item is required.");

        foreach (var item in request.Items)
        {
            if (item.ProductId <= 0)
                return Error("Each item must have a product.");
            if (item.Quantity <= 0)
                return Error("Item quantity must be greater than zero.");
            if (item.UnitPrice < 0)
                return Error("Item unit price cannot be negative.");
            if (item.Gst < 0)
                return Error("Item GST cannot be negative.");
        }

        if (!await _context.Vendors.AnyAsync(v => v.VendorId == request.VendorId))
            return Error("Vendor not found.");
        if (!await _context.PartnersUsers.AnyAsync(pu => pu.PartnerUserId == request.PurchaserId))
            return Error("Purchaser not found.");

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var existingProductCount = await _context.Products.CountAsync(p => productIds.Contains(p.ProductId));
        if (existingProductCount != productIds.Count)
            return Error("One or more selected products no longer exist.");

        var statusId = request.PurchaseStatusId > 0 ? request.PurchaseStatusId : (short)1;
        if (!await _context.PurchaseStatuses.AnyAsync(s => s.PurchaseStatusId == statusId && s.IsActive))
            return Error("Invalid purchase status.");

        var now = DateTime.UtcNow;
        var purchase = new Purchase
        {
            VendorId = request.VendorId,
            PurchaserId = request.PurchaserId,
            PurchaseDate = request.PurchaseDate == default ? now : request.PurchaseDate,
            InvoicePath = CleanOptional(request.InvoicePath),
            PurchaseStatusId = statusId,
            AddedBy = currentUserId,
            AddedOn = now,
            LastModifiedBy = currentUserId,
            LastModifiedOn = now,
            PurchaseDetails = request.Items.Select(i => new PurchaseDetail
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Gst = i.Gst
            }).ToList()
        };

        _context.Purchases.Add(purchase);
        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { $"Purchase PUR-{purchase.PurchaseId:D6} created successfully." }
        };
    }

    // ------------------------------------------------------------
    // Purchase status workflow & edit
    // Mirrors the legacy admin app (HC.Business\Admin\Purchases.cs):
    //  - allowed next statuses come from PurchaseStatusCompatibilities
    //    filtered by the logged-in user's roles
    //    (PurchaseStatusUserRoleCompatibilities)
    //  - every change writes a PurchaseComment audit entry
    //  - RESUBMITTED is transient and immediately becomes SUBMITTED
    //  - APPROVED generates the partner inventory SKUs
    //  - header/items edits are only allowed in ADDED or RETURNED
    // ------------------------------------------------------------
    private enum PurchaseStatusIds : short
    {
        Added = 1,
        Submitted = 2,
        Verified = 3,
        Approved = 4,
        Returned = 5,
        Resubmitted = 6,
        Rejected = 7
    }

    private static string PurchaseNumber(long purchaseId)
    {
        return "PUR-" + purchaseId.ToString("D6");
    }

    private async Task<List<short>> GetUserRoleIdsAsync(long userId)
    {
        return await _context.UserRoles
            .Where(ur => ur.UserId == userId && ur.IsActive)
            .Select(ur => ur.RoleId)
            .ToListAsync();
    }

    public async Task<List<AdminPurchaseStatusDto>> GetPurchaseStatusesAsync(long purchaseId, long currentUserId)
    {
        var currentStatusId = await _context.Purchases
            .Where(p => p.PurchaseId == purchaseId)
            .Select(p => (short?)p.PurchaseStatusId)
            .FirstOrDefaultAsync();

        if (currentStatusId == null)
            return new List<AdminPurchaseStatusDto>();

        var roleIds = await GetUserRoleIdsAsync(currentUserId);
        if (roleIds.Count == 0)
            return new List<AdminPurchaseStatusDto>();

        var compatibleStatusIds = await _context.PurchaseStatusCompatibilities
            .Where(sc => sc.PurchaseStatusId == currentStatusId.Value && sc.IsActive)
            .Select(sc => sc.PurchaseNewStatusId)
            .ToListAsync();

        if (compatibleStatusIds.Count == 0)
            return new List<AdminPurchaseStatusDto>();

        var allowedStatusIds = await _context.PurchaseStatusUserRoleCompatibilities
            .Where(src => compatibleStatusIds.Contains(src.PurchaseStatusId)
                          && src.IsActive
                          && roleIds.Contains(src.UserRoleId))
            .Select(src => src.PurchaseStatusId)
            .Distinct()
            .ToListAsync();

        if (allowedStatusIds.Count == 0)
            return new List<AdminPurchaseStatusDto>();

        return await _context.PurchaseStatuses
            .Where(s => allowedStatusIds.Contains(s.PurchaseStatusId) && s.IsActive)
            .OrderBy(s => s.PurchaseStatusId)
            .Select(s => new AdminPurchaseStatusDto
            {
                PurchaseStatusId = s.PurchaseStatusId,
                PurchaseStatusName = s.PurchaseStatusName
            })
            .ToListAsync();
    }


    public async Task<AdminResultDto> UpdatePurchaseStatusAsync(long purchaseId, AdminPurchaseStatusUpdateRequest request, long currentUserId)
    {
        var purchase = await _context.Purchases
            .Include(p => p.PurchaseStatus)
            .FirstOrDefaultAsync(p => p.PurchaseId == purchaseId);

        if (purchase == null)
            return Error("Purchase not found.");
        if (request.StatusId <= 0)
            return Error("New status is required.");
        if (request.StatusId == purchase.PurchaseStatusId)
            return Error("Purchase is already in the selected status.");

        var isCompatible = await _context.PurchaseStatusCompatibilities
            .AnyAsync(sc => sc.PurchaseStatusId == purchase.PurchaseStatusId
                            && sc.PurchaseNewStatusId == request.StatusId
                            && sc.IsActive);
        if (!isCompatible)
            return Error("Selected status is not compatible with the current status.");

        var newStatus = await _context.PurchaseStatuses
            .FirstOrDefaultAsync(s => s.PurchaseStatusId == request.StatusId && s.IsActive);
        if (newStatus == null)
            return Error("Invalid purchase status.");

        var roleIds = await GetUserRoleIdsAsync(currentUserId);
        var isRoleAllowed = await _context.PurchaseStatusUserRoleCompatibilities
            .AnyAsync(src => src.PurchaseStatusId == request.StatusId
                             && src.IsActive
                             && roleIds.Contains(src.UserRoleId));
        if (!isRoleAllowed)
            return Error("You are not authorized to set the selected status.");

        var currentStatusName = purchase.PurchaseStatus.PurchaseStatusName;
        var now = DateTime.UtcNow;

        purchase.PurchaseStatusId = request.StatusId;
        purchase.LastModifiedBy = currentUserId;
        purchase.LastModifiedOn = now;

        _context.PurchaseComments.Add(new PurchaseComment
        {
            PurchaseId = purchaseId,
            AddedBy = currentUserId,
            AddedOn = now,
            Comments = $"Status Changed from {currentStatusName} to {newStatus.PurchaseStatusName}"
        });

        var extraComment = CleanOptional(request.Comments);
        if (extraComment != null)
        {
            _context.PurchaseComments.Add(new PurchaseComment
            {
                PurchaseId = purchaseId,
                AddedBy = currentUserId,
                AddedOn = now,
                Comments = extraComment
            });
        }

        await _context.SaveChangesAsync();

        // RESUBMITTED is transient: it immediately moves on to SUBMITTED.
        if (request.StatusId == (short)PurchaseStatusIds.Resubmitted)
        {
            var submittedStatusId = (short)PurchaseStatusIds.Submitted;
            var submittedName = await _context.PurchaseStatuses
                .Where(s => s.PurchaseStatusId == submittedStatusId)
                .Select(s => s.PurchaseStatusName)
                .FirstAsync();

            var chainedOn = DateTime.UtcNow;
            purchase.PurchaseStatusId = submittedStatusId;
            purchase.LastModifiedBy = currentUserId;
            purchase.LastModifiedOn = chainedOn;

            _context.PurchaseComments.Add(new PurchaseComment
            {
                PurchaseId = purchaseId,
                AddedBy = currentUserId,
                AddedOn = chainedOn,
                Comments = $"Status Changed from {newStatus.PurchaseStatusName} to {submittedName}"
            });

            await _context.SaveChangesAsync();
        }

        // Approving a purchase generates the partner inventory SKUs.
        if (purchase.PurchaseStatusId == (short)PurchaseStatusIds.Approved)
        {
            await GeneratePurchaseSkusAsync(purchaseId);
        }

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { $"Purchase {PurchaseNumber(purchaseId)} status changed to {newStatus.PurchaseStatusName}." }
        };
    }


    public async Task<AdminResultDto> UpdatePurchaseAsync(long purchaseId, AdminPurchaseUpdateRequest request, long currentUserId)
    {
        var purchase = await _context.Purchases.FirstOrDefaultAsync(p => p.PurchaseId == purchaseId);
        if (purchase == null)
            return Error("Purchase not found.");

        if (purchase.PurchaseStatusId != (short)PurchaseStatusIds.Added
            && purchase.PurchaseStatusId != (short)PurchaseStatusIds.Returned)
            return Error("Purchase details can be edited only while the purchase is in ADDED or RETURNED status.");

        if (request.VendorId <= 0)
            return Error("Vendor is required.");
        if (request.PurchaserId <= 0)
            return Error("Purchaser is required.");
        if (!await _context.Vendors.AnyAsync(v => v.VendorId == request.VendorId))
            return Error("Vendor not found.");
        if (!await _context.PartnersUsers.AnyAsync(pu => pu.PartnerUserId == request.PurchaserId))
            return Error("Purchaser not found.");

        purchase.VendorId = request.VendorId;
        purchase.PurchaserId = request.PurchaserId;
        if (request.PurchaseDate != default)
            purchase.PurchaseDate = request.PurchaseDate;
        purchase.InvoicePath = CleanOptional(request.InvoicePath);
        purchase.LastModifiedBy = currentUserId;
        purchase.LastModifiedOn = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { $"Purchase {PurchaseNumber(purchaseId)} updated successfully." }
        };
    }


    public async Task<AdminResultDto> SavePurchaseItemsAsync(long purchaseId, AdminPurchaseItemsRequest request, long currentUserId)
    {
        var purchase = await _context.Purchases
            .Include(p => p.PurchaseDetails)
            .FirstOrDefaultAsync(p => p.PurchaseId == purchaseId);
        if (purchase == null)
            return Error("Purchase not found.");

        if (purchase.PurchaseStatusId != (short)PurchaseStatusIds.Added
            && purchase.PurchaseStatusId != (short)PurchaseStatusIds.Returned)
            return Error("Purchase items can be edited only while the purchase is in ADDED or RETURNED status.");

        if (request.Items is not { Count: > 0 })
            return Error("At least one item is required.");

        foreach (var item in request.Items)
        {
            if (item.ProductId <= 0)
                return Error("Each item must have a product.");
            if (item.Quantity <= 0)
                return Error("Item quantity must be greater than zero.");
            if (item.UnitPrice < 0)
                return Error("Item unit price cannot be negative.");
            if (item.Gst < 0)
                return Error("Item GST cannot be negative.");
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var existingProductCount = await _context.Products.CountAsync(p => productIds.Contains(p.ProductId));
        if (existingProductCount != productIds.Count)
            return Error("One or more selected products no longer exist.");

        var existingDetails = purchase.PurchaseDetails.ToList();
        var keptDetailIds = new List<long>();

        foreach (var item in request.Items)
        {
            if (item.PurchaseDetailId > 0)
            {
                var detail = existingDetails.FirstOrDefault(d => d.PurchaseDetailId == item.PurchaseDetailId);
                if (detail == null)
                    return Error("One or more purchase items no longer exist.");

                detail.ProductId = item.ProductId;
                detail.Quantity = item.Quantity;
                detail.UnitPrice = item.UnitPrice;
                detail.Gst = item.Gst;
                keptDetailIds.Add(detail.PurchaseDetailId);
            }
            else
            {
                purchase.PurchaseDetails.Add(new PurchaseDetail
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Gst = item.Gst
                });
            }
        }

        foreach (var detail in existingDetails.Where(d => !keptDetailIds.Contains(d.PurchaseDetailId)))
        {
            _context.PurchaseDetails.Remove(detail);
        }

        purchase.LastModifiedBy = currentUserId;
        purchase.LastModifiedOn = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { $"Purchase {PurchaseNumber(purchaseId)} items saved successfully." }
        };
    }


    public async Task<AdminResultDto> AddPurchaseCommentAsync(long purchaseId, AdminPurchaseCommentRequest request, long currentUserId)
    {
        var comments = (request.Comments ?? "").Trim();
        if (comments.Length == 0)
            return Error("Comment is required.");
        if (comments.Length > 2000)
            return Error("Comment cannot exceed 2000 characters.");

        var purchaseExists = await _context.Purchases.AnyAsync(p => p.PurchaseId == purchaseId);
        if (!purchaseExists)
            return Error("Purchase not found.");

        _context.PurchaseComments.Add(new PurchaseComment
        {
            PurchaseId = purchaseId,
            AddedBy = currentUserId,
            AddedOn = DateTime.UtcNow,
            Comments = comments
        });

        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { "Comment added successfully." }
        };
    }

    private async Task GeneratePurchaseSkusAsync(long purchaseId)
    {
        var purchase = await _context.Purchases
            .Include(p => p.Purchaser)
                .ThenInclude(pu => pu.Partner)
                    .ThenInclude(pa => pa.Inventories)
            .Include(p => p.PurchaseDetails)
                .ThenInclude(d => d.Skus)
            .FirstOrDefaultAsync(p => p.PurchaseId == purchaseId);

        if (purchase == null)
            return;

        var inventory = purchase.Purchaser?.Partner?.Inventories
            .FirstOrDefault(i => i.IsDefault);
        if (inventory == null)
            return;

        foreach (var detail in purchase.PurchaseDetails)
        {
            var generatedCount = detail.Skus.Count;
            var index = generatedCount;

            while (generatedCount < detail.Quantity)
            {
                index++;
                _context.Skus.Add(new Sku
                {
                    InventoryId = inventory.InventoryId,
                    PurchaseDetailId = detail.PurchaseDetailId,
                    SkustatusId = 1,
                    Sku1 = purchase.PurchaseId.ToString().PadLeft(5, '0')
                          + detail.PurchaseDetailId.ToString().PadLeft(5, '0')
                          + index.ToString().PadLeft(3, '0')
                });
                generatedCount++;
            }
        }

        await _context.SaveChangesAsync();
    }


}
