using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class UserCommunicationParameter
{
    public long UserCommunicationId { get; set; }

    public int CommunicationTypeParameterId { get; set; }

    public string? UserCommunicationParameterValue { get; set; }

    public virtual CommunicationTypeParameter CommunicationTypeParameter { get; set; } = null!;

    public virtual UserCommunication UserCommunication { get; set; } = null!;
}
