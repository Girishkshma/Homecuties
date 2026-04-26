using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class AdminMenusRole
{
    public short MenuId { get; set; }

    public short RoleId { get; set; }

    public bool IsActive { get; set; }

    public virtual AdminMenu Menu { get; set; } = null!;

    public virtual Role Role { get; set; } = null!;
}
