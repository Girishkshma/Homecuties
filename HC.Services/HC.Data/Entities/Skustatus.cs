using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class Skustatus
{
    public short SkustatusId { get; set; }

    public string Skustatus1 { get; set; } = null!;

    public virtual ICollection<Sku> Skus { get; set; } = new List<Sku>();
}
