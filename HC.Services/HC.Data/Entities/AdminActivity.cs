using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class AdminActivity
{
    public short ActivityId { get; set; }

    public string ActivityTitle { get; set; } = null!;

    public short MenuId { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<AdminActivitiesRole> AdminActivitiesRoles { get; set; } = new List<AdminActivitiesRole>();

    public virtual AdminMenu Menu { get; set; } = null!;
}
