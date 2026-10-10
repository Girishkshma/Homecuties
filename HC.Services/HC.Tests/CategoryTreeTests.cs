using HC.Business;
using HC.Business.Dtos;
using Xunit;

namespace HC.Tests;

/// <summary>
/// The order the admin screens list the categories in, and which of them are headings.
///
/// The shop's own table is what this exists for: its rows are numbered as they were created, so 'For Kids' - a child
/// of 'Toys' (id 1) - is row 14, and the product form listed it after 'Decoratives / Vases' and eleven other
/// categories that have nothing to do with it (see <see cref="TheShopsOwnRowsAreListedWithEachSubCategoryUnderItsParent"/>).
/// A category an admin has to hunt for among the wrong ones is a category a product gets filed under wrongly.
///
/// The tops of that order are the shop's headings (<see cref="CategoryTree.TopsOfTree"/>), and a heading is not a place
/// a product sits: the product form gives it no tick, and the API refuses a product that names one.
/// </summary>
public class CategoryTreeTests
{
    /// <summary>
    /// The shop's own 16 rows, in the order the table answers them: five tops and the sub-category each one holds. The
    /// walk lists every top with its own beneath it, in the table's order, so 'For Kids' follows 'Toys' at once rather
    /// than sitting at row 14 - and the tops stay in the order the shop put them in.
    /// </summary>
    [Fact]
    public void TheShopsOwnRowsAreListedWithEachSubCategoryUnderItsParent()
    {
        var ordered = CategoryTree.InDisplayOrder(new[]
        {
            Category(1, "Toys", null),
            Category(2, "Decoratives", null),
            Category(3, "Office/Study", null),
            Category(4, "Households", null),
            Category(5, "Furnitures", null),
            Category(6, "Vases", 2),
            Category(7, "Pot Houses", 2),
            Category(8, "Musicians", 2),
            Category(9, "Show Pieces", 2),
            Category(10, "Pen Stands", 3),
            Category(11, "Calendars", 3),
            Category(12, "Coasters", 4),
            Category(13, "Center Tables", 5),
            Category(14, "For Kids", 1),
            Category(15, "Mobile Stands", 3),
            Category(16, "Utilities", 4)
        });

        Assert.Equal(
            new short[] { 1, 14, 2, 6, 7, 8, 9, 3, 10, 11, 15, 4, 12, 16, 5, 13 },
            ordered.Select(category => category.CategoryId));
    }

    /// <summary>
    /// A parent the table listed below its own child is still read first: the walk starts at the tops of the tree, not
    /// at the first row it is handed.
    /// </summary>
    [Fact]
    public void AParentIsListedBeforeAChildTheTableListedFirst()
    {
        var ordered = CategoryTree.InDisplayOrder(new[]
        {
            Category(7, "Pot Houses", 2),
            Category(2, "Decoratives", null)
        });

        Assert.Equal(new short[] { 2, 7 }, ordered.Select(category => category.CategoryId));
    }

    /// <summary>
    /// The walk goes all the way down before it goes on: the table records no depth of its own, only the parent each
    /// row names, so a sub-category of a sub-category follows its own parent and not the next top of the tree.
    /// </summary>
    [Fact]
    public void TheWalkGoesAllTheWayDownBeforeItGoesOn()
    {
        var ordered = CategoryTree.InDisplayOrder(new[]
        {
            Category(2, "Decoratives", null),
            Category(5, "Furnitures", null),
            Category(6, "Vases", 2),
            Category(20, "Glass Vases", 6)
        });

        Assert.Equal(new short[] { 2, 6, 20, 5 }, ordered.Select(category => category.CategoryId));
    }

    /// <summary>
    /// A category whose parent row has been deleted is still listed, in the place a top of the tree is read: there is
    /// nothing left in the list to file it under, and leaving it out would hide a category from the only screen that
    /// can put it right.
    /// </summary>
    [Fact]
    public void ACategoryWhoseParentIsGoneIsStillListed()
    {
        var ordered = CategoryTree.InDisplayOrder(new[]
        {
            Category(2, "Decoratives", null),
            Category(6, "Vases", 99),
            Category(7, "Pot Houses", 2)
        });

        Assert.Equal(new short[] { 2, 7, 6 }, ordered.Select(category => category.CategoryId));
    }

