using HC.Business.Dtos;
using HC.Business.Security;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HC.Business;

public class FacebookLoginService : IFacebookLoginService
{
    private const string DefaultJwtSecret = "123456789abcdefgh";

    private readonly HomecutiesDbContext _context;
    private readonly string _jwtSecret;

    public FacebookLoginService(HomecutiesDbContext context, IConfiguration configuration)
    {
        _context = context;
        _jwtSecret = configuration["JWTSecret"] ?? DefaultJwtSecret;
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

        // Same signed token as a password/Google login: the storefront sends it as
        // 'Authorization: Bearer ...' on every customer API call, so answering without one would
        // leave the customer signed in but unable to load their orders, cart or wishlist.
        var (token, expiresOn) = CustomerTokenService.Create(customer.CustomerId, customer.EmailId, _jwtSecret);

        return new LoginCustomerResponseDto
        {
            Result = 1,
            Token = token,
            ExpiresOn = expiresOn,
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
