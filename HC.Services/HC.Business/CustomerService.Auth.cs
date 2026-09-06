// ============================================================
// CustomerService.Auth.cs
// Partial class: CustomerService - Auth operations
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

}
