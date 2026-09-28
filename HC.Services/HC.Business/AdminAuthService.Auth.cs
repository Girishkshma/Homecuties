// ============================================================
// AdminAuthService.Auth.cs
// Partial class: AdminAuthService - Auth operations
// ============================================================

using HC.Business.Dtos;
using HC.Business.Security;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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
            return InvalidCredentials();

        // Encrypt the provided password using HMACSHA256 and compare with stored hash
        var encryptedPassword = EncryptPassword(password);
        if (user.Password != encryptedPassword)
            return InvalidCredentials();

        var roles = GetActiveRoles(user);

        // An admin area session always starts with a role: the role claims in the token are what
        // authorize the individual sections of the admin area.
        if (roles.Count == 0)
        {
            return new AdminLoginResponse
            {
                Result = 0,
                Messages = new[] { "Your account does not have an active admin role. Please ask a Super Admin to grant you access." }
            };
        }

        var adminUser = MapUser(user, roles);

        // JWT carrying the user id, the login id, the roles and the login time/expiry (all UTC).
        var (token, expiresOn) = AdminJwtTokenService.Create(adminUser, roles, _configuration);

        return new AdminLoginResponse
        {
            Result = 1,
            Messages = new[] { "Login successful." },
            Token = token,
            ExpiresOn = expiresOn,
            User = adminUser
        };
    }

    public string GenerateToken(long userId, string loginId, string ipAddress)
    {
        var user = _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefault(u => u.UserId == userId);

        if (user == null)
            return string.Empty;

        var roles = GetActiveRoles(user);
        return AdminJwtTokenService.Create(MapUser(user, roles), roles, _configuration).Token;
    }

    public AdminUserDto? ValidateToken(string token)
    {
        return ValidateToken(token, "");
    }

    public AdminUserDto? ValidateToken(string token, string ipAddress)
    {
        // Signature, issuer, audience and expiry are validated by the shared token service.
        // The address the request came from is deliberately NOT part of the token any more: the API
        // sits behind a reverse proxy, so the address the application sees is the proxy's.
        var principal = AdminJwtTokenService.Validate(token, _configuration);
        if (principal == null)
            return null;

        var userId = AdminJwtTokenService.GetUserId(principal);
        if (userId <= 0)
            return null;

        var user = _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefault(u => u.UserId == userId && u.IsActive == true);

        if (user == null)
            return null;

        return MapUser(user, GetActiveRoles(user));
    }

    private static AdminLoginResponse InvalidCredentials() => new()
    {
        Result = 0,
        Messages = new[] { "Invalid login credentials." }
    };

    private static List<AdminRoleDto> GetActiveRoles(User user) => user.UserRoles
        .Where(ur => ur.IsActive)
        .Select(ur => new AdminRoleDto
        {
            RoleId = ur.Role.RoleId,
            RoleName = ur.Role.RoleName,
            RoleDescription = ur.Role.RoleDescription
        })
        .ToList();

    private static AdminUserDto MapUser(User user, List<AdminRoleDto> roles) => new()
    {
        UserId = user.UserId,
        LoginId = user.LoginId,
        FirstName = user.FirstName,
        MiddleName = user.MiddleName,
        LastName = user.LastName,
        EmailId = user.EmailId,
        MobileNumber = user.MobileNumber,
        IsActive = user.IsActive ?? false,
        Roles = roles
    };

}

