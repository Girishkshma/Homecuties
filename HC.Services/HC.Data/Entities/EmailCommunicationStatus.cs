using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class EmailCommunicationStatus
{
    public short EmailCommunicationStatusId { get; set; }

    public string EmailCommunicationStatus1 { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual ICollection<EmailCommunication> EmailCommunications { get; set; } = new List<EmailCommunication>();
}
