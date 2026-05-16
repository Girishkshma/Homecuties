using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace HC.Business;

public class AdminAuthService : IAdminAuthService
{
    private readonly HomecutiesDbContext _context;

    public AdminAuthService(HomecutiesDbContext context)
    {
        _context = context;
    }

    public async Task<AdminLoginResponse> LoginAsync(string loginId, string password)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.LoginId == loginId && u.IsActive == true);

        if (user == null)
        {
            return new AdminLoginResponse
            {
                Result = 0,
                Messages = new[] { "Invalid login credentials." }
            };
        }

        if (user.Password != password)
        {
            return new AdminLoginResponse
            {
                Result = 0,
                Messages = new[] { "Invalid login credentials." }
            };
        }

        var token = GenerateToken(user.UserId, user.LoginId, "");

        return new AdminLoginResponse
        {
            Result = 1,
            Messages = new[] { "Login successful." },
            Token = token,
            ExpiresOn = DateTime.UtcNow.AddDays(1),
            User = new AdminUserDto
            {
                UserId = user.UserId,
                LoginId = user.LoginId,
                FirstName = user.FirstName,
                MiddleName = user.MiddleName,
                LastName = user.LastName,
                EmailId = user.EmailId,
                MobileNumber = user.MobileNumber,
                IsActive = user.IsActive ?? false,
                Roles = user.UserRoles
                    .Where(ur => ur.IsActive)
                    .Select(ur => new AdminRoleDto
                    {
                        RoleId = ur.Role.RoleId,
                        RoleName = ur.Role.RoleName,
                        RoleDescription = ur.Role.RoleDescription
                    }).ToList()
            }
        };
    }

    public string GenerateToken(long userId, string loginId, string ipAddress)
    {
        var payload = JsonSerializer.Serialize(new
        {
            UserId = userId,
            LoginId = loginId,
            IP = ipAddress,
            Exp = DateTime.UtcNow.AddDays(1).Ticks
        });

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    public AdminUserDto? ValidateToken(string token)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            if (!data.TryGetProperty("UserId", out var userIdProp))
                return null;

            if (!data.TryGetProperty("Exp", out var expProp))
                return null;

            var expTicks = expProp.GetInt64();
            if (new DateTime(expTicks) < DateTime.UtcNow)
                return null;

            var userId = userIdProp.GetInt64();
            var user = _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefault(u => u.UserId == userId && u.IsActive == true);

            if (user == null)
                return null;

            return new AdminUserDto
            {
                UserId = user.UserId,
                LoginId = user.LoginId,
                FirstName = user.FirstName,
                MiddleName = user.MiddleName,
                LastName = user.LastName,
                EmailId = user.EmailId,
                MobileNumber = user.MobileNumber,
                IsActive = user.IsActive ?? false,
                Roles = user.UserRoles
                    .Where(ur => ur.IsActive)
                    .Select(ur => new AdminRoleDto
                    {
                        RoleId = ur.Role.RoleId,
                        RoleName = ur.Role.RoleName,
                        RoleDescription = ur.Role.RoleDescription
                    }).ToList()
            };
        }
        catch
        {
            return null;
        }
    }

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
