using System.Security.Cryptography;
using System.Text;
using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HC.Business;

public partial class AdminDashboardService : IAdminDashboardService
{
    private readonly HomecutiesDbContext _context;
    private readonly string _pwdSecret;

    public AdminDashboardService(HomecutiesDbContext context, IConfiguration configuration)
    {
        _context = context;
        _pwdSecret = configuration["PWDSecret"] ?? "abcd1234!@#$";
    }

}
