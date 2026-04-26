using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class Cart
{
    public long CartId { get; set; }

    public long CustomerId { get; set; }

    public DateTime CreatedOn { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual Customer Customer { get; set; } = null!;
}
