using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class AdminMenu
{
    public short MenuId { get; set; }

    public string MenuTitle { get; set; } = null!;

    public string? MenuDescription { get; set; }

    public string MenuUrl { get; set; } = null!;

    public short? ParentMenuId { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<AdminActivity> AdminActivities { get; set; } = new List<AdminActivity>();

    public virtual ICollection<AdminMenusRole> AdminMenusRoles { get; set; } = new List<AdminMenusRole>();

    public virtual ICollection<AdminMenu> InverseParentMenu { get; set; } = new List<AdminMenu>();

    public virtual AdminMenu? ParentMenu { get; set; }
}
