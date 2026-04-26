using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class EmailValidationTracking
{
    public string EmailId { get; set; } = null!;

    public string? Otp { get; set; }

    public DateTime? Datecreated { get; set; }
}
