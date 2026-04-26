using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class AccessTracking
{
    public long Id { get; set; }

    public string Ip { get; set; } = null!;

    public string Url { get; set; } = null!;

    public DateTime AccessOn { get; set; }

    public string? City { get; set; }

    public string? Region { get; set; }

    public string? Country { get; set; }

    public string? Organization { get; set; }
}
