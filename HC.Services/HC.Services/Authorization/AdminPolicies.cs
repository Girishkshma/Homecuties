namespace HC.Services.Authorization;

/// <summary>
/// Names of the authorization policies used by the admin API.
///
/// Beside <see cref="AdminArea"/> (any section), every policy is named after the menu URL(s) it
/// grants - 'Section:/orders' or 'Section:/products|/users' - and the requirement is built from the
/// very same string, so a policy can never drift away from the sections it is supposed to check.
/// </summary>
public static class AdminPolicies
{
    public const string SectionPrefix = "Section:";

    /// <summary>Any authenticated admin that has access to at least one section of the admin area.</summary>
    public const string AdminArea = "AdminArea";

    // One policy per section, named after the menu URL it grants (the names are compile-time
    // constants so they can be used in '[Authorize(Policy = ...)]').
    public const string Products = SectionPrefix + "/products";
    public const string Orders = SectionPrefix + "/orders";
    public const string Customers = SectionPrefix + "/customers";
    public const string Partners = SectionPrefix + "/partners";
    public const string Vendors = SectionPrefix + "/vendors";
    public const string Purchases = SectionPrefix + "/purchases";
    public const string AdminUsers = SectionPrefix + "/users";
    /// <summary>The category list is used by the product form and by the admin user form.</summary>
    public const string ProductsOrAdminUsers = SectionPrefix + "/products|/users";

    /// <summary>
    /// The shop's money screens - the Finance screen, and the one action that keeps it honest (pulling the
    /// gateway's settlement books from the Dashboard). Both read the same rows the order screens act on
    /// (payments, refunds, what the gateway kept), so a role that may open either section may read the books;
    /// the Finance menu can also be granted on its own once it is seeded (AddFinanceMenu.sql), which is what the
    /// '/finance' half of the name is for.
    /// </summary>
    public const string OrdersOrFinance = SectionPrefix + "/orders|/finance";

    /// <summary>Every section policy the API registers, mirroring the menus mapped in 'AdminMenusRoles'.</summary>
    public static readonly string[] SectionPolicies =
    {
        Products,
        Orders,
        Customers,
        Partners,
        Vendors,
        Purchases,
        AdminUsers,
        ProductsOrAdminUsers,
        OrdersOrFinance
    };

    /// <summary>Policy granting access when the admin's role may open any one of the given menu URLs.</summary>
    public static string ForSections(params string[] menuUrls) => SectionPrefix + string.Join("|", menuUrls);

    /// <summary>Menu URLs carried by a policy name (the part after 'Section:', split on '|').</summary>
    public static string[] SectionsOf(string policyName)
    {
        if (!policyName.StartsWith(SectionPrefix, StringComparison.OrdinalIgnoreCase))
            return Array.Empty<string>();

        return policyName[SectionPrefix.Length..]
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
