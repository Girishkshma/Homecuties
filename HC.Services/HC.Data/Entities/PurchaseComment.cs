using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class PurchaseComment
{
    public long PurchaseCommentId { get; set; }

    public long PurchaseId { get; set; }

    public string Comments { get; set; } = null!;

    public long AddedBy { get; set; }

    public DateTime AddedOn { get; set; }

    public virtual User AddedByNavigation { get; set; } = null!;

    public virtual Purchase Purchase { get; set; } = null!;
}
