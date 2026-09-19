using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace HC.Business;

public partial class CustomerService : ICustomerService
{
    private const string DefaultJwtSecret = "123456789abcdefgh";

    /// <summary>Customers.CustomerStatusID - only ACTIVE customers may sign in.</summary>
    private const short CustomerStatusActive = 1;

    private readonly HomecutiesDbContext _context;
    private readonly string _jwtSecret;

    public CustomerService(HomecutiesDbContext context, IConfiguration configuration)
    {
        _context = context;
        _jwtSecret = configuration["JWTSecret"] ?? DefaultJwtSecret;
    }

}
