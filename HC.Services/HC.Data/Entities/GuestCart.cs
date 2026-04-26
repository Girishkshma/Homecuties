using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class GuestCart
{
    public long CartId { get; set; }

    public long CustomerId { get; set; }

    public DateTime CreatedOn { get; set; }

    public virtual GuestCustomer Customer { get; set; } = null!;

    public virtual ICollection<GuestCartItem> GuestCartItems { get; set; } = new List<GuestCartItem>();
}
