using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class EmailCommunication
{
    public long EmailCommunicationId { get; set; }

    public string FromConfigName { get; set; } = null!;

    public string ToAddress { get; set; } = null!;

    public string? Ccaddress { get; set; }

    public string? Bccaddress { get; set; }

    public string SubjectLine { get; set; } = null!;

    public string Body { get; set; } = null!;

    public bool IsHtml { get; set; }

    public DateTime AddedOn { get; set; }

    public DateTime? SentOn { get; set; }

    public bool IsLocked { get; set; }

    public short EmailCommunicationStatusId { get; set; }

    public virtual ICollection<EmailCommunicationAttachment> EmailCommunicationAttachments { get; set; } = new List<EmailCommunicationAttachment>();

    public virtual ICollection<EmailCommunicationDetail> EmailCommunicationDetails { get; set; } = new List<EmailCommunicationDetail>();

    public virtual EmailCommunicationStatus EmailCommunicationStatus { get; set; } = null!;
}