    /// <summary>
    /// Two rows each naming the other as parent are a loop the walk cannot enter from any top of the tree. Both are
    /// still listed - at the end, in the table's order - and the walk ends, which is what keeps a broken pair from
    /// emptying the screen.
    /// </summary>
    [Fact]
    public void ALoopInTheTableIsListedOnceAndTheWalkEnds()
    {
        var ordered = CategoryTree.InDisplayOrder(new[]
        {
            Category(1, "Toys", null),
            Category(2, "Decoratives", 3),
            Category(3, "Vases", 2)
        });

        Assert.Equal(new short[] { 1, 2, 3 }, ordered.Select(category => category.CategoryId));
    }

    /// <summary>
    /// Whatever shape the table is in - nesting, a deleted parent and a loop together - every category is listed once
    /// and none is listed twice: the form draws one checkbox per row it is given.
    /// </summary>
    [Fact]
    public void EveryCategoryIsListedExactlyOnce()
    {
        var ordered = CategoryTree.InDisplayOrder(new[]
        {
            Category(1, "Toys", null),
            Category(2, "Decoratives", null),
            Category(6, "Vases", 2),
            Category(14, "For Kids", 1),
            Category(15, "Mobile Stands", 99),
            Category(30, "Loop One", 31),
            Category(31, "Loop Two", 30)
        });

        Assert.Equal(7, ordered.Count);
        Assert.Equal(ordered.Count, ordered.Select(category => category.CategoryId).Distinct().Count());

        // The nesting and the tops of the tree come first, and the loop is left to the end.
        Assert.Equal(new short[] { 1, 14, 2, 6, 15, 30, 31 }, ordered.Select(category => category.CategoryId));
    }

    /// <summary>
    /// The shop's five headings are the five top-level categories, in the table's order: a product is filed under one
    /// of the categories beneath them ('Vases', 'For Kids' and the rest) rather than under the heading itself.
    /// </summary>
    [Fact]
    public void TheShopsOwnHeadingsAreTheCategoriesWithNothingUnderThem()
    {
        var headings = CategoryTree.TopsOfTree(new[]
        {
            Category(1, "Toys", null),
            Category(2, "Decoratives", null),
            Category(3, "Office/Study", null),
            Category(4, "Households", null),
            Category(5, "Furnitures", null),
            Category(6, "Vases", 2),
            Category(7, "Pot Houses", 2),
            Category(10, "Pen Stands", 3),
            Category(13, "Center Tables", 5),
            Category(14, "For Kids", 1),
            Category(15, "Mobile Stands", 3),
            Category(16, "Utilities", 4)
        });

        Assert.Equal(
            new short[] { 1, 2, 3, 4, 5 },
            headings.Select(category => category.CategoryId));

        // Every sub-category of the shop is a place a product can be filed, so none of them is a heading.
        Assert.DoesNotContain(headings, category => category.ParentCategoryId is not null);
    }

    /// <summary>
    /// A category whose parent row is gone is a heading too, because there is nothing left in the list to file a product
    /// under: 'TopsOfTree' reads the table the same way the walk does, so the screen cannot offer a tick the API would
    /// refuse.
    /// </summary>
    [Fact]
    public void ACategoryWhoseParentIsGoneIsAHeadingToo()
    {
        var headings = CategoryTree.TopsOfTree(new[]
        {
            Category(2, "Decoratives", null),
            Category(6, "Vases", 99)
        });

        Assert.Equal(new short[] { 2, 6 }, headings.Select(category => category.CategoryId));
    }

    /// <summary>
    /// A heading with categories under it is the only thing that drops out: a product can be filed under a category
    /// deepest in the tree as readily as under the one below a heading, and an empty list of categories has no headings
    /// to give a tick to at all.
    /// </summary>
    [Fact]
    public void EveryCategoryBeneathAHeadingIsAPlaceAProductCanBeFiled()
    {
        var headings = CategoryTree.TopsOfTree(new[]
        {
            Category(2, "Decoratives", null),
            Category(6, "Vases", 2),
            Category(20, "Glass Vases", 6)
        });

        Assert.Equal(new short[] { 2 }, headings.Select(category => category.CategoryId));
        Assert.Empty(CategoryTree.TopsOfTree(Array.Empty<AdminCategoryDto>()));
    }

