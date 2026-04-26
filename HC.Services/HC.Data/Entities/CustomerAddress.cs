using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class CustomerAddress
{
    public long AddressId { get; set; }

    public long CustomerId { get; set; }

    public string AddressTitle { get; set; } = null!;

    public string ContactName { get; set; } = null!;

    public string AddressLine1 { get; set; } = null!;

    public string? AddressLine2 { get; set; }

    public string City { get; set; } = null!;

    public string State { get; set; } = null!;

    public string Country { get; set; } = null!;

    public string Zipcode { get; set; } = null!;

    public string MobileNumber { get; set; } = null!;

    public string? EmailId { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<Order> OrderBillingAddresses { get; set; } = new List<Order>();

    public virtual ICollection<Order> OrderShippingAddresses { get; set; } = new List<Order>();
}
