namespace HC.Business;

/// <summary>
/// One size a product photo is stored in: the image type it is written as (the shop's 'ImageTypes' row), the pixel
/// box the photo is fitted into, and whether a plain upload generates it or the admin has to ask for it.
/// </summary>
/// <param name="ImageTypeId">The 'ImageTypes' id the row is written with (L 1, S 2, R 3, M 4, T 5, P 6).</param>
/// <param name="ShortCode">
/// The type's short code - the second part of every file name ('I10040_S_01.jpg'), and the way the ImageTypes table
/// itself spells the type.
/// </param>
/// <param name="ImageTypeName">The type's name as the admin screens and the 'ImageTypes' table spell it.</param>
/// <param name="Width">
/// The width of the box the photo is fitted into. A photo wider than the box's shape is written this wide; one taller
/// than the box's shape is written narrower, because it is fitted rather than filled (see <c>ProductImageService</c>).
/// </param>
/// <param name="Height">
/// The height of the box the photo is fitted into, the other way round from <paramref name="Width"/>. The two are a
/// box, not a shape the file is forced into: what is written keeps the photograph's own shape, at the largest size
/// that goes inside them, and a smaller upload is never blown up to them.
/// </param>
/// <param name="GeneratedByDefault">
/// True for the sizes the storefront actually draws - the ones the upload screen offers already ticked, so a shop
/// that changes nothing stores exactly what its pages draw; false for the extra sizes the admin has to tick for.
/// </param>
public sealed record ProductImageVariant(
    short ImageTypeId,
    string ShortCode,
    string ImageTypeName,
    int Width,
    int Height,
    bool GeneratedByDefault);

/// <summary>
/// The size map of the shop's product photos: every size a photo can be stored in, which of them an upload writes
/// unless the admin says otherwise, and the name each file is written as.
///
/// It is measured against what the storefront really draws, not against the type list: every image slot in the shop
/// is a box that draws the whole photograph inside it ('object-fit: contain', so a photo of any shape is shown in
/// full), and the widest box is the product page's main frame, capped at 480px (the cards on the home page and the
/// shop grid cap at about 320px, the cart and order rows at 48-64px, the strip under the main frame at 72px). Two
/// pixel boxes cover all of them:
///
/// <list type="bullet">
/// <item>1200x1200 - the master box, written as L, S and P. 1200 is what the shop's own photographs are already
/// stored at (products 10040 and 10041 are 1200x1200), so it is the size the catalogue speaks, and it leaves room
/// for a bigger frame later without a re-upload. A photograph is fitted into it rather than filled to it, so a 3:2
/// upload is written 1200x800 and a 2:3 one 800x1200: the box caps the size, it does not reshape the photo.</item>
/// <item>150x150 - the thumbnail, written as T, for the strip under the main frame and the small rows in the cart and
/// the order list - likewise the photograph's own shape at 150 along its longer side.</item>
/// </list>
///
/// R (Rectangle) and M (Small) are kept because the admin area offers every type the 'ImageTypes' table names, but
/// no slot sits between 150px and 1200px and none of the shop's boxes is shaped for a particular photo (the square
/// boxes show a tall photo whole, letterboxed), so neither is written unless the admin ticks it for the photo being
/// uploaded (see <see cref="Selected"/>). R's 3:2 box is the one non-square shape on the list, and even there the
/// photo is fitted rather than filled: a square upload lands in it 800x800.
/// </summary>
public static class ProductImageVariants
{
    /// <summary>The 'ImageTypes' ids, as the live table names them.</summary>
    public const short LargeImageTypeId = 1;
    public const short SquareImageTypeId = 2;
    public const short RectangleImageTypeId = 3;
    public const short SmallImageTypeId = 4;
    public const short ThumbnailImageTypeId = 5;
    public const short PromoImageTypeId = 6;

    /// <summary>
    /// The master box: the largest size a photo is stored at, and the one a big frame draws. It caps the photo's
    /// longer side; the shorter one follows the photo's own shape, so a wide photograph is written 1200 wide and a
    /// tall one 1200 high.
    /// </summary>
    public const int MasterSize = 1200;

    /// <summary>
    /// The longer side of a thumbnail: the strip under a product page's main frame, and the small rows in the cart
    /// and the order list.
    /// </summary>
    public const int ThumbnailSize = 150;

    /// <summary>The width of the R box: the master box's width in a 3:2 shape.</summary>
    public const int RectangleWidth = 1200;

    /// <summary>The height of the R box: two thirds of its width, so the box is 3:2.</summary>
    public const int RectangleHeight = 800;

