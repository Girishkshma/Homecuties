using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class OrderHistory
{
    public long HistoryId { get; set; }

    public long OrderId { get; set; }

    public DateTime HistoryDate { get; set; }

    public short OrderStatusId { get; set; }

    public string Comments { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
