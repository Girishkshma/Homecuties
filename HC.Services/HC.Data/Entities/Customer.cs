using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class Customer
{
    public long CustomerId { get; set; }

    public string FirstName { get; set; } = null!;

    public string? MiddleName { get; set; }

    public string? LastName { get; set; }

    public string? MobileIsd { get; set; }

    public string? MobileNumber { get; set; }

    public bool? MobileVerified { get; set; }

    public string EmailId { get; set; } = null!;

    public bool EmailVerfied { get; set; }

    public string? Password { get; set; }

    public string? GooglePayLoad { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime ModifiedOn { get; set; }

    public short CustomerStatusId { get; set; }

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<CustomerAddress> CustomerAddresses { get; set; } = new List<CustomerAddress>();

    public virtual CustomerStatus CustomerStatus { get; set; } = null!;

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<WishList> WishLists { get; set; } = new List<WishList>();
}
