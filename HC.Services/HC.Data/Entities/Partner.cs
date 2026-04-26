using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class Partner
{
    public int PartnerId { get; set; }

    public string PartnerName { get; set; } = null!;

    public short PartnerStatusId { get; set; }

    public long LastModifiedBy { get; set; }

    public DateTime LastModifiedOn { get; set; }

    public virtual ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual PartnerStatus PartnerStatus { get; set; } = null!;

    public virtual ICollection<PartnersUser> PartnersUsers { get; set; } = new List<PartnersUser>();
}
