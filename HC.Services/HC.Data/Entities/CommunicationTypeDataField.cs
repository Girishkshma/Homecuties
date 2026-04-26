using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class CommunicationTypeDataField
{
    public int CommunicationTypeDataFieldId { get; set; }

    public short CommunicationTypeId { get; set; }

    public string DataFieldName { get; set; } = null!;

    public string DataFieldDisplayName { get; set; } = null!;

    public short DataFieldSizeLimit { get; set; }

    public byte SortOrder { get; set; }

    public virtual CommunicationType CommunicationType { get; set; } = null!;

    public virtual ICollection<UserCommunicationsDatum> UserCommunicationsData { get; set; } = new List<UserCommunicationsDatum>();
}
