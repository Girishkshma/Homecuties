using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class Parameter
{
    public int ParameterId { get; set; }

    public string ParameterName { get; set; } = null!;

    public string ParameterValue { get; set; } = null!;

    public bool IsActive { get; set; }
}
