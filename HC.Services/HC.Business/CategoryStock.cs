using HC.Business.Dtos;

namespace HC.Business;

/// <summary>
/// The shop's stock, counted under the catalogue's own headings: the figures behind the dashboard's
/// 'Stock by category' cards (see AdminDashboardService.StockByCategory.cs, which does the reads and nothing else).
///
/// Three rules live here, and here only, so the cards can never answer the question two different ways:
/// <list type="number">
/// <item>A card's figures are everything filed under its heading, including under the categories beneath it - not
/// just what is filed on the heading itself. That is what the shop's question is ('how much of this shelf can we
/// still sell'), and it is why a heading's units are not the sum of its rows: a product filed under two
/// sub-categories is stock under both, which is the right answer to 'what does this shelf need' and the wrong answer
/// to 'how many units does the shop hold'.</item>
/// <item>A product with no sellable unit left is out of stock, one with <see cref="LowStockUnits"/> or fewer is low,
/// and anything above that is neither. The units themselves are counted by the one sellable rule the shop has
/// (see <see cref="SkuAvailability"/>) and handed in here, so this never invents a stock figure of its own.</item>
/// <item>Only what the caller hands in is counted, and the caller hands in what the storefront can sell: a suspended
/// product is shown to nobody, and a category link the shop has switched off is a category the product is out of -
/// both of them the storefront's own two rules (see ProductService), applied before this is asked.</item>
/// </list>
///
/// Nothing here touches a database: a product arrives as <see cref="CategoryStockProduct"/> - what it can still sell
/// and where it is filed - so the counting is pinned down by tests the way the order rules are.
/// </summary>
public static class CategoryStock
{
    /// <summary>
    /// How few sellable units a product has left for a card to count it as 'low': few enough that the next order of
    /// it is worth thinking about. The screen is told the mark back (<c>AdminStockByCategoryDto.LowStockUnits</c>) and
    /// words it into the 'low' flags, so the counting and the wording can never drift apart.
    ///
    /// It is a choice of the shop's own and not a figure the catalogue carries: nothing in the tables records a
    /// reorder level, so what is a fact is a product with none left ('out of stock', counted on its own) and what is
    /// this choice is a product with a handful left ('low'). Change it here and every card changes with it.
    /// </summary>
    public const int LowStockUnits = 3;

    /// <summary>
    /// What the shop holds under each heading of the catalogue: one entry per heading, in the tree's own order
    /// (see <see cref="CategoryTree.GroupedByHeading"/>), each with its own categories beneath it.
    ///
    /// The answer also says which products the cards count between them, because they do not count them all: a
    /// product filed under no category, or under categories no heading leads into, is stock the shop holds and no
    /// heading's figures show it. The caller says so in a sentence rather than leaving it out (see
    /// <c>AdminDashboardService.GetStockByCategoryAsync</c>).
    /// </summary>
    public static StockByHeading ByHeading(
        IEnumerable<AdminCategoryDto> categories,
        IEnumerable<CategoryStockProduct> products)
    {
        // Which products are filed under each category, and what each product can still sell. One pass: a product
        // filed under several categories is filed under each of them.
        var filedUnder = new Dictionary<short, List<int>>();
        var sellableUnits = new Dictionary<int, int>();
        var headings = new List<HeadingStock>();
        var counted = new HashSet<int>();

        foreach (var product in products)
        {
            sellableUnits[product.ProductId] = product.SellableUnits;

            foreach (var categoryId in product.CategoryIds.Distinct())
            {
                if (!filedUnder.TryGetValue(categoryId, out var filed))
                {
                    filed = new List<int>();
                    filedUnder[categoryId] = filed;
                }

                filed.Add(product.ProductId);
            }
        }

        foreach (var group in CategoryTree.GroupedByHeading(categories))
        {
            // The heading and everything beneath it, the heading first - a depth-first walk, so a category always
            // comes after the categories under it and folding the sets upwards is one pass in reverse.
            var nodes = new List<AdminCategoryDto> { group.Heading };
            nodes.AddRange(group.Beneath.Select(row => row.Category));

            var held = new Dictionary<short, HashSet<int>>();
            foreach (var node in nodes)
            {
                held[node.CategoryId] = filedUnder.TryGetValue(node.CategoryId, out var filed)
                    ? new HashSet<int>(filed)
                    : new HashSet<int>();
            }

            // Each category hands what it holds up to the one above it: the heading is left with everything under
            // it, and a sub-category with everything under itself as well - which is what makes a row's figures the
            // whole of what that shelf holds rather than what happens to be filed right on it.
            for (var index = nodes.Count - 1; index >= 1; index--)
            {
                var node = nodes[index];
                if (node.ParentCategoryId is { } parentId && held.TryGetValue(parentId, out var above))
                {
                    above.UnionWith(held[node.CategoryId]);
                }
            }

            var counts = nodes.ToDictionary(node => node.CategoryId, node => Count(held[node.CategoryId], sellableUnits));
            var heading = group.Heading;

            headings.Add(new HeadingStock(
                heading,
                counts[heading.CategoryId],
                filedUnder.TryGetValue(heading.CategoryId, out var onTheHeading) ? onTheHeading.Count : 0,
                group.Beneath
                    .Select(row => new HeadingStockLine(row.Category, row.Depth, counts[row.Category.CategoryId]))
                    .ToList()));

            counted.UnionWith(held[heading.CategoryId]);
        }

        return new StockByHeading(headings, counted);
    }

