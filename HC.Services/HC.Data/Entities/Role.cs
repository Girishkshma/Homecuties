using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class Role
{
    public short RoleId { get; set; }

    public string RoleName { get; set; } = null!;

    public string? RoleDescription { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<AdminActivitiesRole> AdminActivitiesRoles { get; set; } = new List<AdminActivitiesRole>();

    public virtual ICollection<AdminMenusRole> AdminMenusRoles { get; set; } = new List<AdminMenusRole>();

    public virtual ICollection<PurchaseStatusUserRoleCompatibility> PurchaseStatusUserRoleCompatibilities { get; set; } = new List<PurchaseStatusUserRoleCompatibility>();

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    public virtual ICollection<UserRolesHistory> UserRolesHistories { get; set; } = new List<UserRolesHistory>();
}
