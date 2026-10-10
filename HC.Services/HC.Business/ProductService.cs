using System.Linq.Expressions;
using HC.Business.Dtos;
using HC.Data;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

public class ProductService : IProductService
{
    private readonly HomecutiesDbContext _context;
    private readonly IProductImageService _productImages;

    public ProductService(HomecutiesDbContext context, IProductImageService productImages)
    {
        _context = context;
        _productImages = productImages;
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

        AddListingPrices(products);
        await AddStockAsync(products);
        ResolveImages(products);
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

        AddListingPrices(products);
        await AddStockAsync(products);
        ResolveImages(products);
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

        AddListingPrices(new[] { product });
        await AddStockAsync(new[] { product });
        ResolveImages(new[] { product });
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

        AddListingPrices(products);
        await AddStockAsync(products);
        ResolveImages(products);
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
    /// Works out what one unit of each product costs the customer and writes it into
    /// <see cref="ProductDto.ListingPrice"/> and the figures it is made of.
    ///
    /// Every price on a listing is composed from the product form's own two sections - 'Pricing &amp; Charges'
    /// (the unit price, both discounts, the packaging / storage / delivery charges and the profit margin) and
    /// 'Taxes' (CGST, SGST, IGST) - by the one rule (<see cref="ProductPricing"/>), so a card, the cart and the
    /// bill cannot describe the same product differently.
    ///
    /// It is a pass over the read rows rather than part of <see cref="ProductProjection"/> for the same reason the
    /// stock is: the projection is translated into SQL and the rule is C#, and putting it in only one of the two
    /// would leave the other free to drift.
    /// </summary>
    private static void AddListingPrices(IReadOnlyCollection<ProductDto> products)
    {
        foreach (var product in products)
        {
            var price = new ProductPricing.Inputs(
                product.UnitPrice,
                product.DiscountPercent,
                product.AdditionalDiscountPercent,
                product.ProfitMarginPercent,
                product.PackagingCharge,
                product.StorageCharge,
                product.DeliveryCharge,
                product.CGSTPercent,
                product.SGSTPercent,
                product.IGSTPercent);

            product.MarginAmount = ProductPricing.Margin(price);
            product.ChargesAmount = ProductPricing.Charges(price);
            product.TaxableValue = ProductPricing.TaxableValue(price);
            product.GstRatePercent = ProductPricing.GstRate(price);
            product.GstAmount = ProductPricing.GstAmount(price);
            product.ListingPrice = ProductPricing.ListingPrice(price);
            product.PreDiscountListingPrice = ProductPricing.PreDiscountListingPrice(price);
            product.PostDiscountListingPrice = ProductPricing.PostDiscountListingPrice(price);
        }
    }

    /// <summary>
    /// Works out what a product's photos are drawn from - the file a big frame takes and the file the strip takes -
    /// from its stored rows and the shop's image folder, which the projection cannot do: whether a row's file is
    /// really there is the file system's answer, not SQL's (see <see cref="ProductGallery.Resolve"/>).
    ///
    /// It is the reason a product page no longer draws the rows as they stand. The shop's older products have rows for
    /// sizes that were never produced, and drawing those rows is what put broken thumbnails in the strip and blank
    /// frames under the main image's arrows.
    /// </summary>
    private void ResolveImages(IReadOnlyCollection<ProductDto> products)
    {
        foreach (var product in products)
            product.Pictures = ProductGallery.Resolve(product.ProductImages, _productImages.Exists);
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
            .Select(pi => new ProductImageRefDto
            {
                ImageTypeId = pi.ImageTypeId,
                ImageIndex = pi.ImageIndex,
                ImageUrl = pi.ImageUrl
            })
            .ToList(),
        // What the price a customer pays is made up of, straight off the product: the pass below turns these into
        // the listing price with the one rule (ProductPricing), because that rule is C# and not something SQL is
        // asked to repeat.
        UnitPrice = p.UnitPrice,
        ProfitMarginPercent = p.ProfitMarginPercent,
        PackagingCharge = p.PackagingCharge,
        StorageCharge = p.StorageCharge,
        DeliveryCharge = p.DeliveryCharge,
        // What the two discounts do to the goods alone: the unit price, and what each of them leaves of it - the
        // additional discount coming off what the discount left, the way both of them come off the price itself
        // (<see cref="ProductPricing"/>). These are the figures a screen speaks about the discount with; what a
        // customer actually pays is ListingPrice, filled in below.
        SalesPrice = p.UnitPrice,
        PreDiscountSalesPrice = p.UnitPrice,
        PostDiscountSalesPrice = p.UnitPrice - (p.UnitPrice * p.DiscountPercent / 100),
        PostAdditionalDiscountSalesPrice =
            (p.UnitPrice - (p.UnitPrice * p.DiscountPercent / 100)) * (1m - p.AdditionalDiscountPercent / 100),
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
