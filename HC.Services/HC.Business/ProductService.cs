using System.Linq.Expressions;
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
        var products = await _context.Products
            .Where(p => p.DisplayOnHomePage && p.ProductStatusId != 2) // 2 = Suspended (disabled)
            .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.ProductImages)
            .Include(p => p.ProductFeatures)
            .Select(ProductProjection)
            .ToListAsync();

        await AddStockAsync(products);
        return products;
    }

    public async Task<IEnumerable<ProductDto>> GetActiveProductsAsync()
    {
        var products = await _context.Products
            .Where(p => p.ProductStatusId != 2) // 2 = Suspended (disabled)
            .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.ProductImages)
            .Include(p => p.ProductFeatures)
            .Select(ProductProjection)
            .ToListAsync();

        await AddStockAsync(products);
        return products;
    }

    public async Task<ProductDto?> GetProductAsync(int productId)
    {
        var product = await _context.Products
            .Where(p => p.ProductId == productId && p.ProductStatusId != 2) // 2 = Suspended (disabled)
            .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.ProductImages)
            .Include(p => p.ProductFeatures)
            .Select(ProductProjection)
            .FirstOrDefaultAsync();

        if (product == null)
            return null;

        await AddStockAsync(new[] { product });
        return product;
    }

    public async Task<IEnumerable<ProductDto>> GetProductsByCategoryAsync(short categoryId)
    {
        var products = await _context.Products
            .Where(p => p.ProductCategories.Any(pc => pc.CategoryId == categoryId && pc.IsActive)
                && p.ProductStatusId != 2) // 2 = Suspended (disabled)
            .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.ProductImages)
            .Include(p => p.ProductFeatures)
            .Select(ProductProjection)
            .ToListAsync();

        await AddStockAsync(products);
        return products;
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
    /// Reads the sellable units of the given products and writes them into their
    /// <see cref="ProductDto.IsInStock"/> and <see cref="ProductDto.AvailableQty"/> fields, which
    /// <see cref="ProductProjection"/> leaves at their defaults.
    ///
    /// The units are counted by <see cref="SkuAvailability.CountSellableByProductAsync"/> instead of
    /// inside the projection so that every stock figure - the product list here, the cart, the wish
    /// list and the checkout - comes from the one definition of "sellable" and therefore counts a
    /// unit that a cancelled order gave back (see <see cref="SkuAvailability"/>).
    /// </summary>
    private async Task AddStockAsync(IReadOnlyCollection<ProductDto> products)
    {
        var stock = await SkuAvailability.CountSellableByProductAsync(_context, products.Select(p => p.ProductID));

        foreach (var product in products)
        {
            var availableQty = stock[product.ProductID];

            product.AvailableQty = availableQty;
            product.IsInStock = availableQty > 0;
        }
    }

    /// <summary>
    /// Projection used by all product queries. It is an <see cref="Expression{TDelegate}"/> so
    /// EF Core can translate it into SQL. A plain C# method would be evaluated client-side after
    /// materialisation, where the (never Included) PurchaseDetails collection is empty, which made
    /// every product report "Out of Stock". The stock fields are filled in by
    /// <see cref="AddStockAsync"/> for the same reason.
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
