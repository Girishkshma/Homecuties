using HC.Business.Security;
using HC.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HC.Services.Authorization;

/// <summary>
/// Authorizes an admin request against the menus (sections) the admin's roles are mapped to in
/// 'AdminMenusRoles'. The roles are read from the validated JWT, so revoking a menu from a role in
/// the database takes effect on the admin's very next request (no new login needed).
/// </summary>
public sealed class AdminMenuAccessHandler : AuthorizationHandler<AdminMenuAccessRequirement>
{
    private readonly HomecutiesDbContext _context;

    public AdminMenuAccessHandler(HomecutiesDbContext context)
    {
        _context = context;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminMenuAccessRequirement requirement)
    {
        // No role claim at all means the token cannot open any section of the admin area.
        var roleIds = AdminJwtTokenService.GetRoleIds(context.User);
        if (roleIds.Count == 0)
            return;

        var sections = requirement.Sections;

        var granted = await _context.AdminMenusRoles
            .AnyAsync(amr => amr.IsActive &&
                             amr.Role.IsActive &&
                             amr.Menu.IsActive &&
                             roleIds.Contains(amr.RoleId) &&
                             (sections.Count == 0 || sections.Contains(amr.Menu.MenuUrl)));

        if (granted)
            context.Succeed(requirement);
    }
}
