namespace HC.Business.Dtos;

/// <summary>
/// One stored image row of a product, as the gallery reads it: which image type it is, which photo of the product it
/// is (the admin's 'Image Index') and the file name it is served under.
///
/// It exists because a product's rows alone do not say which file a screen should draw - a photo is stored in
/// several sizes and the older products were written before all of them existed (see <see cref="ProductGallery"/>,
/// which is what decides).
/// </summary>
public class ProductImageRefDto
{
    /// <summary>The 'ImageTypes' id of the size this row holds.</summary>
    public short ImageTypeId { get; set; }

    /// <summary>
    /// Which photo of the product this row is: 1 for the first photo the admin uploaded, 2 for the second, and so on.
    /// A photo's several sizes share an index, which is what lets a screen show one photo in two sizes at once.
    /// </summary>
    public int ImageIndex { get; set; }

    /// <summary>The file name the row holds ('I10040_S_01.jpg'), as it sits in the shop's image folder.</summary>
    public string ImageUrl { get; set; } = "";
}
