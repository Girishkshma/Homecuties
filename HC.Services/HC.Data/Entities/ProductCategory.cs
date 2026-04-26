using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class ProductCategory
{
    public int ProductId { get; set; }

    public short CategoryId { get; set; }

    public bool IsActive { get; set; }

    public virtual Category Category { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
