using HC.Business;
using HC.Business.Dtos;
using Xunit;

namespace HC.Tests;

/// <summary>
/// What the storefront lists and what opening a row of it shows: the two questions a shopper's menus ask of the
/// catalogue, answered here because a heading of the catalogue is not a place a product sits.
///
/// The live shop is exactly that shape: 'Decoratives' is a heading with 'Vases', 'Pot Houses', 'Musicians' and
/// 'Show Pieces' under it, and nothing filed on the heading itself. So a menu has to list the heading to have a name
/// to read its shelves under (see <see cref="AHeadingTravelsWithTheShelvesTheShopSellsFrom"/>), and opening the
/// heading has to show the shelves under it (see <see cref="OpeningAHeadingShowsTheWholeBranchUnderIt"/>) - a menu
/// item that opens an empty page is worse than no item at all. The broken-catalogue cases are pinned too, because
/// the storefront is the one place they would be invisible: nothing the shop sells from may quietly drop out of a
/// menu (see <see cref="NothingTheShopSellsFromIsEverLeftOut"/>).
/// </summary>
public class CategoryBranchTests
{
    /// <summary>
    /// The menus list the shelves the shop sells from, and the heading of each - so a shopper reads 'Vases' under
    /// 'Decoratives' rather than as a row on its own. A heading with nothing on sale underneath it is not listed at
    /// all, and the parent travels on the row, which is what the storefront groups a menu by.
    /// </summary>
    [Fact]
    public void AHeadingTravelsWithTheShelvesTheShopSellsFrom()
    {
        var listed = CategoryBranch.ForBrowsing(
            new[]
            {
                Category(2, "Decoratives", null),
                Category(5, "Vases", 2),
                Category(6, "Pot Houses", 2),
                Category(50, "Nothing On Sale", null),
                Category(11, "Furnitures", null),
                Category(13, "Center Tables", 11)
            },
            new short[] { 5, 13 });

        // The table's own order, and the rows above the shelves the shop sells from travel with them: 'Decoratives'
        // is listed although nothing is filed on it, and 'Furnitures' with it.
        Assert.Equal(new short[] { 2, 5, 11, 13 }, Ids(listed));

        // A heading the shop sells nothing under is not a menu item at all.
        Assert.DoesNotContain((short)50, listed.Select(category => category.CategoryID));

        // The parent travels on the row, which is what the storefront groups a menu by.
        Assert.Equal((short?)2, listed.Single(category => category.CategoryID == 5).ParentCategoryID);
        Assert.Null(listed.Single(category => category.CategoryID == 2).ParentCategoryID);
    }

    /// <summary>
    /// A branch is deeper than the two levels the live shop has: a heading takes in its child, its child's child and
    /// so on, and a shelf that is opened too. Every shelf at any depth is listed in the menu (under its own parent),
    /// so nothing the shop sells from is unreachable just for sitting deeper.
    /// </summary>
    [Fact]
    public void AHeadingTravelsWithItsWholeBranchAtAnyDepth()
    {
        var listed = CategoryBranch.ForBrowsing(
            new[]
            {
                Category(1, "Toys", null),
                Category(14, "For Kids", 1),
                Category(17, "Plush", 14)
            },
            new short[] { 17 });

        Assert.Equal(new short[] { 1, 14, 17 }, Ids(listed));
    }

    /// <summary>
    /// Opening a heading shows everything filed under it, at any depth, each shelf once - which is what the shelves
    /// under a heading mean. Opening a shelf shows that shelf and the shelves under it, and opening an id nothing is
    /// filed under is an empty listing rather than a refusal.
    /// </summary>
    [Fact]
    public void OpeningAHeadingShowsTheWholeBranchUnderIt()
    {
        var categories = new[]
        {
            Category(2, "Decoratives", null),
            Category(5, "Vases", 2),
            Category(6, "Pot Houses", 2),
            Category(18, "Tall Vases", 5),
            Category(11, "Furnitures", null)
        };

        Assert.Equal(
            new short[] { 2, 5, 6, 18 },
            CategoryBranch.Beneath(categories, 2).OrderBy(categoryId => categoryId).ToArray());

        Assert.Equal(
            new short[] { 5, 18 },
            CategoryBranch.Beneath(categories, 5).OrderBy(categoryId => categoryId).ToArray());

        // A heading with nothing under it at all, and an id the table does not hold.
        Assert.Equal(new short[] { 11 }, CategoryBranch.Beneath(categories, 11));
        Assert.Equal(new short[] { 99 }, CategoryBranch.Beneath(categories, 99));
    }

