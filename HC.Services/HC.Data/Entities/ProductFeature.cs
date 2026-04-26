using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class ProductFeature
{
    public long ProductFeatureId { get; set; }

    public int ProductId { get; set; }

    public string ProductFeature1 { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual Product Product { get; set; } = null!;
}
