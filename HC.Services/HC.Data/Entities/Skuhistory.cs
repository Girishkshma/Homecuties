using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class Skuhistory
{
    public long HistoryId { get; set; }

    public string Sku { get; set; } = null!;

    public int InventoryId { get; set; }

    public short SkustatusId { get; set; }

    public DateTime HistoryDate { get; set; }

    public virtual Sku SkuNavigation { get; set; } = null!;
}
