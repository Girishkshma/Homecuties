using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class UserRoleStatus
{
    public short UserRoleStatusId { get; set; }

    public string UserRoleStatus1 { get; set; } = null!;

    public virtual ICollection<UserRolesHistory> UserRolesHistories { get; set; } = new List<UserRolesHistory>();
}
