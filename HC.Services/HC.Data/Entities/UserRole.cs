using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class UserRole
{
    public long UserId { get; set; }

    public short RoleId { get; set; }

    public bool IsActive { get; set; }

    public virtual Role Role { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
