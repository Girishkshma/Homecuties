using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class Purchase
{
    public long PurchaseId { get; set; }

    public short VendorId { get; set; }

    public int PurchaserId { get; set; }

    public DateTime PurchaseDate { get; set; }

    public string? InvoicePath { get; set; }

    public long AddedBy { get; set; }

    public DateTime AddedOn { get; set; }

    public long LastModifiedBy { get; set; }

    public DateTime LastModifiedOn { get; set; }

    public short PurchaseStatusId { get; set; }

    public virtual User LastModifiedByNavigation { get; set; } = null!;

    public virtual ICollection<PurchaseComment> PurchaseComments { get; set; } = new List<PurchaseComment>();

    public virtual ICollection<PurchaseDetail> PurchaseDetails { get; set; } = new List<PurchaseDetail>();

    public virtual PurchaseStatus PurchaseStatus { get; set; } = null!;

    public virtual PartnersUser Purchaser { get; set; } = null!;

    public virtual Vendor Vendor { get; set; } = null!;
}
