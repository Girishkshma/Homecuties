using HC.Business.Dtos;

namespace HC.Business;

/// <summary>
/// The storefront's reading of the catalogue: which rows its menus list, and what opening one of them means.
///
/// The catalogue is one self-referencing table ('Categories.ParentCategoryId') and a heading of it - a top of the
/// tree, such as 'Decoratives' - is not a place a product sits: the shop files its pieces on the shelves beneath the
/// heading ('Vases', 'Pot Houses', ...). Two questions follow from that, and both are answered here rather than in
/// the queries, so the storefront cannot answer either of them two ways:
/// <list type="number">
/// <item><b>Which rows a menu lists.</b> A shelf is only readable under the heading it belongs to, so the headings of
/// the shelves the shop sells from have to travel with them (see <see cref="ForBrowsing"/>). A heading with nothing
/// on sale beneath it is not listed at all - a menu item that opens an empty page is worse than no item.</item>
/// <item><b>What opening a row shows.</b> A heading the menu offers opens the whole branch under it, every shelf at
/// any depth included, because that is the only thing a heading can mean (see <see cref="Beneath"/>). Opening a shelf
/// shows that shelf.</item>
/// </list>
///
/// It works on <see cref="CategoryDto"/> - the three fields the storefront is told about a category - so the rules
/// can be pinned by tests the way the order rules are. The admin side has the same walk in
/// <see cref="CategoryTree"/>, over <c>AdminCategoryDto</c> and with the admin's own question ('what is filed where')
/// rather than the storefront's ('what can a shopper open'), so the two are deliberately separate.
/// </summary>
public static class CategoryBranch
{
    /// <summary>
    /// The rows a storefront menu lists: every category the shop sells from, and the rows above each of them, in the
    /// table's own order.
    ///
    /// The rows above a shelf are there to name it. A menu that offered 'Vases' on its own would say nothing about
    /// where the pieces are, and a shopper who never sees 'Decoratives' cannot tell that 'Vases', 'Pot Houses' and
    /// 'Show Pieces' are one collection of the shop's - so the heading travels with its shelves even though nothing
    /// is filed on the heading itself.
    ///
    /// Nothing the shop sells from is ever left out, which is why a row whose parent is missing, or which sits in a
    /// loop in the table (two rows each naming the other as parent), is still listed: the storefront sorts that out
    /// where it draws the menu (see the storefront's own category tree), and a product filed where no heading leads is
    /// better listed plainly than hidden. The walk up the parents stops at the first row it has already added, so a
    /// loop in the table ends the walk instead of running it forever.
    /// </summary>
    /// <param name="categories">The category rows, flat, as the table holds them.</param>
    /// <param name="withProducts">The categories that have something filed on them (their active product links).</param>
    public static List<CategoryDto> ForBrowsing(
        IEnumerable<CategoryDto> categories,
        IReadOnlyCollection<short> withProducts)
    {
        var all = categories.ToList();
        var byId = all.ToDictionary(category => category.CategoryID);
        var sells = withProducts.ToHashSet();
        var listed = new HashSet<short>();

        foreach (var category in all.Where(category => sells.Contains(category.CategoryID)))
        {
            var row = category;

            // Up to the top of this row's branch, adding each step once. A parent the table no longer holds (or a
            // loop that leads back into a row already added) ends the walk - there is nothing above it to name.
            while (row != null && listed.Add(row.CategoryID))
            {
                row = row.ParentCategoryID is { } parentId && byId.TryGetValue(parentId, out var parent)
                    ? parent
                    : null;
            }
        }

        return all.Where(category => listed.Contains(category.CategoryID)).ToList();
    }

    /// <summary>
    /// The branch of a category: the category itself and every category beneath it, at any depth - what a shopper is
    /// shown when they open it.
    ///
    /// It is what makes a heading a real browse rather than a page with nothing on it: the storefront's menus offer
    /// the headings (see <see cref="ForBrowsing"/>), and a query that asked for the products filed on the heading
    /// itself would answer with none of them, because the shop files its pieces on the shelves under the heading.
    ///
    /// The answer holds each row once, so a loop in the table (two rows each naming the other as parent) adds nothing
    /// twice and the walk ends. A category the table does not hold answers with just itself, so an id nothing is
    /// filed under is an empty listing rather than a refusal.
    /// </summary>
    public static List<short> Beneath(IEnumerable<CategoryDto> categories, short categoryId)
    {
        var childrenOf = categories
            .Where(category => category.ParentCategoryID is not null)
            .GroupBy(category => category.ParentCategoryID!.Value)
            .ToDictionary(group => group.Key, group => group.Select(category => category.CategoryID).ToList());

        var branch = new List<short> { categoryId };
        var listed = new HashSet<short> { categoryId };
        var toWalk = new Queue<short>();
        toWalk.Enqueue(categoryId);

        while (toWalk.Count > 0)
        {
            if (!childrenOf.TryGetValue(toWalk.Dequeue(), out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                if (!listed.Add(child))
                {
                    continue;
                }

                branch.Add(child);
                toWalk.Enqueue(child);
            }
        }

        return branch;
    }
}
