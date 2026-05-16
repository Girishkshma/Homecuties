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

    public async Task<IEnumerable<WishListDto>> GetWishListAsync(long customerId, bool isGuest)
    {
        if (isGuest)
        {
            return await _context.Set<GuestWishList>()
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
                    AdditionalDiscountPercent = w.Product.AdditionalDiscountPercent,
                    IsInStock = w.Product.PurchaseDetails.Any(pd => pd.Skus.Any(s => !s.OrderItems.Any()))
                })
                .ToListAsync();
        }
        else
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
                    AdditionalDiscountPercent = w.Product.AdditionalDiscountPercent,
                    IsInStock = w.Product.PurchaseDetails.Any(pd => pd.Skus.Any(s => !s.OrderItems.Any()))
                })
                .ToListAsync();
        }
    }

    public async Task<ResultDto> AddToWishListAsync(long customerId, int productId, bool isGuest)
    {
        if (isGuest)
        {
            // Check if already in guest wishlist
            var existing = await _context.Set<GuestWishList>()
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

            // Ensure a GuestCustomer record exists
            var guestCustomer = await _context.Set<GuestCustomer>()
                .FirstOrDefaultAsync(gc => gc.CustomerId == customerId);

            if (guestCustomer == null)
            {
                guestCustomer = new GuestCustomer
                {
                    CreatedOn = DateTime.UtcNow
                };
                _context.Set<GuestCustomer>().Add(guestCustomer);
                await _context.SaveChangesAsync();
                customerId = guestCustomer.CustomerId;
            }

            var wishListItem = new GuestWishList
            {
                CustomerId = customerId,
                ProductId = productId,
                AddedOn = DateTime.UtcNow
            };

            _context.Set<GuestWishList>().Add(wishListItem);
        }
        else
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
        }

        await _context.SaveChangesAsync();

        return new ResultDto
        {
            Result = 1,
            Messages = new[] { "Product added to wishlist." }
        };
    }

    public async Task<ResultDto> RemoveFromWishListAsync(long customerId, int productId, bool isGuest)
    {
        if (isGuest)
        {
            var wishListItem = await _context.Set<GuestWishList>()
                .FirstOrDefaultAsync(w => w.CustomerId == customerId && w.ProductId == productId);

            if (wishListItem == null)
            {
                return new ResultDto
                {
                    Result = 0,
                    Messages = new[] { "Product not found in wishlist." }
                };
            }

            _context.Set<GuestWishList>().Remove(wishListItem);
        }
        else
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
        }

        await _context.SaveChangesAsync();

        return new ResultDto
        {
            Result = 1,
            Messages = new[] { "Product removed from wishlist." }
        };
    }

    public async Task<bool> IsInWishListAsync(long customerId, int productId, bool isGuest)
    {
        if (isGuest)
        {
            return await _context.Set<GuestWishList>()
                .AnyAsync(w => w.CustomerId == customerId && w.ProductId == productId);
        }
        else
        {
            return await _context.WishLists
                .AnyAsync(w => w.CustomerId == customerId && w.ProductId == productId);
        }
    }

    public async Task<ResultDto> TransferGuestWishListAsync(long guestCustomerId, long customerId)
    {
        var guestWishListItems = await _context.Set<GuestWishList>()
            .Where(w => w.CustomerId == guestCustomerId)
            .ToListAsync();

        if (guestWishListItems.Count == 0)
            return new ResultDto { Result = 1, Messages = new[] { "No guest wishlist to transfer" } };

        foreach (var guestItem in guestWishListItems)
        {
            var existing = await _context.WishLists
                .FirstOrDefaultAsync(w => w.CustomerId == customerId && w.ProductId == guestItem.ProductId);

            if (existing == null)
            {
                _context.WishLists.Add(new WishList
                {
                    CustomerId = customerId,
                    ProductId = guestItem.ProductId,
                    AddedOn = guestItem.AddedOn
                });
            }

            _context.Set<GuestWishList>().Remove(guestItem);
        }

        await _context.SaveChangesAsync();
        return new ResultDto { Result = 1, Messages = new[] { "Wishlist transferred" } };
    }
}
