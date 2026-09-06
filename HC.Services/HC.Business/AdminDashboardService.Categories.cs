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

    public async Task<List<AdminCategoryDto>> GetCategoryTreeAsync()
    {
        var categories = await _context.Categories
            .Select(c => new AdminCategoryDto
            {
                CategoryId = c.CategoryId,
                CategoryName = c.CategoryName,
                ParentCategoryId = c.ParentCategoryId,
                ParentCategoryName = c.ParentCategory != null ? c.ParentCategory.CategoryName : null
            })
            .ToListAsync();

        return categories;
    }


}
