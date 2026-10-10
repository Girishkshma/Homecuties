namespace HC.Business;

/// <summary>
/// One file an upload wrote: the size it is (and therefore the image type its row is written with), the name it was
/// written as in the shop's image folder, and the pixels it really came out at.
/// </summary>
/// <param name="Variant">
/// The size the file was written for: the image type its row is written with and the box the photograph was fitted
/// into.
/// </param>
/// <param name="FileName">The name the file was written as in the shop's image folder.</param>
/// <param name="ImageIndex">Which photo of the product this is - the admin's 'Image Index'.</param>
/// <param name="Width">
/// The width of the file that was written, which is the photograph's own once it was fitted into
/// <see cref="ProductImageVariant.Width"/> - not the box's width. A 3:2 photograph written for the 1200x1200 master
/// is 1200x800, and that is what this says: the box caps the size, it does not reshape the photo.
/// </param>
/// <param name="Height">The height of the file that was written, the other way round from <paramref name="Width"/>.</param>
/// <remarks>
/// The written pixels are reported rather than the box because the admin screen prints them beside every file name,
/// which is how the shop team sees what an upload really produced: a screen shown the box would say 1200x1200 of a
/// file that is 1200x800, and a photograph that was fitted would read as one that had been reshaped (see
/// <c>AdminController.UploadProductImage</c>).
/// </remarks>
public sealed record GeneratedProductImage(
    ProductImageVariant Variant,
    string FileName,
    int ImageIndex,
    int Width,
    int Height)
{
    /// <summary>
    /// True when this is the image type a product's card, its cart row and its order row are pictured from, so the
    /// row is written as the product's promo image (see <see cref="ProductImageVariants.IsPromoImage"/>).
    /// </summary>
    public bool IsPromoImage => ProductImageVariants.IsPromoImage(Variant.ImageTypeId);
}

/// <summary>
/// The shop's product image folder: where a product's photographs are written and read from.
///
/// One implementation serves both ends of the storefront's images - the admin area, which puts a photo in and is
/// given back the file names to save rows for, and the product page, which asks whether a row's file is really there
/// before drawing it (see <see cref="ProductGallery"/>) - because both have to agree on exactly one folder. Where
/// that folder is, is set by 'ProductImages:Root' (see <see cref="ProductImageService.ResolveRoot"/>), and it is the
/// folder the storefront's own '{host}/images/products/{file}' is served from: the files a photo is written to and
/// the files a shopper's browser asks for must be the same files, or every upload is invisible.
/// </summary>
public interface IProductImageService
{
    /// <summary>
    /// The request path, inside a web host, that the shop's product images are served at:
    /// '{host}/images/products/{file}'. Every URL the API hands out for a written file is built from it, so a
    /// written file and the URL that names it cannot drift apart.
    /// </summary>
    public const string RequestPath = "images/products";

    /// <summary>The folder every product image file lives in.</summary>
    string Root { get; }

    /// <summary>
    /// Whether the named file is really in the shop's image folder. Only the file's name is read - a stored value is
    /// never allowed to name a folder of its own - and a folder the server cannot read answers 'no' rather than
    /// failing a product page (see <see cref="ProductGallery.Resolve"/> for what that means).
    /// </summary>
    bool Exists(string fileName);

    /// <summary>
    /// Resizes one uploaded photo into the sizes asked for and writes them into the folder, returning the files and
    /// the rows they belong to.
    ///
    /// <paramref name="productId"/> is the product the photo belongs to, and it is required: a photo is named after
    /// the product it belongs to ('I{id}_{code}_{index:00}.jpg'), so a product that has not been saved yet has nothing
    /// to name its files after - the product is saved, and its photos added from its edit page.
    /// <paramref name="imageIndex"/> is which photo of the product this is (the admin's 'Image Index'), which is what
    /// lets a screen show one photo at two sizes at once, and which an upload of the same photo again replaces. The
    /// photo is fitted into each size's box - scaled to the largest size that goes inside it, keeping the photograph's
    /// own shape, so nothing is ever cut off it - and is never blown up beyond the size it was uploaded at.
    ///
    /// <paramref name="imageTypeIds"/> is the sizes to write, as the 'ImageTypes' ids the admin ticked on the upload
    /// screen: what is written is exactly those and nothing else, so a shop that wants only some of the sizes does not
    /// store the rest (see <see cref="ProductImageVariants.Selected"/>). An id the uploader has no size for is left
    /// out; a list that names no size it knows is refused with an <see cref="ArgumentException"/> rather than
    /// answering 'written in 0 sizes', because a photo with no file is a row the product page cannot draw.
    /// </summary>
    Task<IReadOnlyList<GeneratedProductImage>> GenerateAsync(
        Stream source,
        int productId,
        int imageIndex,
        IEnumerable<short> imageTypeIds,
        CancellationToken cancellationToken = default);
}
