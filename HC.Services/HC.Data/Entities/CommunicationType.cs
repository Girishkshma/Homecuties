using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class CommunicationType
{
    public short CommunicationTypeId { get; set; }

    public string CommunicationType1 { get; set; } = null!;

    public short CommunicationModeId { get; set; }

    public short CommunicationTemplateId { get; set; }

    public bool AllowSelection { get; set; }

    public virtual CommunicationMode CommunicationMode { get; set; } = null!;

    public virtual CommunicationTemplate CommunicationTemplate { get; set; } = null!;

    public virtual ICollection<CommunicationTypeDataField> CommunicationTypeDataFields { get; set; } = new List<CommunicationTypeDataField>();

    public virtual ICollection<CommunicationTypeParameter> CommunicationTypeParameters { get; set; } = new List<CommunicationTypeParameter>();

    public virtual ICollection<UserCommunication> UserCommunications { get; set; } = new List<UserCommunication>();
}
