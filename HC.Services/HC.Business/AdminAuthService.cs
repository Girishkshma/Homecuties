using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HC.Business;

public partial class AdminAuthService : IAdminAuthService
{
    private readonly HomecutiesDbContext _context;
    private readonly string _jwtSecret;
    private readonly string _pwdSecret;

    public AdminAuthService(HomecutiesDbContext context, IConfiguration configuration)
    {
        _context = context;
        _jwtSecret = configuration["JWTSecret"] ?? "123456789abcdefgh";
        _pwdSecret = configuration["PWDSecret"] ?? "abcd1234!@#$";
    }

}