    /// <summary>
    /// How deep each row sits, which is what the admin's Categories screen steps it in by: a heading at 0 and its own
    /// shelf at 1, so the screen can draw the catalogue without working the tree out a second time. On the shop's own
    /// rows that is 'Toys' 0 with 'For Kids' 1, 'Decoratives' 0 with its four shelves at 1, and so on down.
    /// </summary>
    [Fact]
    public void EachRowIsAnsweredWithHowDeepItSits()
    {
        var rows = CategoryTree.RowsInDisplayOrder(new[]
        {
            Category(1, "Toys", null),
            Category(2, "Decoratives", null),
            Category(3, "Office/Study", null),
            Category(4, "Households", null),
            Category(5, "Furnitures", null),
            Category(6, "Vases", 2),
            Category(7, "Pot Houses", 2),
            Category(8, "Musicians", 2),
            Category(9, "Show Pieces", 2),
            Category(10, "Pen Stands", 3),
            Category(11, "Calendars", 3),
            Category(12, "Coasters", 4),
            Category(13, "Center Tables", 5),
            Category(14, "For Kids", 1),
            Category(15, "Mobile Stands", 3),
            Category(16, "Utilities", 4)
        });

        // The same order InDisplayOrder answers with: the list only says how deep each row of it sits.
        Assert.Equal(new short[] { 1, 14, 2, 6, 7, 8, 9, 3, 10, 11, 15, 4, 12, 16, 5, 13 },
            rows.Select(row => row.Category.CategoryId));

        Assert.Equal(new[] { 0, 1, 0, 1, 1, 1, 1, 0, 1, 1, 1, 0, 1, 1, 0, 1 },
            rows.Select(row => row.Depth));
    }

    /// <summary>
    /// The depth is counted from the top of the tree the row belongs to, however deep the shop has filed it: a shelf of
    /// a shelf is 2, and a shelf of that 3. Nothing caps it, because the storefront reads a heading's whole branch at
    /// any depth (see CategoryBranch.Beneath) - so the admin's list has to be able to step in as far as the catalogue
    /// goes rather than flatten what it cannot draw.
    /// </summary>
    [Fact]
    public void TheDepthIsCountedFromTheTopOfTheRowsOwnTree()
    {
        var rows = CategoryTree.RowsInDisplayOrder(new[]
        {
            Category(2, "Decoratives", null),
            Category(6, "Vases", 2),
            Category(20, "Glass Vases", 6),
            Category(21, "Stemmed Vases", 20),
            Category(99, "Orphan", 98)
        });

        Assert.Equal(new short[] { 2, 6, 20, 21, 99 }, rows.Select(row => row.Category.CategoryId));
        Assert.Equal(new[] { 0, 1, 2, 3, 0 }, rows.Select(row => row.Depth));

        // The row whose parent is gone is its own top of the tree, so it starts at 0 like a heading.
        Assert.Equal(0, rows.Last().Depth);
    }

    /// <summary>
    /// A pair caught in a loop is still left to the end, and the depth it is given is counted from the first row of the
    /// pair: the row each of them would step in under is in the same broken pair, so there is no truer depth to give
    /// it. The screen that repairs the catalogue reads this list rather than the headings' groups for exactly this
    /// reason - a group drops a loop rather than invent a place for it.
    /// </summary>
    [Fact]
    public void ALoopIsLeftToTheEndAndNumberedFromTheStartOfThePair()
    {
        var rows = CategoryTree.RowsInDisplayOrder(new[]
        {
            Category(1, "Toys", null),
            Category(14, "For Kids", 1),
            Category(2, "Decoratives", null),
            Category(6, "Vases", 2),
            Category(15, "Mobile Stands", 99),
            Category(30, "Loop One", 31),
            Category(31, "Loop Two", 30)
        });

        Assert.Equal(new short[] { 1, 14, 2, 6, 15, 30, 31 }, rows.Select(row => row.Category.CategoryId));
        Assert.Equal(new[] { 0, 1, 0, 1, 0, 0, 1 }, rows.Select(row => row.Depth));
    }

    private static AdminCategoryDto Category(short categoryId, string categoryName, short? parentCategoryId) => new()
    {
        CategoryId = categoryId,
        CategoryName = categoryName,
        ParentCategoryId = parentCategoryId
    };
}
