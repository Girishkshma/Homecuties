using HC.Business.Dtos;

namespace HC.Business;

/// <summary>
/// The shop's own edits to the catalogue: what a category may be called, where one may sit, and when one may be taken
/// out - the questions the admin's Categories screen asks before it writes anything.
///
/// The catalogue is one self-referencing table ('Categories.ParentCategoryId'). Everything else reads it and never
/// changes it: the storefront's menus and what opening one of them shows live in <see cref="CategoryBranch"/>, and the
/// order the admin's own screens list the rows in lives in <see cref="CategoryTree"/>. This is the only place that
/// decides what may be written to it, and it is a class of its own so the rules can be pinned by tests rather than by
/// a screen - what they protect is not visible from any one screen. A category put under itself cannot be drawn by any
/// list; a category put under one of its own shelves is a branch no heading leads into, so the products filed on it
/// drop out of the shop's menus while still sitting in the table; and a category taken out while shelves sit under it
/// leaves each of them naming a parent that is gone, which the storefront then reads as a heading of its own (see
/// <see cref="CategoryTree.TopsOfTree"/>) - one delete turning a filing into a catalogue nobody filed that way.
///
/// Every rule answers with the sentence the screen shows rather than with a bare refusal, and nothing here writes:
/// the reads that call them are in AdminDashboardService.Categories.cs.
/// </summary>
public static class CategoryCatalog
{
    /// <summary>
    /// How long a category name may be: 'Categories.CategoryName' is varchar(20) in the shop's database, so a longer
    /// name is refused here with a sentence rather than by the database when the row is saved.
    /// </summary>
    public const int MaxNameLength = 20;

    /// <summary>
    /// Why the name cannot be used, or null when it can: a name is required and cannot be longer than the column
    /// holds. The name is trimmed here exactly as the write trims it, so a name of spaces is a name that is not there.
    ///
    /// Nothing is said here about two headings carrying a shelf of the same name: 'Vases' under 'Decoratives' and under
    /// 'Furnitures' are two shelves a shopper reads under their own heading ('Decoratives / Vases'), which is what
    /// <see cref="SiblingProblem"/> is for - the pair the shop cannot tell apart is two shelves of one heading.
    /// </summary>
    public static string? NameProblem(string? name)
    {
        var cleaned = (name ?? "").Trim();

        if (cleaned.Length == 0)
            return "Category name is required.";

        if (cleaned.Length > MaxNameLength)
            return $"Category name cannot be longer than {MaxNameLength} characters.";

        return null;
    }

    /// <summary>
    /// Why the name cannot be used for a category sitting where the admin is putting it, or null when it can: two
    /// categories of one parent cannot share a name.
    ///
    /// It is the one duplicate the shop cannot read past. The product form writes every category it offers as
    /// 'Parent / Child', so two 'Vases' under 'Decoratives' are two choices that read the same and a product filed on
    /// the wrong one is a product nobody can tell is filed wrongly. Names of different parents may repeat freely,
    /// because their labels do not.
    /// </summary>
    /// <param name="name">The name as the admin typed it; trimmed here, as the write trims it.</param>
    /// <param name="parentCategoryId">Where the category is being put - null when it becomes a heading.</param>
    /// <param name="categoryId">The row being edited, so it does not clash with the name it already carries; null when the row is being created.</param>
    /// <param name="categories">Every category of the table, flat (the walks below read the whole tree).</param>
    public static string? SiblingProblem(
        string? name,
        short? parentCategoryId,
        short? categoryId,
        IEnumerable<AdminCategoryDto> categories)
    {
        var cleaned = (name ?? "").Trim();

        // A name that is not there clashes with nothing: NameProblem has already refused it, and its own sentence is
        // the one the admin needs.
        if (cleaned.Length == 0)
            return null;

        var clash = categories.FirstOrDefault(category =>
            category.CategoryId != categoryId
            && category.ParentCategoryId == parentCategoryId
            && string.Equals(category.CategoryName.Trim(), cleaned, StringComparison.OrdinalIgnoreCase));

        return clash == null
            ? null
            : $"A category named '{cleaned}' already sits there. Two categories of one parent cannot share a name.";
    }

