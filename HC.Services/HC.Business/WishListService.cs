using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

public class WishListService : IWishListService
{
    private readonly HomecutiesDbContext _context;

    public WishListService(HomecutiesDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<WishListDto>> GetWishListAsync(long customerId)
    {
        return await _context.WishLists
            .Where(w => w.CustomerId == customerId)
            .Include(w => w.Product)
                .ThenInclude(p => p.ProductImages)
            .OrderByDescending(w => w.AddedOn)
            .Select(w => new WishListDto
            {
                CustomerId = w.CustomerId,
                ProductId = w.ProductId,
                AddedOn = w.AddedOn,
                ProductName = w.Product.ProductName,
                ProductTitle = w.Product.ProductTitle,
                ProductDescription = w.Product.ProductDescription,
                PromoImage = w.Product.ProductImages
                    .Where(pi => pi.IsPromoImage && pi.IsActive)
                    .Select(pi => pi.ImageUrl)
                    .FirstOrDefault() ?? "",
                SalesPrice = w.Product.UnitPrice,
                PostDiscountSalesPrice = w.Product.UnitPrice - (w.Product.UnitPrice * w.Product.DiscountPercent / 100),
                PostAdditionalDiscountSalesPrice = w.Product.UnitPrice - (w.Product.UnitPrice * (w.Product.DiscountPercent + w.Product.AdditionalDiscountPercent) / 100),
                DiscountPercent = w.Product.DiscountPercent,
                AdditionalDiscountPercent = w.Product.AdditionalDiscountPercent
            })
            .ToListAsync();
    }

    public async Task<ResultDto> AddToWishListAsync(long customerId, int productId)
    {
        // Check if already in wishlist
        var existing = await _context.WishLists
            .FirstOrDefaultAsync(w => w.CustomerId == customerId && w.ProductId == productId);

        if (existing != null)
        {
            return new ResultDto
            {
                Result = 1,
                Messages = new[] { "Product is already in your wishlist." }
            };
        }

        // Check if product exists
        var productExists = await _context.Products.AnyAsync(p => p.ProductId == productId);
        if (!productExists)
        {
            return new ResultDto
            {
                Result = 0,
                Messages = new[] { "Product not found." }
            };
        }

        var wishListItem = new WishList
        {
            CustomerId = customerId,
            ProductId = productId,
            AddedOn = DateTime.UtcNow
        };

        _context.WishLists.Add(wishListItem);
        await _context.SaveChangesAsync();

        return new ResultDto
        {
            Result = 1,
            Messages = new[] { "Product added to wishlist." }
        };
    }

    public async Task<ResultDto> RemoveFromWishListAsync(long customerId, int productId)
    {
        var wishListItem = await _context.WishLists
            .FirstOrDefaultAsync(w => w.CustomerId == customerId && w.ProductId == productId);

        if (wishListItem == null)
        {
            return new ResultDto
            {
                Result = 0,
                Messages = new[] { "Product not found in wishlist." }
            };
        }

        _context.WishLists.Remove(wishListItem);
        await _context.SaveChangesAsync();

        return new ResultDto
        {
            Result = 1,
            Messages = new[] { "Product removed from wishlist." }
        };
    }

    public async Task<bool> IsInWishListAsync(long customerId, int productId)
    {
        return await _context.WishLists
            .AnyAsync(w => w.CustomerId == customerId && w.ProductId == productId);
    }
}
