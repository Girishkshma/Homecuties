using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

public class FacebookLoginService : IFacebookLoginService
{
    private readonly HomecutiesDbContext _context;

    public FacebookLoginService(HomecutiesDbContext context)
    {
        _context = context;
    }

    public async Task<LoginCustomerResponseDto> ValidateTokenAsync(string id, string accessToken)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.GooglePayLoad != null && c.GooglePayLoad.Contains(id));

        if (customer == null)
        {
            customer = new Customer
            {
                EmailId = $"{id}@facebook.com",
                FirstName = "Facebook",
                LastName = "User",
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
}
