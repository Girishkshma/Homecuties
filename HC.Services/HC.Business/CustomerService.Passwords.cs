// ============================================================
// CustomerService.Passwords.cs
// Partial class: CustomerService - Passwords operations
// ============================================================

using HC.Business.Dtos;
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

        // Update the password
        customer.Password = newPassword;
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
}
