using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class PurchaseStatusCompatibility
{
    public short RelationId { get; set; }

    public short PurchaseStatusId { get; set; }

    public short PurchaseNewStatusId { get; set; }

    public bool IsActive { get; set; }

    public virtual PurchaseStatus PurchaseNewStatus { get; set; } = null!;

    public virtual PurchaseStatus PurchaseStatus { get; set; } = null!;
}
