using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class Inventory
{
    public int InventoryId { get; set; }

    public string InventoryName { get; set; } = null!;

    public int PartnerId { get; set; }

    public short InventoryStatusId { get; set; }

    public bool IsDefault { get; set; }

    public virtual InventoryStatus InventoryStatus { get; set; } = null!;

    public virtual Partner Partner { get; set; } = null!;

    public virtual ICollection<Sku> Skus { get; set; } = new List<Sku>();
}
