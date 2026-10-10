using HC.Business.Dtos;

namespace HC.Business;

/// <summary>
/// Which of a product's stored images a screen draws, and from which size.
///
/// A product's stored rows cannot answer that on their own. A photo is written in several sizes, and the shop's older
/// products were written before all of those sizes existed: product 10040 has rows for all six types at index 1 (L,
/// S, R, M, T, P) and two more rows for S at indices 3 and 4, but only the S and P files are really in the folder -
/// a row is written down whether or not its file was ever produced. Drawing the rows as they stand is what made the
/// strip under the main frame show broken thumbnails and the arrows walk onto blank frames.
///
/// So the gallery is resolved from the rows <em>and</em> the folder: each photo (an <c>ImageIndex</c>) is drawn from
/// the best type that actually has a file, and a photo with no file at all is left out. The order the types are
/// preferred in is the order the storefront draws them, best first, and the two lists below are the whole policy:
///
/// <list type="bullet">
/// <item>A big frame takes the master size (L), then the square (S), then the promo (P).</item>
/// <item>The strip and the small rows take the thumbnail (T), then the square (S), then the promo (P).</item>
/// </list>
///
/// A photo uploaded today has all four of those files, so a big frame draws L and the strip draws T - the two sizes
/// the audit found the storefront actually renders (see <see cref="ProductImageVariants"/>). A photo from before -
/// only S and P on disk - draws its S in both, which is what the shop shows today, only without the rows that point
/// at nothing.
/// </summary>
public static class ProductGallery
{
    /// <summary>
    /// The types a big frame may be drawn from, best first: the master size, the square, the promo. A new photo is
    /// drawn from its L; an older one, whose L was never produced, from its S.
    /// </summary>
    public static readonly IReadOnlyList<short> LargePreference = new[]
    {
        ProductImageVariants.LargeImageTypeId,
        ProductImageVariants.SquareImageTypeId,
        ProductImageVariants.PromoImageTypeId
    };

    /// <summary>
    /// The types the strip under the main frame and the small rows in the cart and the order list may be drawn from,
    /// best first: the thumbnail, the square, the promo.
    /// </summary>
    public static readonly IReadOnlyList<short> ThumbnailPreference = new[]
    {
        ProductImageVariants.ThumbnailImageTypeId,
        ProductImageVariants.SquareImageTypeId,
        ProductImageVariants.PromoImageTypeId
    };

    /// <summary>
    /// The types read when the image folder cannot be reached at all (see <see cref="Resolve"/>): the square and the
    /// promo, the two sizes every one of the shop's products really has a file for.
    /// </summary>
    private static readonly IReadOnlyList<short> ReachableWithoutFolder = new[]
    {
        ProductImageVariants.SquareImageTypeId,
        ProductImageVariants.PromoImageTypeId
    };

    /// <summary>
    /// One photo of a product: the file a big frame draws it from and the file the strip draws, which are the same
    /// file when the smaller size was never produced.
    /// </summary>
    public sealed record Picture(string Large, string Thumbnail);

    /// <summary>
    /// The pictures of a product, in the order the shop put them in, each with the file a big frame draws and the file
    /// the strip draws.
    ///
    /// That order is the photos' image indices: the admin's product form shows the photos in the order of their indices
    /// and its move controls number them again to change it (see <c>ProductFormComponent.movePhoto</c> in
    /// HC.Web.Admin), so the order the shop sees on the form is the order the storefront draws.
    ///
    /// <paramref name="fileIsThere"/> is asked whether a row's file is really in the shop's image folder - the one
    /// thing the rows cannot be trusted about. When it answers 'no' for every file of the product - which is what a
    /// folder the server cannot read looks like, not a product without pictures - the rows are trusted after all and
    /// the two sizes the older products have are used, so a shop whose folder is out of reach shows what it showed
    /// before rather than a product page with no picture at all.
    /// </summary>
    public static List<Picture> Resolve(IEnumerable<ProductImageRefDto> storedImages, Func<string, bool> fileIsThere)
    {
        var pictures = Pick(storedImages, fileIsThere, LargePreference, ThumbnailPreference);
        if (pictures.Count > 0)
            return pictures;

        return Pick(storedImages, _ => true, ReachableWithoutFolder, ReachableWithoutFolder);
    }

    /// <summary>Reads one picture per photo, preferring the types given, and leaves out the photos with no file.</summary>
    private static List<Picture> Pick(
        IEnumerable<ProductImageRefDto> storedImages,
        Func<string, bool> fileIsThere,
        IReadOnlyList<short> largePreference,
        IReadOnlyList<short> thumbnailPreference)
    {
        var pictures = new List<Picture>();

        // A photo is one index: its sizes share it, so they are drawn together and the storefront can show one photo
        // big and small at once. The photos are drawn in the order of their indices, which is the order the admin's
        // product form shows them in and the shop's own choice of order.
        foreach (var photo in storedImages.GroupBy(image => image.ImageIndex).OrderBy(group => group.Key))
        {
            var large = Choose(photo, largePreference, fileIsThere);
            var thumbnail = Choose(photo, thumbnailPreference, fileIsThere);

            // A photo that only has a small file stored is still a photo of the product: draw it big too, rather
            // than leave the product with no picture.
            large ??= thumbnail;

            if (large != null)
                pictures.Add(new Picture(large, thumbnail ?? large));
        }

        return pictures;
    }

    /// <summary>The first of the preferred types this photo has a file for, or null when it has none of them.</summary>
    private static string? Choose(
        IEnumerable<ProductImageRefDto> photo,
        IReadOnlyList<short> preference,
        Func<string, bool> fileIsThere)
    {
        foreach (var imageTypeId in preference)
        {
            var stored = photo.FirstOrDefault(image => image.ImageTypeId == imageTypeId && fileIsThere(image.ImageUrl));
            if (stored != null)
                return stored.ImageUrl;
        }

        return null;
    }
}
