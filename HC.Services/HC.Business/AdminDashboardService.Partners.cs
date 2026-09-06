// ============================================================
// AdminDashboardService.Partners.cs
// Partial class: AdminDashboardService - Partners operations
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
    public async Task<List<AdminPartnerListDto>> GetPartnersAsync()
    {
        return await _context.Partners
            .Include(p => p.PartnerStatus)
            .OrderByDescending(p => p.LastModifiedOn)
            .Select(p => new AdminPartnerListDto
            {
                PartnerId = p.PartnerId,
                PartnerName = p.PartnerName,
                PartnerStatusId = p.PartnerStatusId,
                Status = p.PartnerStatus.PartnerStatus1,
                LastModifiedOn = p.LastModifiedOn
            })
            .ToListAsync();
    }

    public async Task<AdminPartnerDetailDto?> GetPartnerDetailAsync(int partnerId)
    {
        return await _context.Partners
            .Include(p => p.PartnerStatus)
            .Include(p => p.PartnersUsers)
                .ThenInclude(pu => pu.User)
                    .ThenInclude(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
            .Include(p => p.Inventories)
            .Where(p => p.PartnerId == partnerId)
            .Select(p => new AdminPartnerDetailDto
            {
                PartnerId = p.PartnerId,
                PartnerName = p.PartnerName,
                PartnerStatusId = p.PartnerStatusId,
                Status = p.PartnerStatus.PartnerStatus1,
                LastModifiedOn = p.LastModifiedOn,
                Users = p.PartnersUsers.Where(pu => pu.IsActive).Select(pu => new AdminPartnerUserDto
                {
                    UserId = pu.User.UserId,
                    UserName = pu.User.FirstName + " " + (pu.User.LastName ?? ""),
                    LoginId = pu.User.LoginId,
                    EmailId = pu.User.EmailId,
                    MobileNumber = pu.User.MobileNumber,
                    IsActive = pu.User.IsActive ?? false,
                    Roles = pu.User.UserRoles.Where(ur => ur.IsActive).Select(ur => ur.Role.RoleName).ToList()
                }).ToList(),
                InventoryCount = p.Inventories.Count,
                OrderCount = _context.Orders.Count(o => o.SellerId == p.PartnerId)
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<PartnerStatusOptionDto>> GetPartnerStatusesAsync()
    {
        return await _context.PartnerStatuses
            .OrderBy(s => s.PartnerStatusId)
            .Select(s => new PartnerStatusOptionDto
            {
                PartnerStatusId = s.PartnerStatusId,
                PartnerStatus = s.PartnerStatus1
            })
            .ToListAsync();
    }

    public async Task<AdminResultDto> CreatePartnerAsync(PartnerFormRequest request, long currentUserId)
    {
        var partnerName = (request.PartnerName ?? "").Trim();
        var now = DateTime.UtcNow; // UTC

        if (string.IsNullOrWhiteSpace(partnerName))
            return Error("Partner name is required.");
        if (partnerName.Length > 100)
            return Error("Partner name cannot exceed 100 characters.");
        if (!await _context.PartnerStatuses.AnyAsync(s => s.PartnerStatusId == request.PartnerStatusId))
            return Error("Selected partner status is invalid.");

        var partner = new Partner
        {
            PartnerName = partnerName,
            PartnerStatusId = request.PartnerStatusId,
            LastModifiedBy = currentUserId,
            LastModifiedOn = now
        };

        _context.Partners.Add(partner);
        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { $"Partner '{partnerName}' created successfully." }
        };
    }

    public async Task<AdminResultDto> UpdatePartnerAsync(int partnerId, PartnerFormRequest request, long currentUserId)
    {
        var partnerName = (request.PartnerName ?? "").Trim();
        var now = DateTime.UtcNow; // UTC

        if (string.IsNullOrWhiteSpace(partnerName))
            return Error("Partner name is required.");
        if (partnerName.Length > 100)
            return Error("Partner name cannot exceed 100 characters.");
        if (!await _context.PartnerStatuses.AnyAsync(s => s.PartnerStatusId == request.PartnerStatusId))
            return Error("Selected partner status is invalid.");

        var partner = await _context.Partners.FirstOrDefaultAsync(p => p.PartnerId == partnerId);
        if (partner == null)
            return Error("Partner not found.");

        partner.PartnerName = partnerName;
        partner.PartnerStatusId = request.PartnerStatusId;
        partner.LastModifiedBy = currentUserId;
        partner.LastModifiedOn = now;

        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { $"Partner '{partnerName}' updated successfully." }
        };
    }

}
