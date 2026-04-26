using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class WishList
{
    public long CustomerId { get; set; }

    public int ProductId { get; set; }

    public DateTime AddedOn { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
