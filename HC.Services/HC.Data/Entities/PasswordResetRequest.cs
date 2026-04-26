using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class PasswordResetRequest
{
    public long RequestId { get; set; }

    public string EmailId { get; set; } = null!;

    public string? Jwt { get; set; }

    public bool IsAdmin { get; set; }

    public DateTime AddedOn { get; set; }

    public DateTime? CommunicationSentOn { get; set; }
}
