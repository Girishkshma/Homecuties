using Microsoft.AspNetCore.Authorization;

namespace HC.Services.Authorization;

/// <summary>
/// Requires the signed-in admin to hold at least one role that is mapped - in 'AdminMenusRoles' -
/// to one of the given admin area sections (the 'MenuURL' values, e.g. '/orders').
///
/// With no section at all the requirement is satisfied by access to any one section, which is what
/// the dashboard/sidebar need. The role ids come from the validated JWT, so the database is the only
/// place where menu access is administrated.
/// </summary>
public sealed class AdminMenuAccessRequirement : IAuthorizationRequirement
{
    public AdminMenuAccessRequirement(params string[] sectionMenuUrls)
    {
        Sections = sectionMenuUrls ?? Array.Empty<string>();
    }

    /// <summary>Menu URLs of which at least one has to be granted to the admin's roles.</summary>
    public IReadOnlyList<string> Sections { get; }
}