    /// <summary>
    /// A broken catalogue does not lose a shelf from the menu, and does not run the walks forever:
    /// <list type="bullet">
    /// <item>a row whose parent the table no longer holds is still listed - as its own row, since there is no heading
    /// left to read it under;</item>
    /// <item>a row caught in a loop in the table (two rows each naming the other as parent) is listed too, and the
    /// walk up its parents ends at the row it started from instead of going round;</item>
    /// <item>a branch never lists a row twice, so opening a heading that leads into such a loop ends as well.</item>
    /// </list>
    /// </summary>
    [Fact]
    public void NothingTheShopSellsFromIsEverLeftOut()
    {
        var categories = new[]
        {
            Category(2, "Decoratives", null),
            Category(5, "Vases", 2),
            Category(60, "Orphan", 99),      // 99 is not in the table: the row it names as its parent is gone
            Category(70, "Oddities", 71),    // a pair that each names the other as parent
            Category(71, "Odder", 70)
        };

        var listed = CategoryBranch.ForBrowsing(categories, new short[] { 5, 60, 70 });

        // The loop's two rows travel together, each naming the other as parent, and the walk ends rather than going
        // round: the shelf is listed, and so is the row it names - the storefront shows a pair like this rather than
        // dropping the products filed under it.
        Assert.Equal(new short[] { 2, 5, 60, 70, 71 }, Ids(listed));

        // The walks end, and each row of a branch is in it once.
        Assert.Equal(
            new short[] { 2, 5 },
            CategoryBranch.Beneath(categories, 2).OrderBy(categoryId => categoryId).ToArray());

        Assert.Equal(
            new short[] { 70, 71 },
            CategoryBranch.Beneath(categories, 70).OrderBy(categoryId => categoryId).ToArray());

        // A search ends the same way: the word names one row of the loop, and the branch behind it answers without
        // going round.
        Assert.Equal(new short[] { 70, 71 }, CategoryBranch.Answering(categories, "Odd"));
    }

    /// <summary>
    /// A search word that names a category is answered the way the category itself is: the search holds what opening it
    /// shows (see <see cref="CategoryBranch.Answering"/>). That is what makes a search for a heading - a name the menu
    /// offers, with nothing filed on it - answer with the heading's pieces instead of with nothing: 'Decoratives' takes
    /// in 'Vases', 'Pot Houses' and everything filed deeper still.
    ///
    /// The word is read without regard to the case it was typed in and a part of a name is enough; a word no category
    /// names answers with no categories, which is the ordinary case of a search; and a blank word names nothing.
    /// </summary>
    [Fact]
    public void ASearchForACategoryAnswersWithWhatOpeningItShows()
    {
        var categories = new[]
        {
            Category(2, "Decoratives", null),
            Category(5, "Vases", 2),
            Category(6, "Pot Houses", 2),
            Category(18, "Tall Vases", 5),
            Category(1, "Toys", null)
        };

        // A shelf takes in the shelves filed beneath it.
        Assert.Equal(new short[] { 5, 18 }, CategoryBranch.Answering(categories, "Vases"));

        // A heading takes in its whole branch, at any depth - and the case it was typed in means nothing.
        Assert.Equal(new short[] { 2, 5, 6, 18 }, CategoryBranch.Answering(categories, "decoratives"));

        // Part of a name is enough to name the category.
        Assert.Equal(new short[] { 6 }, CategoryBranch.Answering(categories, "POT"));

        // A heading with nothing filed under it answers with itself, as opening it would.
        Assert.Equal(new short[] { 1 }, CategoryBranch.Answering(categories, "toys"));

        // A word no category names - 'wooden' is a piece, not a collection - names no categories at all.
        Assert.Empty(CategoryBranch.Answering(categories, "wooden"));
        Assert.Empty(CategoryBranch.Answering(categories, "   "));
        Assert.Empty(CategoryBranch.Answering(categories, null));
    }

    /// <summary>
    /// The rows a word names on their own: what the header's box offers while a shopper is still typing. A suggestion
    /// is a place a click opens, so it offers the row that answers to the word - 'Decoratives' for 'decor' - and not
    /// the shelves beneath it, which are what the listing (not the suggestion) answers with.
    /// </summary>
    [Fact]
    public void AWordNamesTheRowsThatCarryItAndNotTheRowsBeneathThem()
    {
        var categories = new[]
        {
            Category(2, "Decoratives", null),
            Category(5, "Vases", 2),
            Category(6, "Pot Houses", 2),
            Category(18, "Tall Vases", 5),
            Category(1, "Toys", null)
        };

        // The row that answers, not its branch: 'Vases' is offered once even though 'Tall Vases' answers to it too.
        Assert.Equal(
            new short[] { 5, 18 },
            CategoryBranch.NamedBy(categories, "vases").Select(category => category.CategoryID).ToArray());

        // A heading answers on its own name, while the search for the same word answers with its whole branch.
        Assert.Equal(
            new short[] { 2 },
            CategoryBranch.NamedBy(categories, "DECOR").Select(category => category.CategoryID).ToArray());
        Assert.Equal(new short[] { 2, 5, 6, 18 }, CategoryBranch.Answering(categories, "DECOR"));

        // Nothing to go on: a word no name carries, and a blank or missing one.
        Assert.Empty(CategoryBranch.NamedBy(categories, "wooden"));
        Assert.Empty(CategoryBranch.NamedBy(categories, "   "));
        Assert.Empty(CategoryBranch.NamedBy(categories, null));
    }

    /// <summary>The category ids of a listing, in the order the API sent them.</summary>
    private static short[] Ids(IEnumerable<CategoryDto> categories) =>
        categories.Select(category => category.CategoryID).ToArray();

    private static CategoryDto Category(short categoryId, string categoryName, short? parentCategoryId) => new()
    {
        CategoryID = categoryId,
        CategoryName = categoryName,
        ParentCategoryID = parentCategoryId
    };
}
