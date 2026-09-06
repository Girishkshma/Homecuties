// ============================================================
// AdminAuthService.Auth.cs
// Partial class: AdminAuthService - Auth operations
// ============================================================

using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HC.Business;

public partial class AdminAuthService : IAdminAuthService
{
    public async Task<AdminLoginResponse> LoginAsync(string loginId, string password)
    {
        // Look up user by LoginId or EmailId (matching old app behavior)
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u =>
                (u.LoginId == loginId || (u.EmailId != null && u.EmailId == loginId)) &&
                u.IsActive == true);

        if (user == null)
        {
            return new AdminLoginResponse
            {
                Result = 0,
                Messages = new[] { "Invalid login credentials." }
            };
        }

        // Encrypt the provided password using HMACSHA256 and compare with stored hash
        var encryptedPassword = EncryptPassword(password);
        if (user.Password != encryptedPassword)
        {
            return new AdminLoginResponse
            {
                Result = 0,
                Messages = new[] { "Invalid login credentials." }
            };
        }

        // Generate JWT token matching old app format
        var token = GenerateJwtToken(user.UserId, user.LoginId, "");

        return new AdminLoginResponse
        {
            Result = 1,
            Messages = new[] { "Login successful." },
            Token = token,
            ExpiresOn = DateTime.UtcNow.AddHours(1), // UTC, 1-hour session
            User = new AdminUserDto
            {
                UserId = user.UserId,
                LoginId = user.LoginId,
                FirstName = user.FirstName,
                MiddleName = user.MiddleName,
                LastName = user.LastName,
                EmailId = user.EmailId,
                MobileNumber = user.MobileNumber,
                IsActive = user.IsActive ?? false,
                Roles = user.UserRoles
                    .Where(ur => ur.IsActive)
                    .Select(ur => new AdminRoleDto
                    {
                        RoleId = ur.Role.RoleId,
                        RoleName = ur.Role.RoleName,
                        RoleDescription = ur.Role.RoleDescription
                    }).ToList()
            }
        };
    }

    public string GenerateToken(long userId, string loginId, string ipAddress)
    {
        return GenerateJwtToken(userId, loginId, ipAddress);
    }

    public AdminUserDto? ValidateToken(string token)
    {
        return ValidateToken(token, "");
    }

    public AdminUserDto? ValidateToken(string token, string ipAddress)
    {
        try
        {
            var payload = ValidateJwtToken(token, ipAddress);
            if (payload == null)
                return null;

            var p = payload.Value;
            if (!p.TryGetProperty("UserId", out var userIdProp))
                return null;

            var userId = userIdProp.GetInt64();
            var user = _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefault(u => u.UserId == userId && u.IsActive == true);

            if (user == null)
                return null;

            return new AdminUserDto
            {
                UserId = user.UserId,
                LoginId = user.LoginId,
                FirstName = user.FirstName,
                MiddleName = user.MiddleName,
                LastName = user.LastName,
                EmailId = user.EmailId,
                MobileNumber = user.MobileNumber,
                IsActive = user.IsActive ?? false,
                Roles = user.UserRoles
                    .Where(ur => ur.IsActive)
                    .Select(ur => new AdminRoleDto
                    {
                        RoleId = ur.Role.RoleId,
                        RoleName = ur.Role.RoleName,
                        RoleDescription = ur.Role.RoleDescription
                    }).ToList()
            };
        }
        catch
        {
            return null;
        }
    }

}
