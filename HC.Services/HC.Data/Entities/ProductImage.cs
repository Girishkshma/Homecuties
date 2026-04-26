using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class ProductImage
{
    public long ProductImageId { get; set; }

    public int ProductId { get; set; }

    public short ImageTypeId { get; set; }

    public int ImageIndex { get; set; }

    public string ImageUrl { get; set; } = null!;

    public bool IsPromoImage { get; set; }

    public bool IsActive { get; set; }

    public virtual ImageType ImageType { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
