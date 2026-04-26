using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class UserCommunicationsDatum
{
    public long UserCommunicationId { get; set; }

    public int CommunicationTypeDataFieldId { get; set; }

    public string? UserCommunicationData { get; set; }

    public virtual CommunicationTypeDataField CommunicationTypeDataField { get; set; } = null!;

    public virtual UserCommunication UserCommunication { get; set; } = null!;
}
