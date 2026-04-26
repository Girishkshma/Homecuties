using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class CommunicationTypeParameter
{
    public int CommunicationTypeParameterId { get; set; }

    public short CommunicationTypeId { get; set; }

    public string CommunicationTypeParameterName { get; set; } = null!;

    public string? CommunicationTypeParameterDescription { get; set; }

    public virtual CommunicationType CommunicationType { get; set; } = null!;

    public virtual ICollection<UserCommunicationParameter> UserCommunicationParameters { get; set; } = new List<UserCommunicationParameter>();
}
