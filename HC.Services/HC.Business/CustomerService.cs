using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace HC.Business;

public partial class CustomerService : ICustomerService
{
    private readonly HomecutiesDbContext _context;

    public CustomerService(HomecutiesDbContext context)
    {
        _context = context;
    }

}
