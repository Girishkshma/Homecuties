using HC.Business.Dtos;
using HC.Data;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

public class ProductService : IProductService
{
    private readonly HomecutiesDbContext _context;

    public ProductService(HomecutiesDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ProductDto>> GetProductsForHomepageAsync()
    {
        return await _context.Products
            .Where(p => p.DisplayOnHomePage)
            .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.ProductImages)
            .Include(p => p.ProductFeatures)
            .Select(p => MapProduct(p))
            .ToListAsync();
    }

    public async Task<ProductDto?> GetProductAsync(int productId)
    {
        return await _context.Products
            .Where(p => p.ProductId == productId)
            .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.ProductImages)
            .Include(p => p.ProductFeatures)
            .Select(p => MapProduct(p))
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<ProductDto>> GetProductsByCategoryAsync(short categoryId)
    {
        return await _context.Products
            .Where(p => p.ProductCategories.Any(pc => pc.CategoryId == categoryId && pc.IsActive))
            .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.ProductImages)
            .Include(p => p.ProductFeatures)
            .Select(p => MapProduct(p))
            .ToListAsync();
    }

    public async Task<IEnumerable<CategoryDto>> GetCategoriesAsync()
    {
        return await _context.Categories
            .Select(c => new CategoryDto
            {
                CategoryID = c.CategoryId,
                CategoryName = c.CategoryName,
                ParentCategoryID = c.ParentCategoryId
            })
            .ToListAsync();
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

    private static ProductDto MapProduct(Data.Entities.Product p) => new()
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
