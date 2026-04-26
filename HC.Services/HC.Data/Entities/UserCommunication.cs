using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class UserCommunication
{
    public long UserCommunicationId { get; set; }

    public long UserId { get; set; }

    public short CommunicationTypeId { get; set; }

    public DateTime AddedOn { get; set; }

    public DateTime? CommunicationCreatedOn { get; set; }

    public virtual CommunicationType CommunicationType { get; set; } = null!;

    public virtual User User { get; set; } = null!;

    public virtual ICollection<UserCommunicationParameter> UserCommunicationParameters { get; set; } = new List<UserCommunicationParameter>();

    public virtual ICollection<UserCommunicationsDatum> UserCommunicationsData { get; set; } = new List<UserCommunicationsDatum>();
}
