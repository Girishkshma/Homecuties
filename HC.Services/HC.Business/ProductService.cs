using System.Linq.Expressions;
using HC.Business.Dtos;
using HC.Data;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

public class ProductService : IProductService
{
    // SKUStatuses.SKUStatusID = 1 => "Available".
    // Only Available SKUs that are not already reserved by an order count as sellable stock.
    private const short AvailableSkuStatusId = 1;

    private readonly HomecutiesDbContext _context;

    public ProductService(HomecutiesDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ProductDto>> GetProductsForHomepageAsync()
    {
        return await _context.Products
            .Where(p => p.DisplayOnHomePage && p.ProductStatusId != 2) // 2 = Suspended (disabled)
            .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.ProductImages)
            .Include(p => p.ProductFeatures)
            .Select(ProductProjection)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProductDto>> GetActiveProductsAsync()
    {
        return await _context.Products
            .Where(p => p.ProductStatusId != 2) // 2 = Suspended (disabled)
            .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.ProductImages)
            .Include(p => p.ProductFeatures)
            .Select(ProductProjection)
            .ToListAsync();
    }

    public async Task<ProductDto?> GetProductAsync(int productId)
    {
        return await _context.Products
            .Where(p => p.ProductId == productId && p.ProductStatusId != 2) // 2 = Suspended (disabled)
            .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.ProductImages)
            .Include(p => p.ProductFeatures)
            .Select(ProductProjection)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<ProductDto>> GetProductsByCategoryAsync(short categoryId)
    {
        return await _context.Products
            .Where(p => p.ProductCategories.Any(pc => pc.CategoryId == categoryId && pc.IsActive)
                && p.ProductStatusId != 2) // 2 = Suspended (disabled)
            .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.ProductImages)
            .Include(p => p.ProductFeatures)
            .Select(ProductProjection)
            .ToListAsync();
    }

    public async Task<IEnumerable<CategoryDto>> GetCategoriesAsync()
    {
        return await _context.Categories
            .Where(c => c.ProductCategories.Any(pc => pc.IsActive))
            .Select(c => new CategoryDto
            {
                CategoryID = c.CategoryId,
                CategoryName = c.CategoryName,
                ParentCategoryID = c.ParentCategoryId
            })
            .ToListAsync();
    }

    /// <summary>Live counts for the storefront hero section, read straight from the database.</summary>
    public async Task<HomeStatsDto> GetHomeStatsAsync()
    {
        var productCount = await _context.Products
            .CountAsync(p => p.ProductStatusId != 2); // 2 = Suspended (disabled)

        var categoryCount = await _context.Categories
            .CountAsync(c => c.ProductCategories.Any(pc => pc.IsActive));

        var customerCount = await _context.Customers.CountAsync();

        return new HomeStatsDto
        {
            ProductCount = productCount,
            CategoryCount = categoryCount,
            CustomerCount = customerCount
        };
    }

    public async Task<CategoryDto?> GetCategoryAsync(short categoryId)
    {
        return await _context.Categories
            .Where(c => c.CategoryId == categoryId)
            .Select(c => new CategoryDto
            {
                CategoryID = c.CategoryId,
                CategoryName = c.CategoryName,
                ParentCategoryID = c.ParentCategoryId
            })
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Projection used by all product queries. It is an <see cref="Expression{TDelegate}"/> so
    /// EF Core can translate it into SQL - including the stock calculation. A plain C# method
    /// would be evaluated client-side after materialisation, where the (never Included)
    /// PurchaseDetails collection is empty, which made every product report "Out of Stock".
    /// </summary>
    private static readonly Expression<Func<Data.Entities.Product, ProductDto>> ProductProjection = p => new ProductDto
    {
        ProductID = p.ProductId,
        ProductName = p.ProductName,
        ProductTitle = p.ProductTitle,
        ProductDescription = p.ProductDescription,
        PromoImage = p.ProductImages
            .Where(pi => pi.IsPromoImage && pi.IsActive)
            .Select(pi => pi.ImageUrl)
            .FirstOrDefault() ?? "",
        ProductImages = p.ProductImages
            .Where(pi => pi.IsActive)
            .OrderBy(pi => pi.ImageIndex)
            .Select(pi => pi.ImageUrl)
            .ToList(),
        SalesPrice = p.UnitPrice,
        PreDiscountSalesPrice = p.UnitPrice,
        PostDiscountSalesPrice = p.UnitPrice - (p.UnitPrice * p.DiscountPercent / 100),
        PostAdditionalDiscountSalesPrice = p.UnitPrice - (p.UnitPrice * (p.DiscountPercent + p.AdditionalDiscountPercent) / 100),
        DiscountPercent = p.DiscountPercent,
        AdditionalDiscountPercent = p.AdditionalDiscountPercent,
        CGSTPercent = p.Cgstpercent,
        SGSTPercent = p.Sgstpercent,
        IGSTPercent = p.Igstpercent,
        IsInStock = p.PurchaseDetails.SelectMany(pd => pd.Skus).Any(s => s.SkustatusId == AvailableSkuStatusId && !s.OrderItems.Any()),
        AvailableQty = p.PurchaseDetails.SelectMany(pd => pd.Skus).Count(s => s.SkustatusId == AvailableSkuStatusId && !s.OrderItems.Any()),
        Features = p.ProductFeatures
            .Where(pf => pf.IsActive)
            .Select(pf => new ProductFeatureDto
            {
                FeatureID = pf.ProductFeatureId,
                Feature = pf.ProductFeature1,
                IsActive = pf.IsActive
            }).ToList(),
        Categories = p.ProductCategories
            .Where(pc => pc.IsActive)
            .Select(pc => new CategoryDto
            {
                CategoryID = pc.Category.CategoryId,
                CategoryName = pc.Category.CategoryName,
                ParentCategoryID = pc.Category.ParentCategoryId
            }).ToList()
    };
}
