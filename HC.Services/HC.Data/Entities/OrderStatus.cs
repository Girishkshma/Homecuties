using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class OrderStatus
{
    public short OrderStatusId { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
