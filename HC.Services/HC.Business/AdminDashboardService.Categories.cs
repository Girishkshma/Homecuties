// ============================================================
// AdminDashboardService.Categories.cs
// Partial class: AdminDashboardService - Categories operations
// ============================================================

using System.Security.Cryptography;
using System.Text;
using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HC.Business;

public partial class AdminDashboardService : IAdminDashboardService
{
    public async Task<List<AdminCategoryDto>> GetCategoriesAsync()
    {
        return await _context.Categories
            .Select(c => new AdminCategoryDto
            {
                CategoryId = c.CategoryId,
                CategoryName = c.CategoryName,
                ParentCategoryId = c.ParentCategoryId
            })
            .ToListAsync();
    }

    /// <summary>
    /// The categories of the admin product form's checkbox list and of the admin user form: each category followed by
    /// the categories beneath it (see <see cref="CategoryTree"/>).
    ///
    /// The form writes 'Parent / Child' on a sub-category and lists the categories in the order it is given, so the
    /// hierarchical order is what makes a sub-category read as one - without it the screen puts a sub-category after
    /// whichever categories the table happens to answer before it.
    /// </summary>
    public async Task<List<AdminCategoryDto>> GetCategoryTreeAsync()
    {
        return CategoryTree.InDisplayOrder(await AllCategoriesAsync());
    }

    /// <summary>
    /// The catalogue as the admin's Categories screen lists it: every category in the order the screens walk it, each
    /// with where it sits and the two figures that decide whether it can be deleted (see
    /// <see cref="CategoryTree.RowsInDisplayOrder"/> for the order and the depth, and <see cref="CategoryCatalog"/> for
    /// the rules those figures answer).
    ///
    /// The rows and their parents come from one read; the counts come from a second one over the two things that refer
    /// to a category ('ProductCategories' for what is filed on it, and the table's own self-reference for what sits
    /// under it), folded onto the rows in memory. Reading the whole catalogue is what these rules cost - a rule that
    /// saw only the row it was about could not tell that a chosen parent is one of that row's own shelves.
    /// </summary>
    public async Task<List<AdminCategoryListDto>> GetManagedCategoriesAsync()
    {
        var categories = await AllCategoriesAsync();

        var counts = (await _context.Categories
                .Select(c => new
                {
                    c.CategoryId,
                    Products = c.ProductCategories.Count,
                    Children = c.InverseParentCategory.Count
                })
                .ToListAsync())
            .ToDictionary(entry => entry.CategoryId);

        return CategoryTree.RowsInDisplayOrder(categories)
            .Select(row =>
            {
                counts.TryGetValue(row.Category.CategoryId, out var count);

                return new AdminCategoryListDto
                {
                    CategoryId = row.Category.CategoryId,
                    CategoryName = row.Category.CategoryName,
                    ParentCategoryId = row.Category.ParentCategoryId,
                    ParentCategoryName = row.Category.ParentCategoryName,
                    Depth = row.Depth,
                    ProductCount = count?.Products ?? 0,
                    ChildCount = count?.Children ?? 0
                };
            })
            .ToList();
    }

    /// <summary>
    /// Every category of the table, flat, with the name of the parent each one names - the list every rule of
    /// <see cref="CategoryCatalog"/> is asked of, and the one the product form's checkbox list is ordered from.
    /// </summary>
    private async Task<List<AdminCategoryDto>> AllCategoriesAsync()
    {
        return await _context.Categories
            .Select(c => new AdminCategoryDto
            {
                CategoryId = c.CategoryId,
                CategoryName = c.CategoryName,
                ParentCategoryId = c.ParentCategoryId,
                ParentCategoryName = c.ParentCategory != null ? c.ParentCategory.CategoryName : null
            })
            .ToListAsync();
    }

