using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class Scheduler
{
    public int ScheduleId { get; set; }

    public string ScheduleName { get; set; } = null!;

    public string? ScheduleDescription { get; set; }

    /// <summary>
    /// valid name of the assembly to load (with physical path if necessory)
    /// </summary>
    public string AssemblyToLoad { get; set; } = null!;

    /// <summary>
    /// Fully Qualified Name of the Class to Load with in Assembly to Load
    /// </summary>
    public string ClassToLoad { get; set; } = null!;

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTill { get; set; }

    /// <summary>
    /// &apos;Y&apos; - Year, &apos;Q&apos; - Quarter Year, &apos;M&apos; - Month, &apos;W&apos; - Week, &apos;D&apos; - Day, &apos;H&apos; - Hour, &apos;I&apos; - minute, &apos;S&apos; - Second
    /// </summary>
    public string ScheduleExecutionUnit { get; set; } = null!;

    public short ScheduleExecutionFrequency { get; set; }

    public int FixedDate { get; set; }

    public int FixedHour { get; set; }

    public int FixedMinute { get; set; }

    public DateTime? NextExecutionOn { get; set; }

    public virtual ICollection<SchedulerTransaction> SchedulerTransactions { get; set; } = new List<SchedulerTransaction>();
}
