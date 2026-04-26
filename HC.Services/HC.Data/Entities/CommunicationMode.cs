using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class CommunicationMode
{
    public short CommunicationModeId { get; set; }

    public string CommunicationMode1 { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual ICollection<CommunicationType> CommunicationTypes { get; set; } = new List<CommunicationType>();
}
