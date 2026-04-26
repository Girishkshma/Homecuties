using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class CartItem
{
    public long CartId { get; set; }

    public int ProductId { get; set; }

    public short Quantity { get; set; }

    public virtual Cart Cart { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
