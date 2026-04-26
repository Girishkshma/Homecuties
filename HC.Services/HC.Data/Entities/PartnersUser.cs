using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class PartnersUser
{
    public int PartnerUserId { get; set; }

    public int PartnerId { get; set; }

    public long UserId { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime AddedOn { get; set; }

    public bool IsActive { get; set; }

    public virtual Partner Partner { get; set; } = null!;

    public virtual ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();

    public virtual User User { get; set; } = null!;
}
