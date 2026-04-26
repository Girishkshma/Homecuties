using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class ImageType
{
    public short ImageTypeId { get; set; }

    public string ImageTypeName { get; set; } = null!;

    public string ShortCode { get; set; } = null!;

    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();
}
