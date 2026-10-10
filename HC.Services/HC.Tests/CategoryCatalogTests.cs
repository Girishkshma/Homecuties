using HC.Business;
using HC.Business.Dtos;
using Xunit;

namespace HC.Tests;

/// <summary>
/// The shop's own edits to the catalogue (see <see cref="CategoryCatalog"/>): what a category may be called, where one
/// may sit, and when one may be taken out.
///
/// Nothing here touches a database: the rules are asked of a list of rows, which is exactly how the API asks them, and
/// what they protect is what one wrong answer costs - a category under itself cannot be drawn by any list, a category
/// under one of its own shelves is a branch no heading leads into (so the products filed in it drop out of the shop's
/// menus while still sitting in the table), and a delete that leaves shelves naming a parent that is gone turns each of
/// them into a heading of its own. The shop's own sixteen rows ('Toys' with 'For Kids', 'Decoratives' with its four
/// shelves, and the rest) are the shape these tests are written on.
/// </summary>
public class CategoryCatalogTests
{
    /// <summary>
    /// A name is required and cannot be longer than the column takes - 'Categories.CategoryName' is varchar(20), so a
    /// 21st character is refused with a sentence here rather than by the database when the row is saved.
    /// </summary>
    [Fact]
    public void ANameIsRequiredAndNoLongerThanTheColumnHolds()
    {
        Assert.NotNull(CategoryCatalog.NameProblem(null));
        Assert.NotNull(CategoryCatalog.NameProblem(""));
        Assert.NotNull(CategoryCatalog.NameProblem("   "));
        Assert.Contains("required", CategoryCatalog.NameProblem("   "));

        Assert.Null(CategoryCatalog.NameProblem("Coasters"));
        Assert.Null(CategoryCatalog.NameProblem("  Coasters  "));
        Assert.Null(CategoryCatalog.NameProblem(new string('a', CategoryCatalog.MaxNameLength)));

        var tooLong = new string('a', CategoryCatalog.MaxNameLength + 1);
        Assert.Contains(CategoryCatalog.MaxNameLength.ToString(), CategoryCatalog.NameProblem(tooLong));
    }

    /// <summary>
    /// Two categories of one parent cannot share a name, because that is the pair the shop cannot read past: the
    /// product form offers every category as 'Parent / Child', so two 'Vases' under 'Decoratives' are two choices that
    /// read the same. Names of different parents may repeat - 'Decoratives / Vases' and 'Furnitures / Vases' are two
    /// labels a shopper can tell apart - and a category keeping the name it already carries is not a clash with itself.
    /// </summary>
    [Fact]
    public void TwoCategoriesOfOneParentCannotShareAName()
    {
        var catalogue = TheShopsCatalogue();

        var problem = CategoryCatalog.SiblingProblem("Vases", 2, null, catalogue);
        Assert.NotNull(problem);
        Assert.Contains("Vases", problem);

        // The same shelf name under another heading is another shelf, read under its own heading.
        Assert.Null(CategoryCatalog.SiblingProblem("Vases", 5, null, catalogue));

        // A name the shop typed in another case is the same name to every reader, so it is the same clash.
        Assert.NotNull(CategoryCatalog.SiblingProblem("vases", 2, null, catalogue));

        // Editing 'Vases' (id 6) and sending its own name back is not a clash with the row being edited.
        Assert.Null(CategoryCatalog.SiblingProblem("Vases", 2, 6, catalogue));

        // A heading is a place in the catalogue too, and two headings cannot share a name either.
        Assert.NotNull(CategoryCatalog.SiblingProblem("Toys", null, null, catalogue));
        Assert.Null(CategoryCatalog.SiblingProblem("Toys", null, 1, catalogue));

        // A name that is not there is NameProblem's to refuse; this rule says nothing about it.
        Assert.Null(CategoryCatalog.SiblingProblem("", 2, null, catalogue));
    }

    /// <summary>
    /// A category cannot sit under itself, nor under one of its own shelves at any depth: either way the shelf names a
    /// parent that is beneath it, so no heading leads into the branch and the products filed anywhere in it drop out of
    /// the shop's menus while still sitting in the table.
    /// </summary>
    [Fact]
    public void ACategoryCannotSitUnderItselfOrUnderOneOfItsOwnShelves()
    {
        var catalogue = TheShopsCatalogue();

        var underItself = CategoryCatalog.ParentProblem(2, 2, catalogue);
        Assert.NotNull(underItself);
        Assert.Contains("itself", underItself);

        // 'Decoratives' (2) holds 'Vases' (6), so neither that shelf nor a shelf of that shelf may become its parent.
        Assert.NotNull(CategoryCatalog.ParentProblem(6, 2, catalogue));
        Assert.NotNull(CategoryCatalog.ParentProblem(9, 2, catalogue));

        // A shelf of another heading is a move the shop may make, and so is becoming a heading.
        Assert.Null(CategoryCatalog.ParentProblem(5, 2, catalogue));
        Assert.Null(CategoryCatalog.ParentProblem(null, 2, catalogue));
    }

    /// <summary>
    /// A parent the table no longer holds is refused with a sentence, rather than left to fail on the foreign key when
    /// the row is saved - an admin cannot act on a constraint name, and the edit they were making is not written.
    /// </summary>
    [Fact]
    public void AParentTheCatalogueNoLongerHoldsIsRefused()
    {
        var problem = CategoryCatalog.ParentProblem(99, 6, TheShopsCatalogue());

        Assert.NotNull(problem);
        Assert.Contains("no longer in the catalogue", problem);
    }

