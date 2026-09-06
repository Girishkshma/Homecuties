using System.Security.Cryptography;
using System.Text;
using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HC.Business;

public partial class OrderService : IOrderService
{
    private readonly HomecutiesDbContext _context;
    private readonly IConfiguration _configuration;

    public OrderService(HomecutiesDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

}
