using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HC.Business;

public class GoogleLoginService : IGoogleLoginService
{
    private readonly HomecutiesDbContext _context;

    public GoogleLoginService(HomecutiesDbContext context)
    {
        _context = context;
    }

    public async Task<LoginCustomerResponseDto> ValidateTokenAsync(string idToken)
    {
        var payload = ParseGoogleToken(idToken);

        if (payload == null)
            return new LoginCustomerResponseDto { Result = 0, Messages = new[] { "Invalid token" } };

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.EmailId == payload.Email);

        if (customer == null)
        {
            customer = new Customer
            {
                EmailId = payload.Email,
                FirstName = payload.FirstName ?? string.Empty,
                LastName = payload.LastName ?? string.Empty,
                CreatedOn = DateTime.UtcNow,
                ModifiedOn = DateTime.UtcNow,
                CustomerStatusId = 1
            };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
        }

        return new LoginCustomerResponseDto
        {
            Result = 1,
            Customer = new CustomerDto
            {
                CustomerID = customer.CustomerId,
                FirstName = customer.FirstName,
                MiddleName = customer.MiddleName,
                LastName = customer.LastName,
                EmailId = customer.EmailId,
                IsGuest = false
            }
        };
    }

    private static GooglePayload? ParseGoogleToken(string idToken)
    {
        try
        {
            var parts = idToken.Split('.');
            if (parts.Length < 2) return null;

            var payload = parts[1];
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            var data = JsonSerializer.Deserialize<GooglePayload>(json);
            return data;
        }
        catch
        {
            return null;
        }
    }
}

public class GooglePayload
{
    public string? Email { get; set; }
    public string? Given_Name { get; set; }
    public string? Family_Name { get; set; }
    public string? FirstName => Given_Name;
    public string? LastName => Family_Name;
}
