using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class UserRelation
{
    public short UserRelationId { get; set; }

    public string UserRelationName { get; set; } = null!;

    public string? UserRelationDescription { get; set; }
}