    /// <summary>The side of the M box: a small square, for a frame between the thumbnail and the master.</summary>
    public const int SmallSize = 480;

    /// <summary>
    /// The quality every generated file is written at. It is high enough that a photograph shows no artefact at the
    /// sizes the shop draws (the master is drawn at 480px at most, the thumbnail at 72px) and low enough that the
    /// files stay near what the catalogue's own 1200x1200 photographs weigh.
    /// </summary>
    public const int JpegQuality = 88;

    /// <summary>
    /// The extension of every generated file. The shop's folder is all JPEG - the photographs are photographs, and
    /// the name a product's file is looked up by must not depend on what the admin happened to upload - so a PNG or
    /// a WEBP upload is decoded, resized and written as a JPEG like any other (see <c>ProductImageService</c>).
    /// </summary>
    public const string Extension = ".jpg";

    /// <summary>
    /// Every size the uploader knows about, in the order a photo's rows are written. The order is the order the
    /// storefront prefers them in: the sizes it draws first, then the ones it does not.
    /// </summary>
    private static readonly ProductImageVariant[] Variants =
    {
        new(LargeImageTypeId, "L", "Large", MasterSize, MasterSize, true),
        new(SquareImageTypeId, "S", "Square", MasterSize, MasterSize, true),
        new(PromoImageTypeId, "P", "Promo Image", MasterSize, MasterSize, true),
        new(ThumbnailImageTypeId, "T", "Thumbnail", ThumbnailSize, ThumbnailSize, true),
        new(RectangleImageTypeId, "R", "Rectangle", RectangleWidth, RectangleHeight, false),
        new(SmallImageTypeId, "M", "Small", SmallSize, SmallSize, false)
    };

    /// <summary>Every size the uploader can generate, in the order a photo's rows are written.</summary>
    public static IReadOnlyList<ProductImageVariant> All => Variants;

    /// <summary>
    /// The sizes an upload writes when the admin has not said otherwise: the four the storefront draws, which the
    /// upload screen offers already ticked.
    /// </summary>
    public static IReadOnlyList<ProductImageVariant> Default
        => Variants.Where(v => v.GeneratedByDefault).ToList();

    /// <summary>
    /// The sizes an upload is to write: the ones the admin ticked for the photograph being uploaded, in the order a
    /// photo's rows are written whatever order they were ticked in. An id the uploader has no size for is left out -
    /// the shop's 'ImageTypes' table may name a type from before the uploader (see <see cref="For"/>) - and a list
    /// that names none it knows answers empty, which <c>ProductImageService.GenerateAsync</c> refuses rather than
    /// writing nothing and calling it a success.
    /// </summary>
    public static IReadOnlyList<ProductImageVariant> Selected(IEnumerable<short>? imageTypeIds)
    {
        var ticked = imageTypeIds?.ToList() ?? new List<short>();

        return Variants.Where(variant => ticked.Contains(variant.ImageTypeId)).ToList();
    }

    /// <summary>The size the given 'ImageTypes' id names, or null when the shop's table has no such row.</summary>
    public static ProductImageVariant? For(short imageTypeId)
        => Variants.FirstOrDefault(v => v.ImageTypeId == imageTypeId);

    /// <summary>
    /// True for the type a product's card, its cart row and its order row are pictured from: the Promo Image. It is one
    /// of the sizes an upload writes unless the admin unticks it, so a product uploaded as usual always has a picture
    /// for the listings.
    /// </summary>
    public static bool IsPromoImage(short imageTypeId) => imageTypeId == PromoImageTypeId;

    /// <summary>
    /// The name a photo's variant is written as: 'I{product id}_{short code}_{index:00}.jpg' - 'I10040_S_01.jpg'.
    /// It is the name every file in the shop's image folder already has, so the folder stays readable and a photo can
    /// be found from its product by eye.
    ///
    /// The product's id is what names the file, and that is the whole reason a photo is added from a saved product's
    /// page rather than while the product is being created: a product that has no id yet has nothing to name its files
    /// after (see <c>AdminDashboardService.CreateProductAsync</c>), so the admin saves the product first and adds its
    /// photos from the edit page it lands on.
    ///
    /// Uploading the same photo index again writes the same names, which replaces that photo rather than piling up
    /// files nothing points at - but it does mean the file keeps its name while its content changes, so a browser
    /// that has cached it may keep showing the old picture until it re-checks (see <c>ProductImages:Root</c> in
    /// appsettings.json).
    /// </summary>
    public static string FileName(int productId, string shortCode, int imageIndex)
        => $"I{productId}_{shortCode}_{imageIndex:00}{Extension}";
}
