// ============================================================
// CustomerService.Profile.cs
// Partial class: CustomerService - Profile operations
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
