using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class SmscommuncationStatus
{
    public short SmscommunicationStatusId { get; set; }

    public string SmscommunicationStatus { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual ICollection<Smscommunication> Smscommunications { get; set; } = new List<Smscommunication>();
}
