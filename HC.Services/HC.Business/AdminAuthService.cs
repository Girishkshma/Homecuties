using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HC.Business;

public class AdminAuthService : IAdminAuthService
{
    private readonly HomecutiesDbContext _context;
    private readonly string _jwtSecret;
    private readonly string _pwdSecret;

    public AdminAuthService(HomecutiesDbContext context, IConfiguration configuration)
    {
        _context = context;
        _jwtSecret = configuration["JWTSecret"] ?? "123456789abcdefgh";
        _pwdSecret = configuration["PWDSecret"] ?? "abcd1234!@#$";
    }

    /// <summary>
    /// Creates an HMACSHA256 hash of the input string using the given secret key.
    /// Matches the old HC.Common.Crypto.CreateHMACSHA256Token implementation.
    /// </summary>
    private static string CreateHMACSHA256Token(string message, string secret)
    {
        secret = secret ?? "";
        var encoding = new ASCIIEncoding();
        byte[] keyByte = encoding.GetBytes(secret);
        byte[] messageBytes = encoding.GetBytes(message);
        using (var hmacsha256 = new HMACSHA256(keyByte))
        {
            byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);
            return Convert.ToBase64String(hashmessage);
        }
    }

    /// <summary>
    /// Encrypts a password using HMACSHA256 with the PWDSecret.
    /// Matches the old app's password encryption: CreateHMACSHA256Token(password, PWDSecret)
    /// </summary>
    private string EncryptPassword(string password)
    {
        return CreateHMACSHA256Token(password, _pwdSecret);
    }

    /// <summary>
    /// Generates a JWT token matching the old app's format:
    /// base64(header).base64(body).HMACSHA256(base64(header)+"."+base64(body), JWTSecret)
    /// </summary>
    private string GenerateJwtToken(long userId, string loginId, string ipAddress)
    {
        var header = new { Type = "JWT", Crypto = "HMACSHA256" };
        var now = DateTime.UtcNow.AddHours(5.5); // IST
        var body = new
        {
            UserId = userId,
            LoginId = loginId,
            IPAddress = ipAddress,
            Created = now.ToString("yyyyMMddHHmmss"),
            Expires = now.AddHours(1).ToString("yyyyMMddHHmmss")
        };

        var headerSer = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(header)));
        var bodySer = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body)));

        var token = CreateHMACSHA256Token(headerSer + "." + bodySer, _jwtSecret);

        return headerSer + "." + bodySer + "." + token;
    }

    /// <summary>
    /// Validates a JWT token matching the old app's format.
    /// Returns the deserialized payload if valid, null otherwise.
    /// </summary>
    private JsonElement? ValidateJwtToken(string jwt, string ipAddress)
    {
        try
        {
            var ar = jwt.Split('.');
            if (ar.Length != 3)
                return null;

            // Verify signature
            var expectedSig = CreateHMACSHA256Token(ar[0] + "." + ar[1], _jwtSecret);
            if (expectedSig != ar[2])
                return null;

            // Decode payload
            var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(ar[1]));
            var payload = JsonSerializer.Deserialize<JsonElement>(payloadJson);

            // Verify IP address
            if (payload.TryGetProperty("IPAddress", out var ipProp))
            {
                if (ipProp.GetString() != ipAddress)
                    return null;
            }

            // Verify expiration
            if (payload.TryGetProperty("Expires", out var expProp))
            {
                var expStr = expProp.GetString();
                if (!string.IsNullOrEmpty(expStr))
                {
                    if (DateTime.ParseExact(expStr, "yyyyMMddHHmmss", null) < DateTime.UtcNow.AddHours(5.5))
                        return null;
                }
            }

            return payload;
        }
        catch
        {
            return null;
        }
    }

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
            ExpiresOn = DateTime.UtcNow.AddHours(5.5).AddHours(1), // IST + 1 hour
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

    public async Task<List<AdminMenuDto>> GetMenusByRoleAsync(short roleId)
    {
        var menus = await _context.AdminMenusRoles
            .Where(amr => amr.RoleId == roleId && amr.IsActive)
            .Include(amr => amr.Menu)
                .ThenInclude(m => m.InverseParentMenu)
            .Include(amr => amr.Menu)
                .ThenInclude(m => m.AdminActivities.Where(a => a.IsActive))
            .Select(amr => amr.Menu)
            .ToListAsync();

        return BuildMenuTree(menus.Where(m => m.ParentMenuId == null).ToList(), menus.ToList());
    }

    public async Task<List<AdminMenuDto>> GetAllMenusAsync()
    {
        var menus = await _context.AdminMenus
            .Include(m => m.InverseParentMenu)
            .Include(m => m.AdminActivities.Where(a => a.IsActive))
            .Where(m => m.IsActive)
            .ToListAsync();

        return BuildMenuTree(menus.Where(m => m.ParentMenuId == null).ToList(), menus);
    }

    public async Task<AdminResultDto> ForgotPasswordAsync(string loginId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u =>
            (u.LoginId == loginId || (u.EmailId != null && u.EmailId == loginId)) &&
            u.IsActive == true);

        if (user == null)
        {
            // Return success even if user not found to prevent enumeration
            return new AdminResultDto
            {
                Result = 1,
                Messages = new[] { "If an account with that login ID exists, a password reset link has been sent." }
            };
        }

        // Generate a JWT-based reset token matching old app format
        var now = DateTime.UtcNow.AddHours(5.5);
        var resetData = new
        {
            EmailID = user.EmailId ?? user.LoginId,
            IPAddress = "",
            RequestDate = now.ToString("yyyyMMddHHmmssfff")
        };

        var resetDataJson = JsonSerializer.Serialize(resetData);
        var resetDataB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(resetDataJson));
        var resetToken = CreateHMACSHA256Token(resetDataB64, _jwtSecret);
        var jwt = resetDataB64 + "." + resetToken;

        // Store the reset request
        var resetRequest = new PasswordResetRequest
        {
            EmailId = user.EmailId ?? user.LoginId,
            Jwt = jwt,
            IsAdmin = true,
            AddedOn = DateTime.UtcNow
        };

        _context.PasswordResetRequests.Add(resetRequest);
        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { $"If an account with that login ID exists, a password reset link has been sent. Token: {Convert.ToBase64String(Encoding.UTF8.GetBytes(jwt))}" }
        };
    }

    public async Task<AdminResultDto> ResetPasswordAsync(string token, string newPassword)
    {
        try
        {
            // Decode the token (it was base64 encoded before sending)
            var jwt = Encoding.UTF8.GetString(Convert.FromBase64String(token));

            // Validate the JWT format
            var ar = jwt.Split('.');
            if (ar.Length != 2)
            {
                return new AdminResultDto
                {
                    Result = 0,
                    Messages = new[] { "Invalid reset token." }
                };
            }

            var expectedSig = CreateHMACSHA256Token(ar[0], _jwtSecret);
            if (expectedSig != ar[1])
            {
                return new AdminResultDto
                {
                    Result = 0,
                    Messages = new[] { "Invalid reset token." }
                };
            }

            // Decode the payload
            var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(ar[0]));
            var payload = JsonSerializer.Deserialize<JsonElement>(payloadJson);

            if (!payload.TryGetProperty("EmailID", out var emailProp))
            {
                return new AdminResultDto
                {
                    Result = 0,
                    Messages = new[] { "Invalid reset token." }
                };
            }

            var emailId = emailProp.GetString() ?? "";

            // Find a valid reset request
            var resetRequest = await _context.PasswordResetRequests
                .Where(r => r.Jwt == jwt && r.IsAdmin)
                .OrderByDescending(r => r.AddedOn)
                .FirstOrDefaultAsync();

            if (resetRequest == null)
            {
                return new AdminResultDto
                {
                    Result = 0,
                    Messages = new[] { "Invalid or expired reset token." }
                };
            }

            // Check if token is expired (24 hours)
            if (resetRequest.AddedOn < DateTime.UtcNow.AddHours(-24))
            {
                return new AdminResultDto
                {
                    Result = 0,
                    Messages = new[] { "Reset token has expired. Please request a new one." }
                };
            }

            // Find the user by email or login ID
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                (u.EmailId != null && u.EmailId == emailId) ||
                u.LoginId == emailId);

            if (user == null)
            {
                return new AdminResultDto
                {
                    Result = 0,
                    Messages = new[] { "User not found." }
                };
            }

            // Update the password using HMACSHA256 encryption (matching old app)
            user.Password = EncryptPassword(newPassword);
            user.ModifiedOn = DateTime.UtcNow;

            // Mark the token as used
            resetRequest.Jwt = null;

            await _context.SaveChangesAsync();

            return new AdminResultDto
            {
                Result = 1,
                Messages = new[] { "Password has been reset successfully. You can now log in with your new password." }
            };
        }
        catch
        {
            return new AdminResultDto
            {
                Result = 0,
                Messages = new[] { "Invalid reset token." }
            };
        }
    }

    private List<AdminMenuDto> BuildMenuTree(List<AdminMenu> parentMenus, List<AdminMenu> allMenus)
    {
        var result = new List<AdminMenuDto>();

        foreach (var menu in parentMenus.OrderBy(m => m.MenuId))
        {
            var dto = new AdminMenuDto
            {
                MenuId = menu.MenuId,
                MenuTitle = menu.MenuTitle,
                MenuDescription = menu.MenuDescription,
                MenuUrl = menu.MenuUrl,
                ParentMenuId = menu.ParentMenuId,
                IsActive = menu.IsActive,
                Activities = menu.AdminActivities.Select(a => new AdminActivityDto
                {
                    ActivityId = a.ActivityId,
                    ActivityTitle = a.ActivityTitle,
                    MenuId = a.MenuId,
                    IsActive = a.IsActive
                }).ToList(),
                Children = BuildMenuTree(
                    allMenus.Where(m => m.ParentMenuId == menu.MenuId).ToList(),
                    allMenus)
            };

            result.Add(dto);
        }

        return result;
    }
}