    /// <summary>
    /// Adds a category: a heading when it names no parent, a shelf of one when it does. The name and the place are put
    /// to <see cref="CategoryCatalog"/> before anything is written, and its sentence is what comes back when either is
    /// refused - so a screen that shows the message says why, and the row is not written at all.
    ///
    /// The id is the table's own (CategoryID is an identity column, as ProductID is), which is why nothing sets it
    /// here: two admins adding a shelf at the same moment cannot collide on one.
    /// </summary>
    public async Task<AdminResultDto> CreateCategoryAsync(CategoryFormRequest request, long currentUserId)
    {
        var name = (request.CategoryName ?? "").Trim();

        var problem = CatalogueProblem(name, request.ParentCategoryId, null, await AllCategoriesAsync());
        if (problem != null)
            return Error(problem);

        _context.Categories.Add(new Category
        {
            CategoryName = name,
            ParentCategoryId = request.ParentCategoryId
        });

        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { $"Category '{name}' created successfully." }
        };
    }

    /// <summary>
    /// Renames a category and moves it, its own shelves travelling with it - the same body as
    /// <see cref="CreateCategoryAsync"/> and the same rules, with the row's own id given so that the name it already
    /// carries and the parent it already sits under are not read as clashes with itself (see
    /// <see cref="CategoryCatalog.SiblingProblem"/> and <see cref="CategoryCatalog.ParentProblem"/>).
    /// </summary>
    public async Task<AdminResultDto> UpdateCategoryAsync(short categoryId, CategoryFormRequest request, long currentUserId)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == categoryId);
        if (category == null)
            return Error("Category not found.");

        var name = (request.CategoryName ?? "").Trim();

        var problem = CatalogueProblem(name, request.ParentCategoryId, categoryId, await AllCategoriesAsync());
        if (problem != null)
            return Error(problem);

        category.CategoryName = name;
        category.ParentCategoryId = request.ParentCategoryId;

        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { $"Category '{name}' updated successfully." }
        };
    }

    /// <summary>
    /// Takes a category out, once <see cref="CategoryCatalog.DeleteProblem"/> has nothing left to say about it: no
    /// category sits directly under it and no product is filed on it, switched on or off. The count of links is read
    /// from 'ProductCategories' - the same rows the rule is asked about - so the answer the admin was given and the
    /// delete that follows cannot disagree.
    /// </summary>
    public async Task<AdminResultDto> DeleteCategoryAsync(short categoryId, long currentUserId)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == categoryId);
        if (category == null)
            return Error("Category not found.");

        var productsFiled = await _context.ProductCategories.CountAsync(pc => pc.CategoryId == categoryId);

        var problem = CategoryCatalog.DeleteProblem(categoryId, await AllCategoriesAsync(), productsFiled);
        if (problem != null)
            return Error(problem);

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { $"Category '{category.CategoryName}' deleted successfully." }
        };
    }

    /// <summary>
    /// The first thing wrong with the name and the place an admin chose for a category, or null when neither has
    /// anything wrong: the three rules of <see cref="CategoryCatalog"/>, in the order the screen reads them - the name
    /// itself, a name already used under that parent, and last the parent, which is the one edit that can put a branch
    /// beyond the reach of the catalogue.
    /// </summary>
    /// <param name="name">The name as the admin typed it; trimmed by the rules, as the write trims it.</param>
    /// <param name="parentCategoryId">Where the category is being put - null for a heading.</param>
    /// <param name="categoryId">The row being edited, so it does not clash with what it already is; null when the row is being created.</param>
    /// <param name="categories">Every category of the table, flat.</param>
    private static string? CatalogueProblem(
        string name,
        short? parentCategoryId,
        short? categoryId,
        List<AdminCategoryDto> categories)
    {
        return CategoryCatalog.NameProblem(name)
            ?? CategoryCatalog.SiblingProblem(name, parentCategoryId, categoryId, categories)
            ?? CategoryCatalog.ParentProblem(parentCategoryId, categoryId, categories);
    }

}
