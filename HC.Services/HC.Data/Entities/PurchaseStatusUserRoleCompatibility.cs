using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class PurchaseStatusUserRoleCompatibility
{
    public short RelationId { get; set; }

    public short PurchaseStatusId { get; set; }

    public short UserRoleId { get; set; }

    public bool IsActive { get; set; }

    public virtual PurchaseStatus PurchaseStatus { get; set; } = null!;

    public virtual Role UserRole { get; set; } = null!;
}
