using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class GuestCustomer
{
    public long CustomerId { get; set; }

    public DateTime CreatedOn { get; set; }

    public virtual ICollection<GuestCart> GuestCarts { get; set; } = new List<GuestCart>();
}
