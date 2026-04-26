using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class AdminActivitiesRole
{
    public short ActivityId { get; set; }

    public short RoleId { get; set; }

    public bool IsActive { get; set; }

    public virtual AdminActivity Activity { get; set; } = null!;

    public virtual Role Role { get; set; } = null!;
}
