using SkiaSharp;

namespace HC.Business;

/// <summary>
/// The shop's product image folder on disk (see <see cref="IProductImageService"/>).
/// </summary>
public sealed class ProductImageService : IProductImageService
{
    /// <summary>
    /// The configuration key that says where the shop's product images live. Empty or missing means
    /// '{web root}/images/products', which is where the API serves them from itself.
    /// </summary>
    public const string RootConfigurationKey = "ProductImages:Root";

    /// <summary>
    /// How a photo is read when it is brought down to one of the sizes: the pixels around each one that is kept are
    /// weighed in, and the picture is read through the mipmaps first, so a step that is already close to the size it
    /// lands at is what is read (see <see cref="HalveToSize"/>).
    /// </summary>
    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    public ProductImageService(string root)
    {
        Root = root;
    }

    public string Root { get; }

    /// <summary>
    /// Where the shop's product images live: 'ProductImages:Root' when the shop sets it, '{web root}/images/products'
    /// when it does not.
    ///
    /// The shop sets it when the images are served by another site on the same machine - the Angular storefront runs
    /// on its own host, and '{host}/images/products/' is where a shopper's browser asks for a photograph, so the
    /// files have to be written into that same folder. The API's own web root is the default because a shop that
    /// serves its storefront from the API host needs nothing configured at all.
    ///
    /// This is the one place the folder is resolved, so the uploader, the product page's file check and the static
    /// files the API serves cannot drift onto three different folders.
    /// </summary>
    public static string ResolveRoot(string? configuredRoot, string? webRootPath, string contentRootPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            // A relative root is read from the API's own content root rather than from whatever folder the process
            // happened to be started in, so '..\..\HC.Web\src\assets\images\products' means one folder whether the
            // API is started from Visual Studio, from 'dotnet run' or as a deployed app.
            return Path.GetFullPath(configuredRoot.Trim(), contentRootPath);
        }

        var webRoot = string.IsNullOrWhiteSpace(webRootPath)
            ? Path.Combine(contentRootPath, "wwwroot")
            : webRootPath!;

