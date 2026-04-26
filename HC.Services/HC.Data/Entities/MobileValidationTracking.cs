using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class MobileValidationTracking
{
    public string MobileNumber { get; set; } = null!;

    public string? Otp { get; set; }

    public DateTime? DateCreated { get; set; }
}
