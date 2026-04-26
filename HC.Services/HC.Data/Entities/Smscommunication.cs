using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class Smscommunication
{
    public long SmscommunicationId { get; set; }

    public string FromConfigName { get; set; } = null!;

    public string MobileNumber { get; set; } = null!;

    public string Message { get; set; } = null!;

    public DateTime AddedOn { get; set; }

    public DateTime? SentOn { get; set; }

    public short SmscommunicationStatusId { get; set; }

    public virtual SmscommuncationStatus SmscommunicationStatus { get; set; } = null!;
}
