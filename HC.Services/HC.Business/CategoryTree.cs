using HC.Business.Dtos;

namespace HC.Business;

/// <summary>
/// The order the admin screens list the categories in: every category followed at once by the categories beneath it,
/// rather than the order the 'Categories' table happens to answer in.
///
/// The tree is one self-referencing table ('Categories.ParentCategoryId') and its rows are numbered as they were
/// added, so a sub-category sits wherever it was created. The live shop is exactly that shape: 'For Kids' is a child
/// of 'Toys' (id 1) but is row 14, so the product form's checkboxes read 'Toys', 'Decoratives', 'Decoratives / Vases'
/// ... and only at the end 'Toys / For Kids' - a sub-category listed after a dozen categories it has nothing to do
/// with, with a 'Parent / Child' label naming a parent far above it.
///
/// The order here is a walk of the tree: each top of the tree in turn (in the table's own order, so the shop's own
/// ordering is kept), under it its children, under each of those its own, and so on down. The product form draws one
/// checkbox per category in the order it is given and never re-sorts them, so this order is the order the admin sees.
///
/// Nothing is ever dropped, because a category an admin cannot see is one they cannot fix:
/// <list type="bullet">
/// <item>A category whose parent row is gone is a top of the tree here - the list holds nothing to file it under.</item>
/// <item>A category caught in a loop in the table (two rows each naming the other as parent) is unreachable from any
/// top, so it is listed at the end rather than taking everything below it out of the list.</item>
/// </list>
/// </summary>
public static class CategoryTree
{
    /// <summary>
    /// The categories in the order the admin screens list them: every category followed by the categories beneath it,
    /// depth first, with the categories of one parent in the order the table gave them. The same walk is answered
    /// row by row - with how deep each one sits - by <see cref="RowsInDisplayOrder"/>; this is that list without it.
    /// </summary>
    public static List<AdminCategoryDto> InDisplayOrder(IEnumerable<AdminCategoryDto> categories)
        => RowsInDisplayOrder(categories).Select(row => row.Category).ToList();

    /// <summary>
    /// The same walk, with how many steps each row sits below the top of the tree it belongs to: 0 for a heading, 1 for
    /// a shelf of one, 2 for a shelf of a shelf, and so on down.
    ///
    /// The admin's Categories screen draws the catalogue from this: the stepping of a row in is what says whether it is
    /// a heading or a shelf of one, so the depth travels with the row rather than being worked out again per screen
    /// (the dashboard's stock cards read it the same way - see <see cref="GroupedByHeading"/>).
    ///
    /// A row caught in a loop in the table is still left to the end and is numbered from 0, like the tops of the tree:
    /// the row it would step in under is in the same broken pair, so there is no truer depth to give it. That is why the
    /// screen that repairs the catalogue reads this list rather than the headings' groups, which drop a loop rather than
    /// invent a place for it.
    /// </summary>
    public static List<CatalogueRow> RowsInDisplayOrder(IEnumerable<AdminCategoryDto> categories)
    {
        var all = categories.ToList();
        var children = ChildrenOf(all);

        var ordered = new List<CatalogueRow>(all.Count);
        var listed = new HashSet<short>();

        // The tops of the tree first: a category with no parent, and one whose parent is not in the list at all.
        foreach (var top in TopsOfTree(all))
        {
            AddWithItsChildren(top, 0, children, listed, ordered);
        }

        // Anything still unlisted can only be inside a loop in the table, which no top of the tree leads into. It is
        // listed at the end, in the table's own order, so a broken pair is visible to whoever can repair it.
        foreach (var category in all)
        {
            AddWithItsChildren(category, 0, children, listed, ordered);
        }

        return ordered;
    }

    /// <summary>
    /// The tops of the tree among <paramref name="categories"/>: a category with no parent, and one whose parent is not
    /// in the list at all - the same reading the walk above starts from.
    ///
    /// These are the shop's headings, and a heading is not a place a product sits: a customer browses the categories
    /// beneath one, so a product filed under the heading itself is a product that is listed nowhere. The admin's product
    /// form therefore draws a heading greyed with no tick to give, and the API refuses a product that names one (see
    /// <c>AdminDashboardService.CreateProductAsync</c>) - both of them by asking this question of the same list.
    /// </summary>
    public static List<AdminCategoryDto> TopsOfTree(IEnumerable<AdminCategoryDto> categories)
    {
        var all = categories.ToList();
        var ids = all.Select(category => category.CategoryId).ToHashSet();

        return all.Where(category => IsTopOfTree(category, ids)).ToList();
    }

    /// <summary>True for a category the walk can start at: one whose parent is nothing, or is not in the list.</summary>
    private static bool IsTopOfTree(AdminCategoryDto category, HashSet<short> ids)
        => category.ParentCategoryId is null || !ids.Contains(category.ParentCategoryId.Value);

