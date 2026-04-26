using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class VendorsUser
{
    public long VendorUserId { get; set; }

    public short VendorId { get; set; }

    public long UserId { get; set; }

    public DateTime AddedOn { get; set; }

    public bool IsActive { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Vendor Vendor { get; set; } = null!;
}
