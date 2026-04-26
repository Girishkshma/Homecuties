using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class Vendor
{
    public short VendorId { get; set; }

    public string VendorName { get; set; } = null!;

    public string? VendorAddress { get; set; }

    public string Mobile { get; set; } = null!;

    public string? Remarks { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();

    public virtual ICollection<VendorsUser> VendorsUsers { get; set; } = new List<VendorsUser>();
}
