// ============================================================
// AdminDashboardService.StockByCategory.cs
// Partial class: AdminDashboardService - Stock by category
// ============================================================

using HC.Business.Dtos;
using HC.Data;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

public partial class AdminDashboardService : IAdminDashboardService
{
    /// <summary>ProductStatuses.ProductStatusID = 2 => "Suspended" (disabled): the storefront shows it to nobody.</summary>
    private const short SuspendedProductStatusId = 2;

    /// <summary>
    /// The dashboard's 'Stock by category' cards: the shop's stock cut by the catalogue's own headings, so the
    /// business can see which category needs restocking rather than which single product does - one card per
    /// heading, with the categories beneath it and what each of them can still sell.
    ///
    /// This method reads and words; the counting itself is in <see cref="CategoryStock"/>, where the three rules
    /// behind every figure live (what a heading's figures hold, what makes a product out of stock or low, and that
    /// only what the storefront can sell is counted at all). The reads are:
    /// <list type="bullet">
    /// <item>the categories, once, flat - the tree is built from them the same way the product form builds it
    /// (see <see cref="CategoryTree.GroupedByHeading"/>);</item>
    /// <item>every product with its status, and the active category links of the ones that are not suspended;</item>
    /// <item>the sellable units of every product, counted in the database by the shop's one sellable rule
    /// (see <see cref="SkuAvailability.CountSellableByProductAsync"/>).</item>
    /// </list>
    ///
    /// What the cards cannot show is said in <see cref="AdminStockByCategoryDto.Messages"/> instead of being left
    /// out silently: stock filed under no category a card can show (so the storefront lists it nowhere), and the
    /// units of products the shop has suspended.
    /// </summary>
    public async Task<AdminStockByCategoryDto> GetStockByCategoryAsync()
    {
        var categories = await _context.Categories
            .Select(c => new AdminCategoryDto
            {
                CategoryId = c.CategoryId,
                CategoryName = c.CategoryName,
                ParentCategoryId = c.ParentCategoryId
            })
            .ToListAsync();

        // Every product with the status it is in. The status decides whether the product is stock at all, and the
        // suspended ones are still asked for their units below - not to count them, but so the answer can say what
        // the shop holds off sale rather than pass over it.
        var products = await _context.Products
            .Select(p => new { p.ProductId, p.ProductStatusId })
            .ToListAsync();

        // The units of every product, by the shop's one sellable rule: a unit in the 'Available' pool that no live
        // order holds - which is what puts a cancelled order's unit back on the shelf (see SkuAvailability).
        var sellableUnits = await SkuAvailability.CountSellableByProductAsync(
            _context, products.Select(product => product.ProductId));

        // Where the products the storefront can sell are filed: a link the shop has switched off is a category the
        // product is out of, and a suspended product is shown to nobody at all - the same two rules the storefront's
        // own queries apply (see ProductService), so a card can never hold stock a customer cannot buy.
        var filedUnder = (await _context.ProductCategories
                .Where(pc => pc.IsActive && pc.Product.ProductStatusId != SuspendedProductStatusId)
                .Select(pc => new { pc.ProductId, pc.CategoryId })
                .ToListAsync())
            .GroupBy(link => link.ProductId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<short>)group.Select(link => link.CategoryId).ToList());

        var counted = new List<CategoryStockProduct>(products.Count);
        foreach (var product in products)
        {
            if (product.ProductStatusId == SuspendedProductStatusId)
            {
                continue;
            }

            counted.Add(new CategoryStockProduct(
                product.ProductId,
                sellableUnits[product.ProductId],
                filedUnder.TryGetValue(product.ProductId, out var filedIn)
                    ? filedIn
                    : Array.Empty<short>()));
        }

        var stock = CategoryStock.ByHeading(categories, counted);

        var answer = new AdminStockByCategoryDto
        {
            LowStockUnits = CategoryStock.LowStockUnits,
            Headings = stock.Headings.Select(heading => new AdminCategoryStockDto
            {
                CategoryId = heading.Heading.CategoryId,
                CategoryName = heading.Heading.CategoryName,
                ProductCount = heading.Counts.ProductCount,
                UnitsInStock = heading.Counts.UnitsInStock,
                OutOfStockProducts = heading.Counts.OutOfStockProducts,
                LowStockProducts = heading.Counts.LowStockProducts,
                ProductsOnTheHeading = heading.ProductsOnTheHeading,
                SubCategories = heading.Lines.Select(line => new AdminCategoryStockLineDto
                {
                    CategoryId = line.Category.CategoryId,
                    CategoryName = line.Category.CategoryName,
                    Depth = line.Depth,
                    ProductCount = line.Counts.ProductCount,
                    UnitsInStock = line.Counts.UnitsInStock,
                    OutOfStockProducts = line.Counts.OutOfStockProducts,
                    LowStockProducts = line.Counts.LowStockProducts
                }).ToList()
            }).ToList()
        };

        // What the cards cannot show by themselves, so that nothing is missing from them without being said.
        //
        // Stock the shop holds that no heading's figures reach: a product filed under no category at all, or under
        // only categories caught in a loop in the table - the one case where the catalogue itself has to be fixed
        // rather than restocked, and the reason the answer says which products were counted (see
        // CategoryStock.ByHeading).
        var offTheCards = counted
            .Where(product => !stock.ProductsCounted.Contains(product.ProductId))
            .ToList();

        if (offTheCards.Count > 0)
        {
            answer.Messages = answer.Messages.Append(
                $"Filed under no category a card can show: {Products(offTheCards.Count)} holding " +
                $"{Units(offTheCards.Sum(product => product.SellableUnits))}. The storefront lists such stock " +
                "nowhere, and no heading's figures hold it - file it under a category to sell it.").ToArray();
        }

        // The units of products the shop has suspended: real stock, off sale, and in none of the figures above.
        var suspended = products
            .Where(product => product.ProductStatusId == SuspendedProductStatusId)
            .ToList();

        var suspendedUnits = suspended.Sum(product => sellableUnits[product.ProductId]);
        if (suspendedUnits > 0)
        {
            answer.Messages = answer.Messages.Append(
                $"Suspended by the shop: {Products(suspended.Count)} still holding {Units(suspendedUnits)} - stock " +
                "nobody can buy, and no card counts it. Put them back on sale to sell those units.").ToArray();
        }

        return answer;
    }

    /// <summary>'1 product' / '3 products' - so a sentence about one of them reads as one.</summary>
    private static string Products(int count) => count == 1 ? "1 product" : $"{count} products";

    /// <summary>'1 unit' / '7 units', for the same reason.</summary>
    private static string Units(int units) => units == 1 ? "1 unit" : $"{units} units";
}
