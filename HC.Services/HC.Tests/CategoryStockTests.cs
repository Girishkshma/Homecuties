using HC.Business;
using HC.Business.Dtos;
using Xunit;

namespace HC.Tests;

/// <summary>
/// The dashboard's 'Stock by category' cards: what each heading of the catalogue can still sell, and what under it
/// has none left or nearly none.
///
/// These figures are what the shop orders stock against, so the choices behind them are pinned here rather than left
/// to a screen: a card answers 'what does this shelf need' and not 'how many units does the shop hold'
/// (see <see cref="TheRowsOfACardDoNotAddUpToItsHeading"/>), the units are the sellable ones the storefront counts
/// and never a figure invented here, and the two marks an order is decided from are the same two on every card
/// (see <see cref="OutOfStockLowAndEnoughAreThreeDifferentProducts"/>).
/// </summary>
public class CategoryStockTests
{
    /// <summary>
    /// A heading is everything filed under it, each product counted once - and a sub-category's figures are
    /// everything under itself as well, so the shop can read a card from the heading down.
    /// </summary>
    [Fact]
    public void AHeadingHoldsEverythingFiledBeneathItCountedOnce()
    {
        var stock = CategoryStock.ByHeading(
            new[]
            {
                Category(1, "Toys", null),
                Category(14, "For Kids", 1),
                Category(15, "Learning", 1),
                Category(17, "Plush", 14)
            },
            new[]
            {
                Product(101, 5, 14, 17),
                Product(102, 0, 17),
                Product(103, 2, 15)
            });

        var heading = Assert.Single(stock.Headings);

        Assert.Equal("Toys", heading.Heading.CategoryName);
        Assert.Equal(new CategoryStockCounts(3, 7, 1, 1), heading.Counts);

        // Depth first, each category before the ones under it and siblings in the table's own order: 'Plush' sits
        // inside 'For Kids', so it steps in under it rather than after 'Learning'.
        Assert.Equal(new short[] { 14, 17, 15 }, heading.Lines.Select(line => line.Category.CategoryId));
        Assert.Equal(new[] { 0, 1, 0 }, heading.Lines.Select(line => line.Depth));

        Assert.Equal(new CategoryStockCounts(2, 5, 1, 0), Line(heading, 14));
        Assert.Equal(new CategoryStockCounts(2, 5, 1, 0), Line(heading, 17));
        Assert.Equal(new CategoryStockCounts(1, 2, 0, 1), Line(heading, 15));

        // Between them the cards count every product the shop filed somewhere it can show.
        Assert.Equal(new[] { 101, 102, 103 }, stock.ProductsCounted.OrderBy(productId => productId));
    }

    /// <summary>
    /// The rows of a card do not add up to its heading, and that is the point of the card: a product filed under two
    /// sub-categories is stock under both - which is what a shelf needs - while the heading counts it once, which is
    /// what the shop holds. Adding the rows up would report stock the shop does not have, and reporting the heading
    /// alone would hide which shelf to look at.
    /// </summary>
    [Fact]
    public void TheRowsOfACardDoNotAddUpToItsHeading()
    {
        var heading = Assert.Single(CategoryStock.ByHeading(
            new[]
            {
                Category(1, "Toys", null),
                Category(14, "For Kids", 1),
                Category(17, "Plush", 14)
            },
            new[]
            {
                // The same toy is filed under the child and under the sub-category of the child: two shelves, one
                // product, and the shop holds it once.
                Product(101, 5, 14, 17)
            }).Headings);

        Assert.Equal(1, heading.Counts.ProductCount);
        Assert.Equal(5, heading.Counts.UnitsInStock);

        Assert.All(heading.Lines, line =>
        {
            Assert.Equal(1, line.Counts.ProductCount);
            Assert.Equal(5, line.Counts.UnitsInStock);
        });

        Assert.Equal(2, heading.Lines.Sum(line => line.Counts.ProductCount));
        Assert.Equal(10, heading.Lines.Sum(line => line.Counts.UnitsInStock));
    }

    /// <summary>
    /// The products of older days - filed right on a heading, before the form stopped offering one as a place to file
    /// a product - are in the heading's figures and under none of its rows, so the card says how many they are rather
    /// than leaving the figures unexplained.
    /// </summary>
    [Fact]
    public void AProductFiledOnTheHeadingItselfIsInTheFiguresAndInNoRow()
    {
        var heading = Assert.Single(CategoryStock.ByHeading(
            new[]
            {
                Category(2, "Decoratives", null),
                Category(6, "Vases", 2)
            },
            new[]
            {
                Product(201, 4, 2),
                Product(202, 1, 6)
            }).Headings);

        Assert.Equal(new CategoryStockCounts(2, 5, 0, 1), heading.Counts);
        Assert.Equal(1, heading.ProductsOnTheHeading);

        var vases = Assert.Single(heading.Lines);
        Assert.Equal(new CategoryStockCounts(1, 1, 0, 1), vases.Counts);
    }

