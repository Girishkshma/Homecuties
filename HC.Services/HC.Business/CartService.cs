using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

public partial class CartService : ICartService
{
    // SKUStatuses.SKUStatusID = 1 => "Available"
    private const short AvailableSkuStatusId = 1;

    private readonly HomecutiesDbContext _context;

    public CartService(HomecutiesDbContext context)
    {
        _context = context;
    }

}