    /// <summary>
    /// A row being created (no id of its own yet) can name any parent the table holds - it cannot be its own shelf,
    /// because it has none - and a heading is always a place in the catalogue: the top of a branch is not a mistake.
    /// </summary>
    [Fact]
    public void ANewRowCanNameAnyParentAndAHeadingIsAlwaysAllowed()
    {
        var catalogue = TheShopsCatalogue();

        Assert.Null(CategoryCatalog.ParentProblem(6, null, catalogue));
        Assert.Null(CategoryCatalog.ParentProblem(null, null, catalogue));
    }

    /// <summary>
    /// A category with categories directly under it cannot be taken out: those rows would be left naming a parent that
    /// is gone, which the storefront reads as a heading of its own - one delete turning a filing into a catalogue
    /// nobody filed that way. What is under them travels with the heading, so 'Decoratives' reports its four shelves
    /// and is not let go of until they are dealt with.
    /// </summary>
    [Fact]
    public void ACategoryWithShelvesUnderItCannotBeTakenOut()
    {
        var catalogue = TheShopsCatalogue();

        var fourShelves = CategoryCatalog.DeleteProblem(2, catalogue, 0);
        Assert.NotNull(fourShelves);
        Assert.Contains("4 categories sit under this one", fourShelves);
        Assert.Contains("Move them out or delete them first", fourShelves);

        // One shelf is said in the singular, because the message is read as a sentence about this category.
        var oneShelf = CategoryCatalog.DeleteProblem(1, catalogue, 0);
        Assert.NotNull(oneShelf);
        Assert.Contains("One category sits under this one", oneShelf);
    }

    /// <summary>
    /// A category with products filed on it cannot be taken out: the links in 'ProductCategories' are how a product is
    /// filed at all, and the delete would take them with it, so those products would drop out of the shop's menus and
    /// filters while still sitting in the table. The count is every link on the category, switched on or off - a link
    /// the shop has turned off is still a row the delete would remove.
    /// </summary>
    [Fact]
    public void ACategoryWithProductsFiledOnItCannotBeTakenOut()
    {
        var catalogue = TheShopsCatalogue();

        var one = CategoryCatalog.DeleteProblem(6, catalogue, 1);
        Assert.NotNull(one);
        Assert.Contains("One product is filed on this category", one);

        var many = CategoryCatalog.DeleteProblem(6, catalogue, 12);
        Assert.NotNull(many);
        Assert.Contains("12 products are filed on this category", many);

        // What sits under the category is what is said first: the shelves are what the admin has to deal with, and
        // counting the products of a heading at all is the read's business (a heading is no place a product sits).
        var both = CategoryCatalog.DeleteProblem(2, catalogue, 3);
        Assert.NotNull(both);
        Assert.Contains("4 categories sit under this one", both);
    }

    /// <summary>
    /// A category with nothing under it and nothing filed on it is a row nothing refers to, so it can go - and a
    /// heading with no shelves is one of those: the two counts on the screen, not the family a row belongs to, are what
    /// decide it. A row the table no longer holds is no longer in the way of anything either.
    /// </summary>
    [Fact]
    public void ACategoryNothingRefersToCanBeTakenOut()
    {
        var catalogue = TheShopsCatalogue();

        // 'Vases' (6), a shelf of 'Decoratives', with nothing filed on it.
        Assert.Null(CategoryCatalog.DeleteProblem(6, catalogue, 0));

        // A heading that holds nothing at all (row 20), and a row already deleted (row 99).
        Assert.Null(CategoryCatalog.DeleteProblem(20, catalogue, 0));
        Assert.Null(CategoryCatalog.DeleteProblem(99, catalogue, 0));

        // The heading those shelves are read under is held on to.
        Assert.NotNull(CategoryCatalog.DeleteProblem(2, catalogue, 0));
    }

    /// <summary>
    /// A loop in the table - two rows each naming the other as parent - does not take the walk round for ever: each of
    /// the pair is still refused as the other's parent, and each still holds the other, so neither can be taken out
    /// while they are as they are. That is what an admin repairing the catalogue has to be told.
    /// </summary>
    [Fact]
    public void ALoopInTheTableEndsTheWalkAndIsStillRefusedAsAParent()
    {
        var catalogue = new[]
        {
            Category(2, "Decoratives", null),
            Category(30, "Loop One", 31),
            Category(31, "Loop Two", 30)
        };

        Assert.NotNull(CategoryCatalog.ParentProblem(31, 30, catalogue));
        Assert.NotNull(CategoryCatalog.ParentProblem(30, 31, catalogue));

        Assert.NotNull(CategoryCatalog.DeleteProblem(30, catalogue, 0));
        Assert.NotNull(CategoryCatalog.DeleteProblem(31, catalogue, 0));

        // Neither of the pair is a shelf of 'Decoratives', so neither name clashes with its own or with the other's.
        Assert.Null(CategoryCatalog.SiblingProblem("Loop One", 2, null, catalogue));
    }

    /// <summary>
    /// The shop's sixteen rows, in the order the table answers them: five headings and the shelves under them, which is
    /// the shape every rule above is asked about (the same rows CategoryTreeTests lists in display order).
    /// </summary>
    private static AdminCategoryDto[] TheShopsCatalogue() => new[]
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
    };

    private static AdminCategoryDto Category(short categoryId, string categoryName, short? parentCategoryId) => new()
    {
        CategoryId = categoryId,
        CategoryName = categoryName,
        ParentCategoryId = parentCategoryId
    };
}
