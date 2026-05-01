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
}
