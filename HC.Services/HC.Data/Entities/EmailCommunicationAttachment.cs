using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class EmailCommunicationAttachment
{
    public long EmailCommunicationAttachmentId { get; set; }

    public long EmailCommunicationId { get; set; }

    public string Attachment { get; set; } = null!;

    public virtual EmailCommunication EmailCommunication { get; set; } = null!;
}
