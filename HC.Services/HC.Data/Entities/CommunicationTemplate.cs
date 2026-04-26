using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class CommunicationTemplate
{
    public short CommunicationTemplateId { get; set; }

    public string TemplateName { get; set; } = null!;

    public string? TemplateDescription { get; set; }

    public string? Query { get; set; }

    public string? Htmlpath { get; set; }

    public string? XslPath { get; set; }

    public bool? IsActive { get; set; }

    public virtual ICollection<CommunicationType> CommunicationTypes { get; set; } = new List<CommunicationType>();
}
