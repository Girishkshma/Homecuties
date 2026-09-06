// ============================================================
// AdminDashboardService.Vendors.cs
// Partial class: AdminDashboardService - Vendors operations
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
    public async Task<List<AdminVendorListDto>> GetVendorsAsync()
    {
        return await _context.Vendors
            .OrderBy(v => v.VendorName)
            .Select(v => new AdminVendorListDto
            {
                VendorId = v.VendorId,
                VendorName = v.VendorName,
                VendorAddress = v.VendorAddress,
                Mobile = v.Mobile,
                IsActive = v.IsActive
            })
            .ToListAsync();
    }

    public async Task<AdminVendorDetailDto?> GetVendorDetailAsync(short vendorId)
    {
        return await _context.Vendors
            .Include(v => v.VendorsUsers)
                .ThenInclude(vu => vu.User)
                    .ThenInclude(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
            .Include(v => v.Purchases)
            .Where(v => v.VendorId == vendorId)
            .Select(v => new AdminVendorDetailDto
            {
                VendorId = v.VendorId,
                VendorName = v.VendorName,
                VendorAddress = v.VendorAddress,
                Mobile = v.Mobile,
                Remarks = v.Remarks,
                IsActive = v.IsActive,
                Users = v.VendorsUsers.Where(vu => vu.IsActive).Select(vu => new AdminVendorUserDto
                {
                    UserId = vu.User.UserId,
                    UserName = vu.User.FirstName + " " + (vu.User.LastName ?? ""),
                    LoginId = vu.User.LoginId,
                    EmailId = vu.User.EmailId,
                    MobileNumber = vu.User.MobileNumber,
                    IsActive = vu.User.IsActive ?? false,
                    Roles = vu.User.UserRoles.Where(ur => ur.IsActive).Select(ur => ur.Role.RoleName).ToList()
                }).ToList(),
                PurchaseCount = v.Purchases.Count
            })
            .FirstOrDefaultAsync();
    }

    public async Task<AdminResultDto> CreateVendorAsync(VendorFormRequest request, long currentUserId)
    {
        var vendorName = (request.VendorName ?? "").Trim();
        var mobile = (request.Mobile ?? "").Trim();

        if (string.IsNullOrWhiteSpace(vendorName))
            return Error("Vendor name is required.");
        if (vendorName.Length > 50)
            return Error("Vendor name cannot exceed 50 characters.");
        if (string.IsNullOrWhiteSpace(mobile))
            return Error("Mobile number is required.");
        if (!mobile.All(char.IsDigit) || mobile.Length > 10)
            return Error("Mobile number must be up to 10 digits.");

        var vendor = new Vendor
        {
            VendorName = vendorName,
            VendorAddress = CleanOptional(request.VendorAddress),
            Mobile = mobile,
            Remarks = CleanOptional(request.Remarks),
            IsActive = request.IsActive
        };

        _context.Vendors.Add(vendor);
        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { $"Vendor '{vendorName}' created successfully." }
        };
    }

    public async Task<AdminResultDto> UpdateVendorAsync(short vendorId, VendorFormRequest request, long currentUserId)
    {
        var vendorName = (request.VendorName ?? "").Trim();
        var mobile = (request.Mobile ?? "").Trim();

        if (string.IsNullOrWhiteSpace(vendorName))
            return Error("Vendor name is required.");
        if (vendorName.Length > 50)
            return Error("Vendor name cannot exceed 50 characters.");
        if (string.IsNullOrWhiteSpace(mobile))
            return Error("Mobile number is required.");
        if (!mobile.All(char.IsDigit) || mobile.Length > 10)
            return Error("Mobile number must be up to 10 digits.");

        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.VendorId == vendorId);
        if (vendor == null)
            return Error("Vendor not found.");

        vendor.VendorName = vendorName;
        vendor.VendorAddress = CleanOptional(request.VendorAddress);
        vendor.Mobile = mobile;
        vendor.Remarks = CleanOptional(request.Remarks);
        vendor.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { $"Vendor '{vendorName}' updated successfully." }
        };
    }

}
