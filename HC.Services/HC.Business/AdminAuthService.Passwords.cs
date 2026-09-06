// ============================================================
// AdminAuthService.Passwords.cs
// Partial class: AdminAuthService - Passwords operations
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
        var now = DateTime.UtcNow; // UTC
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

}
