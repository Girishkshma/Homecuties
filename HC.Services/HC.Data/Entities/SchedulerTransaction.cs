using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class SchedulerTransaction
{
    public long ScheduleTransactionId { get; set; }

    public int ScheduleId { get; set; }

    public DateTime ScheduleStartedOn { get; set; }

    public DateTime? ScheduleCompletedOn { get; set; }

    /// <summary>
    /// Statuses : &apos;S&apos; : Started, &apos;E&apos; : Executing, &apos;F&apos; : Failed, &apos;C&apos; : Completed
    /// </summary>
    public string ScheduleTransactionStatus { get; set; } = null!;

    public virtual Scheduler Schedule { get; set; } = null!;
}
