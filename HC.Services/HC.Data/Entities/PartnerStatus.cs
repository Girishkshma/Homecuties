using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class PartnerStatus
{
    public short PartnerStatusId { get; set; }

    public string PartnerStatus1 { get; set; } = null!;

    public virtual ICollection<Partner> Partners { get; set; } = new List<Partner>();
}