    /// <summary>
    /// Why the category cannot sit under the chosen parent, or null when it can.
    ///
    /// Moving a category is the one edit that can take the catalogue apart, because the table carries no rule about
    /// itself beyond the parent each row names. A category put under itself, or under one of its own shelves at any
    /// depth, makes a branch nothing can be reached from - the shelf names a parent that is beneath it, so no heading
    /// leads into either of them - and both are refused here with the sentence the screen shows. A parent row that is
    /// no longer in the table is refused too, rather than left to fail on the foreign key with nothing an admin could
    /// act on.
    ///
    /// What is deliberately not here: any depth a category may sit at. A heading's branch is read however deep it
    /// goes (see <see cref="CategoryBranch.Beneath"/> for the storefront and <see cref="CategoryTree.RowsInDisplayOrder"/>
    /// for the admin's own lists), so a shelf of a shelf is a place the shop may use rather than a mistake.
    /// </summary>
    /// <param name="parentCategoryId">The parent the admin chose - null for a heading, which is always a place in the catalogue.</param>
    /// <param name="categoryId">The row being moved; null when the row is being created (a row that is not there yet cannot sit under itself).</param>
    /// <param name="categories">Every category of the table, flat.</param>
    public static string? ParentProblem(
        short? parentCategoryId,
        short? categoryId,
        IEnumerable<AdminCategoryDto> categories)
    {
        if (parentCategoryId is not { } parentId)
            return null;

        var all = categories.ToList();
        var parent = all.FirstOrDefault(category => category.CategoryId == parentId);

        if (parent == null)
            return "The category it should sit under is no longer in the catalogue.";

        if (categoryId == parentId)
            return "A category cannot sit under itself.";

        if (categoryId is { } id && Beneath(all, id).Contains(parentId))
            return $"'{parent.CategoryName}' is one of this category's own shelves: putting it here would make a branch no heading leads into.";

        return null;
    }

    /// <summary>
    /// Why the category cannot be taken out of the catalogue, or null when it can.
    ///
    /// A row may only be removed as a leaf, and the shop is told which of the two things is in the way rather than
    /// simply being refused:
    /// <list type="bullet">
    /// <item>A category with shelves directly under it cannot go: those rows would be left naming a parent that is
    /// gone, which the storefront reads as a heading of its own - an edit to one row turned into a catalogue nobody
    /// filed that way. Moving the shelves out first is an edit this same screen makes.</item>
    /// <item>A category with products filed on it cannot go: the links in 'ProductCategories' are how a product is
    /// filed at all, and the delete would have to take them with it - those products would drop out of the shop's
    /// menus and filters while still sitting in the table. The product form is where they are moved.</item>
    /// </list>
    ///
    /// The count of products is every link on the category, switched on or off: a link the shop has turned off is
    /// still a row the delete would remove, so it is counted and said rather than quietly deleted with it.
    /// </summary>
    /// <param name="categoryId">The category being removed.</param>
    /// <param name="categories">Every category of the table, flat.</param>
    /// <param name="productsFiled">Every product link on the category, whatever state the link is in.</param>
    public static string? DeleteProblem(
        short categoryId,
        IEnumerable<AdminCategoryDto> categories,
        int productsFiled)
    {
        var shelves = categories.Count(category => category.ParentCategoryId == categoryId);

        if (shelves == 1)
            return "One category sits under this one. Move it out or delete it first.";
        if (shelves > 1)
            return $"{shelves} categories sit under this one. Move them out or delete them first.";

        if (productsFiled == 1)
            return "One product is filed on this category. Move it to another category first.";
        if (productsFiled > 1)
            return $"{productsFiled} products are filed on this category. Move them to another category first.";

        return null;
    }

    /// <summary>
    /// A category and everything beneath it, at any depth - the rows a move would carry with it, which is why a parent
    /// inside this set is refused.
    ///
    /// The walk is the same guarded one the rest of the catalogue uses: a row already walked is not walked again, so a
    /// loop in the table (two rows each naming the other as parent) ends the walk instead of running it forever.
    /// </summary>
    private static HashSet<short> Beneath(List<AdminCategoryDto> categories, short categoryId)
    {
        var childrenOf = categories
            .Where(category => category.ParentCategoryId is not null)
            .GroupBy(category => category.ParentCategoryId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(category => category.CategoryId).ToList());

        var beneath = new HashSet<short> { categoryId };
        var toWalk = new Queue<short>();
        toWalk.Enqueue(categoryId);

        while (toWalk.Count > 0)
        {
            if (!childrenOf.TryGetValue(toWalk.Dequeue(), out var children))
                continue;

            foreach (var child in children.Where(beneath.Add))
            {
                toWalk.Enqueue(child);
            }
        }

        return beneath;
    }
}
