using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class PurchaseStatus
{
    public short PurchaseStatusId { get; set; }

    public string PurchaseStatusName { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual ICollection<PurchaseStatusCompatibility> PurchaseStatusCompatibilityPurchaseNewStatuses { get; set; } = new List<PurchaseStatusCompatibility>();

    public virtual ICollection<PurchaseStatusCompatibility> PurchaseStatusCompatibilityPurchaseStatuses { get; set; } = new List<PurchaseStatusCompatibility>();

    public virtual ICollection<PurchaseStatusUserRoleCompatibility> PurchaseStatusUserRoleCompatibilities { get; set; } = new List<PurchaseStatusUserRoleCompatibility>();

    public virtual ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
}
