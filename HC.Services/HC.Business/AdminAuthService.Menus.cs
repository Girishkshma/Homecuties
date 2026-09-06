// ============================================================
// AdminAuthService.Menus.cs
// Partial class: AdminAuthService - Menus operations
// ============================================================

using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HC.Business;

public partial class AdminAuthService : IAdminAuthService
{
    public async Task<List<AdminMenuDto>> GetMenusByRoleAsync(short roleId)
    {
        var menus = await _context.AdminMenusRoles
            .Where(amr => amr.RoleId == roleId && amr.IsActive)
            .Include(amr => amr.Menu)
                .ThenInclude(m => m.InverseParentMenu)
            .Include(amr => amr.Menu)
                .ThenInclude(m => m.AdminActivities.Where(a => a.IsActive))
            .Select(amr => amr.Menu)
            .ToListAsync();

        return BuildMenuTree(menus.Where(m => m.ParentMenuId == null).ToList(), menus.ToList());
    }

    public async Task<List<AdminMenuDto>> GetAllMenusAsync()
    {
        var menus = await _context.AdminMenus
            .Include(m => m.InverseParentMenu)
            .Include(m => m.AdminActivities.Where(a => a.IsActive))
            .Where(m => m.IsActive)
            .ToListAsync();

        return BuildMenuTree(menus.Where(m => m.ParentMenuId == null).ToList(), menus);
    }

    private List<AdminMenuDto> BuildMenuTree(List<AdminMenu> parentMenus, List<AdminMenu> allMenus)
    {
        var result = new List<AdminMenuDto>();

        foreach (var menu in parentMenus.OrderBy(m => m.MenuId))
        {
            var dto = new AdminMenuDto
            {
                MenuId = menu.MenuId,
                MenuTitle = menu.MenuTitle,
                MenuDescription = menu.MenuDescription,
                MenuUrl = menu.MenuUrl,
                ParentMenuId = menu.ParentMenuId,
                IsActive = menu.IsActive,
                Activities = menu.AdminActivities.Select(a => new AdminActivityDto
                {
                    ActivityId = a.ActivityId,
                    ActivityTitle = a.ActivityTitle,
                    MenuId = a.MenuId,
                    IsActive = a.IsActive
                }).ToList(),
                Children = BuildMenuTree(
                    allMenus.Where(m => m.ParentMenuId == menu.MenuId).ToList(),
                    allMenus)
            };

            result.Add(dto);
        }

        return result;
    }
}
