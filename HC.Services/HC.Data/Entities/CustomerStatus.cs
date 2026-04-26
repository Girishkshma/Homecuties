using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class CustomerStatus
{
    public short CustomerStatusId { get; set; }

    public string CustomerStatus1 { get; set; } = null!;

    public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();
}
