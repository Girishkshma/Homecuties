using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class InventoryStatus
{
    public short InventoryStatusId { get; set; }

    public string InventoryStatus1 { get; set; } = null!;

    public virtual ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
}