    /// <summary>
    /// What a set of products comes to: how many of them there are, the units they can still sell, and how many of
    /// them are out of stock or close to it (see the two marks in the class summary).
    /// </summary>
    private static CategoryStockCounts Count(IEnumerable<int> productIds, IReadOnlyDictionary<int, int> sellableUnits)
    {
        var products = 0;
        var units = 0;
        var outOfStock = 0;
        var low = 0;

        foreach (var productId in productIds)
        {
            var sellable = sellableUnits.GetValueOrDefault(productId);

            products++;
            units += sellable;

            if (sellable == 0)
            {
                outOfStock++;
            }
            else if (sellable <= LowStockUnits)
            {
                low++;
            }
        }

        return new CategoryStockCounts(products, units, outOfStock, low);
    }
}

/// <summary>
/// One product as the cards count it: what it can still sell, and the categories it is filed under (its active links
/// only - a link the shop has switched off is a category the product is not in).
///
/// It is a record of the service's reads rather than something read here, which is what keeps the counting clear of a
/// database: the service queries, this class works the figures out.
/// </summary>
public sealed record CategoryStockProduct(int ProductId, int SellableUnits, IReadOnlyCollection<short> CategoryIds);

/// <summary>
/// What a set of products comes to on a card or a row: how many products the shop has to look at, the units they can
/// still sell, and how many of them have none left or nearly none.
/// </summary>
public sealed record CategoryStockCounts(int ProductCount, int UnitsInStock, int OutOfStockProducts, int LowStockProducts);

/// <summary>
/// A heading of the catalogue as a card: the heading, the categories beneath it, and what each of them comes to.
///
/// The heading's own figures are everything filed under it, including under the categories beneath it - see the class
/// summary for why that is the question a card is asked. The products filed right on the heading are in those figures
/// and in none of the rows, so a card can say how many they are.
/// </summary>
public sealed record HeadingStock(
    AdminCategoryDto Heading,
    CategoryStockCounts Counts,
    int ProductsOnTheHeading,
    IReadOnlyList<HeadingStockLine> Lines);

/// <summary>
/// A category beneath a heading, how deep it sits beneath it (see <see cref="HeadingRow.Depth"/>) and what it and
/// everything under it comes to.
/// </summary>
public sealed record HeadingStockLine(AdminCategoryDto Category, int Depth, CategoryStockCounts Counts);

/// <summary>
/// The cards, and which products they count between them: a product filed under no category, or under categories no
/// heading reaches (a loop in the table), is in no card at all.
/// </summary>
public sealed record StockByHeading(IReadOnlyList<HeadingStock> Headings, IReadOnlySet<int> ProductsCounted);
