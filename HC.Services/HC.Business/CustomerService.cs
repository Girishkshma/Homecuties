using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace HC.Business;

public class CustomerService : ICustomerService
{
    private readonly HomecutiesDbContext _context;

    public CustomerService(HomecutiesDbContext context)
    {
        _context = context;
    }

    public async Task<CustomerDto?> GetCustomerAsync(long customerId)
    {
        return await _context.Customers
            .Where(c => c.CustomerId == customerId)
            .Select(c => new CustomerDto
            {
                CustomerID = c.CustomerId,
                FirstName = c.FirstName,
                MiddleName = c.MiddleName,
                LastName = c.LastName,
                EmailId = c.EmailId,
                MobileNumber = c.MobileNumber,
                MobileIsd = c.MobileIsd,
                IsGuest = false
            })
            .FirstOrDefaultAsync();
    }

    public JwtResponseDto GetCustomerJwt(long customerId, string email, string ipAddress)
    {
        var payload = JsonSerializer.Serialize(new
        {
            CustomerID = customerId,
            Email = email,
            IP = ipAddress
        });

        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
        return new JwtResponseDto { Token = token, ExpiresOn = DateTime.UtcNow.AddDays(7) };
    }

    public JwtValidationDto ValidateCustomerJwt(string jwt)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(jwt));
            return new JwtValidationDto { Valid = true, Data = json };
        }
        catch
        {
            return new JwtValidationDto { Valid = false };
        }
    }

    public async Task<GuestCustomerDto> CreateGuestCustomerAsync()
    {
        var guest = new GuestCustomer
        {
            CreatedOn = DateTime.UtcNow
        };

        _context.GuestCustomers.Add(guest);
        await _context.SaveChangesAsync();

        return new GuestCustomerDto
        {
            CustomerID = guest.CustomerId,
            FirstName = "Guest",
            MiddleName = "",
            LastName = "",
            IsGuest = true
        };
    }

    public async Task<LoginCustomerResponseDto> CreateCustomerAsync(string firstName, string lastName, string email, string password)
    {
        // Check if email already exists
        var existingCustomer = await _context.Customers
            .FirstOrDefaultAsync(c => c.EmailId == email);

        if (existingCustomer != null)
        {
            return new LoginCustomerResponseDto
            {
                Result = 0,
                Messages = new[] { "An account with this email already exists." },
                Customer = null
            };
        }

        var customer = new Customer
        {
            FirstName = firstName,
            LastName = lastName,
            EmailId = email,
            Password = password,
            CreatedOn = DateTime.UtcNow,
            ModifiedOn = DateTime.UtcNow,
            CustomerStatusId = 1, // Active status
            EmailVerfied = false
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        return new LoginCustomerResponseDto
        {
            Result = 1,
            Messages = new[] { "Account created successfully." },
            Customer = new CustomerDto
            {
                CustomerID = customer.CustomerId,
                FirstName = customer.FirstName,
                MiddleName = customer.MiddleName ?? "",
                LastName = customer.LastName ?? "",
                EmailId = customer.EmailId,
                MobileNumber = customer.MobileNumber ?? "",
                MobileIsd = customer.MobileIsd ?? "",
                IsGuest = false
            }
        };
    }

    public async Task<LoginCustomerResponseDto> LoginAsync(string email, string password)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.EmailId == email);

        if (customer == null)
        {
            return new LoginCustomerResponseDto
            {
                Result = 0,
                Messages = new[] { "Invalid email or password." },
                Customer = null
            };
        }

        if (customer.Password != password)
        {
            return new LoginCustomerResponseDto
            {
                Result = 0,
                Messages = new[] { "Invalid email or password." },
                Customer = null
            };
        }

        return new LoginCustomerResponseDto
        {
            Result = 1,
            Messages = new[] { "Login successful." },
            Customer = new CustomerDto
            {
                CustomerID = customer.CustomerId,
                FirstName = customer.FirstName,
                MiddleName = customer.MiddleName ?? "",
                LastName = customer.LastName ?? "",
                EmailId = customer.EmailId,
                MobileNumber = customer.MobileNumber ?? "",
                MobileIsd = customer.MobileIsd ?? "",
                IsGuest = false
            }
        };
    }

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
