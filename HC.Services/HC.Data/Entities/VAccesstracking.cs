using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class VAccesstracking
{
    public string Ip { get; set; } = null!;

    public string Url { get; set; } = null!;

    public DateTime AccessOn { get; set; }

    public string? YearMonth { get; set; }

    public string? City { get; set; }

    public string? Region { get; set; }

    public string? Country { get; set; }

    public string? Organization { get; set; }
}
