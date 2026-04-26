using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class EmailCommunicationDetail
{
    public long EmailCommunicationDetailId { get; set; }

    public long EmailCommunicationId { get; set; }

    public string Body { get; set; } = null!;

    public virtual EmailCommunication EmailCommunication { get; set; } = null!;
}