    /// <summary>
    /// Adds a category and then everything beneath it, depth first, each row carrying how many steps below the top of
    /// the tree it sits. A category already listed is not added again, which is what ends a loop in the table rather
    /// than walking it forever.
    /// </summary>
    private static void AddWithItsChildren(
        AdminCategoryDto category,
        int depth,
        Dictionary<short, List<AdminCategoryDto>> children,
        HashSet<short> listed,
        List<CatalogueRow> ordered)
    {
        if (!listed.Add(category.CategoryId))
        {
            return;
        }

        ordered.Add(new CatalogueRow(category, depth));

        if (!children.TryGetValue(category.CategoryId, out var beneath))
        {
            return;
        }

        foreach (var child in beneath)
        {
            AddWithItsChildren(child, depth + 1, children, listed, ordered);
        }
    }

    /// <summary>
    /// The catalogue's headings, each with everything filed beneath it: one group per top of the tree, the heading
    /// first and then the categories below it depth first, in the same order <see cref="InDisplayOrder"/> lists them.
    ///
    /// This is the shape the dashboard's 'Stock by category' cards are drawn from (see HC.Business.CategoryStock and
    /// <c>AdminDashboardService.GetStockByCategoryAsync</c>), which is why a row also says how deep it sits beneath
    /// its heading: a card steps a sub-category of a sub-category in under the row it belongs to.
    ///
    /// Only what a top of the tree leads into is grouped. A category caught in a loop in the table is in no group at
    /// all - the same rows the walk above leaves to the end - and a caller that shows units has to say so about it
    /// rather than leave them out silently.
    /// </summary>
    public static List<HeadingGroup> GroupedByHeading(IEnumerable<AdminCategoryDto> categories)
    {
        var all = categories.ToList();
        var children = ChildrenOf(all);
        var groups = new List<HeadingGroup>();

        foreach (var top in TopsOfTree(all))
        {
            var group = new HeadingGroup(top);
            AddBeneath(top, 0, children, new HashSet<short> { top.CategoryId }, group.Beneath);

            groups.Add(group);
        }

        return groups;
    }

    /// <summary>
    /// Adds everything beneath a category, depth first, with how deep each one sits under the heading. A category
    /// already listed is not added again, which is what ends a loop in the table rather than walking it forever.
    /// </summary>
    private static void AddBeneath(
        AdminCategoryDto category,
        int depth,
        Dictionary<short, List<AdminCategoryDto>> children,
        HashSet<short> listed,
        List<HeadingRow> rows)
    {
        if (!children.TryGetValue(category.CategoryId, out var beneath))
        {
            return;
        }

        foreach (var child in beneath)
        {
            if (!listed.Add(child.CategoryId))
            {
                continue;
            }

            rows.Add(new HeadingRow(child, depth));

            AddBeneath(child, depth + 1, children, listed, rows);
        }
    }

    /// <summary>
    /// The children of each category, gathered once. 'Categories' answers its rows flat, so a walk of the tree would
    /// otherwise search for the children of every step. A parent the list does not hold is left out here, which is
    /// what makes a category whose parent row is gone a top of the tree rather than a child of nothing.
    /// </summary>
    private static Dictionary<short, List<AdminCategoryDto>> ChildrenOf(List<AdminCategoryDto> categories)
    {
        var ids = categories.Select(category => category.CategoryId).ToHashSet();

        return categories
            .Where(category => category.ParentCategoryId is { } parentId && ids.Contains(parentId))
            .GroupBy(category => category.ParentCategoryId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
    }
}

/// <summary>A heading of the catalogue - a top of the tree - and the categories filed beneath it.</summary>
public sealed record HeadingGroup(AdminCategoryDto Heading)
{
    /// <summary>
    /// The categories beneath the heading, depth first: a category always before the ones under it, so a caller
    /// folding their figures upwards reads the list in reverse. The heading itself is not in here.
    /// </summary>
    public List<HeadingRow> Beneath { get; } = new();
}

/// <summary>
/// A category beneath a heading, and how deep it sits there: 0 for the heading's own child, 1 for a sub-category of
/// one of those, and so on down.
/// </summary>
public sealed record HeadingRow(AdminCategoryDto Category, int Depth);

/// <summary>
/// A category of the admin's own list (<see cref="CategoryTree.RowsInDisplayOrder"/>) and how many steps it sits below
/// the top of the tree it belongs to: 0 for a heading, 1 for a shelf of one, and so on down. Unlike
/// <see cref="HeadingRow"/>, which only ever describes a row read under a heading, this describes every row of the
/// table - a heading among them.
/// </summary>
public sealed record CatalogueRow(AdminCategoryDto Category, int Depth);
