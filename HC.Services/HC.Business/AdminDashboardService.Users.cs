// ============================================================
// AdminDashboardService.Users.cs
// Partial class: AdminDashboardService - Users operations
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
    public async Task<List<AdminUserListDto>> GetAdminUsersAsync()
    {
        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .OrderBy(u => u.FirstName)
            .Select(u => new AdminUserListDto
            {
                UserId = u.UserId,
                LoginId = u.LoginId,
                FirstName = u.FirstName,
                LastName = u.LastName,
                EmailId = u.EmailId,
                IsActive = u.IsActive ?? false,
                Roles = u.UserRoles.Where(ur => ur.IsActive).Select(ur => ur.Role.RoleName).ToList()
            })
            .ToListAsync();
    }

    public async Task<AdminUserDetailDto?> GetAdminUserAsync(long userId)
    {
        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u => u.UserId == userId)
            .Select(u => new AdminUserDetailDto
            {
                UserId = u.UserId,
                LoginId = u.LoginId,
                FirstName = u.FirstName,
                MiddleName = u.MiddleName,
                LastName = u.LastName,
                EmailId = u.EmailId,
                MobileNumber = u.MobileNumber,
                IsActive = u.IsActive ?? false,
                MustChangePassword = u.MustChangePassword ?? false,
                Roles = u.UserRoles.Where(ur => ur.IsActive).Select(ur => new AdminRoleDto
                {
                    RoleId = ur.Role.RoleId,
                    RoleName = ur.Role.RoleName,
                    RoleDescription = ur.Role.RoleDescription
                }).ToList()
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<AdminRoleDto>> GetAdminRolesAsync()
    {
        return await _context.Roles
            .Where(r => r.IsActive)
            .OrderBy(r => r.RoleName)
            .Select(r => new AdminRoleDto
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName,
                RoleDescription = r.RoleDescription
            })
            .ToListAsync();
    }

    public async Task<AdminResultDto> CreateAdminUserAsync(AdminUserCreateRequest request, long currentUserId)
    {
        var loginId = (request.LoginId ?? "").Trim();
        var firstName = (request.FirstName ?? "").Trim();
        var now = DateTime.UtcNow; // UTC

        if (string.IsNullOrWhiteSpace(loginId))
            return Error("Login ID is required.");
        if (loginId.Length > 20)
            return Error("Login ID cannot exceed 20 characters.");
        if (string.IsNullOrWhiteSpace(firstName))
            return Error("First name is required.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return Error("Password is required and must be at least 6 characters.");
        if (request.RoleIds == null || request.RoleIds.Count == 0)
            return Error("At least one role must be assigned.");

        if (await _context.Users.AnyAsync(u => u.LoginId == loginId))
            return Error($"Login ID '{loginId}' is already in use.");

        var validRoleIds = await GetValidRoleIdsAsync(request.RoleIds);
        if (validRoleIds.Count != request.RoleIds.Count)
            return Error("One or more selected roles are invalid.");

        var user = new User
        {
            LoginId = loginId,
            Password = EncryptPassword(request.Password),
            FirstName = firstName,
            MiddleName = CleanOptional(request.MiddleName),
            LastName = CleanOptional(request.LastName),
            EmailId = CleanOptional(request.EmailId),
            MobileNumber = CleanOptional(request.MobileNumber),
            AddressLine1 = "",
            IsActive = request.IsActive,
            MustChangePassword = request.MustChangePassword,
            PasswordLastChangedOn = now,
            AddedBy = currentUserId,
            AddedOn = now
        };

        foreach (var roleId in validRoleIds)
        {
            user.UserRoles.Add(new UserRole { RoleId = roleId, IsActive = true });
        }

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { $"Admin user '{loginId}' created successfully." }
        };
    }

    public async Task<AdminResultDto> UpdateAdminUserAsync(long userId, AdminUserUpdateRequest request, long currentUserId)
    {
        var loginId = (request.LoginId ?? "").Trim();
        var firstName = (request.FirstName ?? "").Trim();
        var now = DateTime.UtcNow; // UTC

        if (string.IsNullOrWhiteSpace(loginId))
            return Error("Login ID is required.");
        if (loginId.Length > 20)
            return Error("Login ID cannot exceed 20 characters.");
        if (string.IsNullOrWhiteSpace(firstName))
            return Error("First name is required.");
        if (!string.IsNullOrWhiteSpace(request.Password) && request.Password.Length < 6)
            return Error("New password must be at least 6 characters.");
        if (request.RoleIds == null || request.RoleIds.Count == 0)
            return Error("At least one role must be assigned.");

        var user = await _context.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null)
            return Error("Admin user not found.");

        if (user.LoginId != loginId && await _context.Users.AnyAsync(u => u.LoginId == loginId))
            return Error($"Login ID '{loginId}' is already in use.");

        var validRoleIds = await GetValidRoleIdsAsync(request.RoleIds);
        if (validRoleIds.Count != request.RoleIds.Count)
            return Error("One or more selected roles are invalid.");

        user.LoginId = loginId;
        user.FirstName = firstName;
        user.MiddleName = CleanOptional(request.MiddleName);
        user.LastName = CleanOptional(request.LastName);
        user.EmailId = CleanOptional(request.EmailId);
        user.MobileNumber = CleanOptional(request.MobileNumber);
        user.IsActive = request.IsActive;
        user.MustChangePassword = request.MustChangePassword;
        user.ModifiedBy = currentUserId;
        user.ModifiedOn = now;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.Password = EncryptPassword(request.Password);
            user.PasswordLastChangedOn = now;
        }

        // Synchronize role assignments (keep historical rows, toggle IsActive)
        var existingRoles = user.UserRoles.ToList();
        foreach (var roleId in validRoleIds)
        {
            var assignment = existingRoles.FirstOrDefault(ur => ur.RoleId == roleId);
            if (assignment == null)
                user.UserRoles.Add(new UserRole { RoleId = roleId, IsActive = true });
            else if (!assignment.IsActive)
                assignment.IsActive = true;
        }

        foreach (var assignment in existingRoles.Where(ur => ur.IsActive))
        {
            if (!validRoleIds.Contains(assignment.RoleId))
                assignment.IsActive = false;
        }

        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { $"Admin user '{loginId}' updated successfully." }
        };
    }

    private async Task<List<short>> GetValidRoleIdsAsync(List<short> roleIds)
    {
        var distinct = roleIds.Distinct().ToList();
        return await _context.Roles
            .Where(r => distinct.Contains(r.RoleId) && r.IsActive)
            .Select(r => r.RoleId)
            .ToListAsync();
    }

    private string EncryptPassword(string password)
    {
        return CreateHmacSha256Token(password, _pwdSecret);
    }

    private static string CreateHmacSha256Token(string message, string secret)
    {
        secret ??= "";
        var encoding = new ASCIIEncoding();
        byte[] keyByte = encoding.GetBytes(secret);
        byte[] messageBytes = encoding.GetBytes(message);
        using (var hmacsha256 = new HMACSHA256(keyByte))
        {
            byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);
            return Convert.ToBase64String(hashmessage);
        }
    }

    private static string? CleanOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static AdminResultDto Error(string message)
    {
        return new AdminResultDto { Result = 0, Messages = new[] { message } };
    }

}