    /// <summary>
    /// Out of stock and low are three different products, not two: none left is a fact the catalogue can be ordered
    /// against at once, a handful left is the shop's own mark (<see cref="CategoryStock.LowStockUnits"/>), and one
    /// above that is neither. A product with none left is not also counted as low - it is the worse of the two.
    /// </summary>
    [Fact]
    public void OutOfStockLowAndEnoughAreThreeDifferentProducts()
    {
        var heading = Assert.Single(CategoryStock.ByHeading(
            new[]
            {
                Category(1, "Toys", null),
                Category(14, "For Kids", 1)
            },
            new[]
            {
                Product(101, 0, 14),
                Product(102, CategoryStock.LowStockUnits, 14),
                Product(103, CategoryStock.LowStockUnits + 1, 14)
            }).Headings);

        var expected = new CategoryStockCounts(3, CategoryStock.LowStockUnits * 2 + 1, 1, 1);

        Assert.Equal(expected, heading.Counts);
        Assert.Equal(expected, Assert.Single(heading.Lines).Counts);
    }

    /// <summary>
    /// Stock the shop holds that no card can show is counted by no card, and the answer says so instead of leaving it
    /// out silently: a product filed under no category, and one filed under categories caught in a loop in the table
    /// (no heading of the tree leads into either of them). The caller turns the products left out into a sentence,
    /// which is why the answer names the ones it counted and not only the ones it did not.
    /// </summary>
    [Fact]
    public void AProductFiledWhereNoHeadingLeadsIsCountedNowhere()
    {
        var stock = CategoryStock.ByHeading(
            new[]
            {
                Category(1, "Toys", null),
                Category(14, "For Kids", 1),

                // A pair that each names the other as its parent: no heading leads into them, so the two of them are
                // a broken catalogue rather than a shelf that has run out.
                Category(50, "Oddities", 51),
                Category(51, "Odder", 50)
            },
            new[]
            {
                Product(101, 2, 14),
                Product(102, 6, 50),
                Product(103, 9)
            });

        var heading = Assert.Single(stock.Headings);
        Assert.Equal("Toys", heading.Heading.CategoryName);
        Assert.Equal(new CategoryStockCounts(1, 2, 0, 1), heading.Counts);

        Assert.Equal(new[] { 101 }, stock.ProductsCounted);
        Assert.DoesNotContain(102, stock.ProductsCounted);
        Assert.DoesNotContain(103, stock.ProductsCounted);
    }

    /// <summary>
    /// A catalogue with nothing to count still answers: a heading with nothing under it is a card with nothing on it,
    /// and a list with no headings is no cards at all - the screen then says there is nothing there rather than
    /// showing a card of zeroes that reads like stock that has all run out.
    /// </summary>
    [Fact]
    public void AHeadingWithNothingUnderItIsACardWithNothingOnIt()
    {
        var stock = CategoryStock.ByHeading(
            new[]
            {
                Category(2, "Decoratives", null),
                Category(5, "Furnitures", null),
                Category(13, "Center Tables", 5)
            },
            Array.Empty<CategoryStockProduct>());

        Assert.Equal(new short[] { 2, 5 }, stock.Headings.Select(heading => heading.Heading.CategoryId));

        Assert.All(stock.Headings, heading =>
        {
            Assert.Equal(new CategoryStockCounts(0, 0, 0, 0), heading.Counts);
            Assert.Equal(0, heading.ProductsOnTheHeading);
        });

        Assert.Empty(stock.ProductsCounted);
        Assert.Empty(CategoryStock.ByHeading(Array.Empty<AdminCategoryDto>(), Array.Empty<CategoryStockProduct>()).Headings);
    }

    private static CategoryStockCounts Line(HeadingStock heading, short categoryId) =>
        Assert.Single(heading.Lines.Where(line => line.Category.CategoryId == categoryId)).Counts;

    private static AdminCategoryDto Category(short categoryId, string categoryName, short? parentCategoryId) => new()
    {
        CategoryId = categoryId,
        CategoryName = categoryName,
        ParentCategoryId = parentCategoryId
    };

    /// <summary>
    /// A product as the service hands it in: what it can still sell, and the categories it is filed under. A product
    /// with no category at all is the shop's 'nobody filed this' case, which the card counts nowhere.
    /// </summary>
    private static CategoryStockProduct Product(int productId, int sellableUnits, params short[] categoryIds) =>
        new(productId, sellableUnits, categoryIds);
}
