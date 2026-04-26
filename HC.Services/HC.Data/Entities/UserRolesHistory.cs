using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class UserRolesHistory
{
    public long UserRoleHistoryId { get; set; }

    public long UserId { get; set; }

    public short RoleId { get; set; }

    public short UserRoleStatusId { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public virtual Role Role { get; set; } = null!;

    public virtual User User { get; set; } = null!;

    public virtual UserRoleStatus UserRoleStatus { get; set; } = null!;
}