        return Path.Combine(webRoot, "images", "products");
    }

    public bool Exists(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        try
        {
            // Only the file's name is read: a stored value must never be able to point outside the folder.
            return File.Exists(Path.Combine(Root, Path.GetFileName(fileName)));
        }
        catch
        {
            // An unreadable folder - permissions, a share that is down - is not worth failing a product page over.
            return false;
        }
    }

    public async Task<IReadOnlyList<GeneratedProductImage>> GenerateAsync(
        Stream source,
        int productId,
        int imageIndex,
        IEnumerable<short> imageTypeIds,
        CancellationToken cancellationToken = default)
    {
        var sizes = ProductImageVariants.Selected(imageTypeIds);

        // Nothing to write. A request that ticks no size is the admin's to fix: answering 'written in 0 sizes' would
        // leave the form with a photo whose rows point at no file, which the product page would draw as a gap. The
        // upload is refused before the file is even read, so a request that asks for no size writes nothing at all.
        if (sizes.Count == 0)
            throw new ArgumentException("No size was asked for that this uploader can write.", nameof(imageTypeIds));

        using var uploaded = Decode(source);

        var generated = new List<GeneratedProductImage>();

        Directory.CreateDirectory(Root);

        foreach (var variant in sizes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The product's id names the file, the same way every other photo in the folder is named: a photo belongs
            // to a saved product, which is why the product is saved before its first photo is added.
            var fileName = ProductImageVariants.FileName(productId, variant.ShortCode, imageIndex);

            using var resized = FitToBox(uploaded, variant.Width, variant.Height);
            using var image = SKImage.FromBitmap(resized);
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, ProductImageVariants.JpegQuality);

            if (encoded is null)
                throw new InvalidOperationException(
                    $"The {variant.ImageTypeName} ({variant.ShortCode}) version of the image could not be written.");

            await File.WriteAllBytesAsync(Path.Combine(Root, fileName), encoded.ToArray(), cancellationToken);

            // The file's own size, not the box it was fitted into: what is reported here is what the admin screen
            // prints beside the file name, and the box would describe a photograph this uploader never wrote (a
            // 1200x1200 master for a 3:2 photo that is really 1200x800).
            generated.Add(new GeneratedProductImage(variant, fileName, imageIndex, resized.Width, resized.Height));
        }

        return generated;
    }

    /// <summary>
    /// Reads the uploaded file into a bitmap, whatever image format it is.
    ///
    /// The stream is copied into memory first because it is decoded once and then read six times over, and a request
    /// stream can only be walked once. A file that cannot be decoded is refused here, before anything is written, so
    /// an upload that is not really an image leaves no half-written files behind - and the admin is told what is
    /// wrong rather than left with a row pointing at a file that was never made.
    /// </summary>
    private static SKBitmap Decode(Stream source)
    {
        using var buffered = new MemoryStream();
        source.CopyTo(buffered);
        buffered.Position = 0;

        return SKBitmap.Decode(buffered)
            ?? throw new InvalidOperationException("The uploaded file is not an image the server can read.");
    }

    /// <summary>
    /// The uploaded photo brought down to fit one variant's box: scaled to the largest size that goes inside the box
    /// with the photograph's own shape kept, and never blown up past the size it was uploaded at - a bigger copy of a
    /// small photograph would carry no more detail than the photograph itself.
    ///
    /// Nothing is cut off it, so what the shop shows is the whole photograph the admin uploaded. A 3:2 upload lands
    /// in the master's 1200x1200 box as 1200x800 and a 2:3 upload as 800x1200; the box caps the size, it does not
    /// reshape the photo. That is why every slot in the storefront draws a product photo with 'object-fit: contain'
    /// - the product page's frame, its thumbnail strip, the product cards and the cart and order rows - so the frame
    /// is a box the photograph sits inside rather than a window over it.
    ///
    /// It is drawn onto a white canvas, because the file is written as a JPEG whether or not what was uploaded had
    /// transparency in it: a PNG with a cut-out background lands on white rather than on black.
    /// </summary>
    private static SKBitmap FitToBox(SKBitmap uploaded, int width, int height)
    {
        var photoRatio = (double)uploaded.Width / uploaded.Height;
        var boxRatio = (double)width / height;

        int fittedWidth;
        int fittedHeight;

        if (photoRatio > boxRatio)
        {
            // Wider than the box's shape: the box's width is as far as it goes, and the height follows the photo's
            // own shape rather than the box's.
            fittedWidth = Math.Min(uploaded.Width, width);
            fittedHeight = Math.Max(1, (int)Math.Round(fittedWidth / photoRatio, MidpointRounding.AwayFromZero));
        }
        else
        {
            // Taller than the box's shape: the box's height is as far as it goes, and the width follows the photo.
            fittedHeight = Math.Min(uploaded.Height, height);
            fittedWidth = Math.Max(1, (int)Math.Round(fittedHeight * photoRatio, MidpointRounding.AwayFromZero));
        }

        // A photograph that already fits inside the box - one of its sides is at the size it was uploaded at, so the
        // box is the smaller of the two - is written at the size it came in rather than enlarged into the box.
        if (fittedWidth >= uploaded.Width || fittedHeight >= uploaded.Height)
            return Flatten(uploaded);

        using var scaled = HalveToSize(uploaded, fittedWidth, fittedHeight);

        return Flatten(scaled);
    }

    /// <summary>
    /// The photo brought down to the size it is written at in steps rather than in one leap.
    ///
    /// A 1200x800 master cut straight down to a 150x100 thumbnail is eight times smaller, and a filter that reads
    /// only the pixels around where it lands would drop most of the photograph and turn fine detail into speckle.
    /// Halving first means every step reads a picture already close to the size it is drawn at, so the last step is
    /// the only one that has to be any good - which is what keeps the storefront's thumbnails sharp.
    ///
    /// The size asked for is the one being written to (see <see cref="FitToBox"/>), so it is the photograph's own
    /// shape all the way down and the halving cannot take anything off its edges.
    /// </summary>
    private static SKBitmap HalveToSize(SKBitmap photo, int width, int height)
    {
        var current = photo;
        var mine = false;

        while (current.Width > width * 2 && current.Height > height * 2)
        {
            var half = Scale(current, Math.Max(width, current.Width / 2), Math.Max(height, current.Height / 2));
            if (mine)
                current.Dispose();

            current = half;
            mine = true;
        }

        var scaled = Scale(current, width, height);
        if (mine)
            current.Dispose();

        return scaled;
    }

    /// <summary>One step of <see cref="HalveToSize"/>, at the size it asks for.</summary>
    private static SKBitmap Scale(SKBitmap source, int width, int height)
        => source.Resize(new SKImageInfo(width, height, source.ColorType, SKAlphaType.Premul), Sampling)
           ?? throw new InvalidOperationException($"The image could not be brought down to {width}x{height}.");

    /// <summary>
    /// The finished bitmap as it is written: the photo drawn onto white, so a photograph uploaded with transparency
    /// in it - a PNG with a cut-out background - lands on white rather than on black once the file is written as a
    /// JPEG.
    ///
    /// This is a copy at the size the photo is already at, so nothing is scaled and there is no sampling to weigh up:
    /// the white only shows where the photograph has transparency.
    /// </summary>
    private static SKBitmap Flatten(SKBitmap photo)
    {
        var flattened = new SKBitmap(
            new SKImageInfo(photo.Width, photo.Height, SKColorType.Rgba8888, SKAlphaType.Opaque));

        using (var canvas = new SKCanvas(flattened))
        using (var paint = new SKPaint())
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(photo, SKRect.Create(flattened.Width, flattened.Height), paint);
        }

        return flattened;
    }
}
