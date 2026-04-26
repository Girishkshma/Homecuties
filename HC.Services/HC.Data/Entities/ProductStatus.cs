using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class ProductStatus
{
    public short ProductStatusId { get; set; }

    public string ProductStatusName { get; set; } = null!;

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
