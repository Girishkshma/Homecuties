// ============================================================
// CustomerService.Passwords.cs
// Partial class: CustomerService - Passwords operations
// ============================================================

using HC.Business.Dtos;
using HC.Business.Security;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace HC.Business;

public partial class CustomerService : ICustomerService
{
    public async Task<ResultDto> ForgotPasswordAsync(string email)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.EmailId == email);

        if (customer == null)
        {
            // Return success even if email not found to prevent email enumeration
            return new ResultDto
            {
                Result = 1,
                Messages = new[] { "If an account with that email exists, a password reset link has been sent." }
            };
        }

        // Generate a reset token (GUID-based)
        var token = Guid.NewGuid().ToString("N");

        // Store the reset request
        var resetRequest = new PasswordResetRequest
        {
            EmailId = email,
            Jwt = token,
            IsAdmin = false,
            AddedOn = DateTime.UtcNow
        };

        _context.PasswordResetRequests.Add(resetRequest);
        await _context.SaveChangesAsync();

        // In a production app, you would send an email here with the reset link.
        // For now, we'll return the token in the response so the user can use it
        // via the reset password page.
        return new ResultDto
        {
            Result = 1,
            Messages = new[] { $"If an account with that email exists, a password reset link has been sent. Token: {token}" }
        };
    }

    public async Task<ResultDto> ResetPasswordAsync(string token, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            return new ResultDto
            {
                Result = 0,
                Messages = new[] { "Password must be at least 6 characters long." }
            };
        }

        // Find a valid reset request (within 24 hours)
        var resetRequest = await _context.PasswordResetRequests
            .Where(r => r.Jwt == token && !r.IsAdmin)
            .OrderByDescending(r => r.AddedOn)
            .FirstOrDefaultAsync();

        if (resetRequest == null)
        {
            return new ResultDto
            {
                Result = 0,
                Messages = new[] { "Invalid or expired reset token." }
            };
        }

        // Check if token is expired (24 hours)
        if (resetRequest.AddedOn < DateTime.UtcNow.AddHours(-24))
        {
            return new ResultDto
            {
                Result = 0,
                Messages = new[] { "Reset token has expired. Please request a new one." }
            };
        }

        // Find the customer
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.EmailId == resetRequest.EmailId);

        if (customer == null)
        {
            return new ResultDto
            {
                Result = 0,
                Messages = new[] { "Customer not found." }
            };
        }

        // Update the password (always stored as a PBKDF2 hash)
        customer.Password = CustomerPasswordHasher.Hash(newPassword);
        customer.ModifiedOn = DateTime.UtcNow;

        // Mark the token as used by clearing it
        resetRequest.Jwt = null;

        await _context.SaveChangesAsync();

        return new ResultDto
        {
            Result = 1,
            Messages = new[] { "Password has been reset successfully. You can now log in with your new password." }
        };
    }

    /// <summary>
    /// Sets (or changes) the internal password of the signed-in customer.
    ///
    /// Accounts created through an external provider (Google) start without a password and can set
    /// one here - the storefront asks them to right after their first sign-in, so they can also log
    /// in with e-mail/password later. When a password already exists the current one must be
    /// supplied, so a hijacked session cannot silently take the account over.
    /// </summary>
    public async Task<ResultDto> SetPasswordAsync(long customerId, string? currentPassword, string newPassword)
    {
        newPassword ??= "";

        if (newPassword.Length < 6)
            return ErrorResult("Password must be at least 6 characters long.");

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer == null)
            return ErrorResult("Customer not found.");

        if (customer.CustomerStatusId != CustomerStatusActive)
            return ErrorResult("Your account is not active. Please contact support.");

        if (!string.IsNullOrEmpty(customer.Password) &&
            (string.IsNullOrEmpty(currentPassword) ||
             !CustomerPasswordHasher.Verify(customer.Password, currentPassword, out _)))
        {
            return ErrorResult("Your current password is incorrect. Use 'Forgot password' to reset it.");
        }

        customer.Password = CustomerPasswordHasher.Hash(newPassword);
        customer.ModifiedOn = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new ResultDto
        {
            Result = 1,
            Messages = new[] { "Password set successfully. You can now sign in with your email and password too." }
        };
    }

    private static ResultDto ErrorResult(string message)
    {
        return new ResultDto
        {
            Result = 0,
            Messages = new[] { message }
        };
    }
}
