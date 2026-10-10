using System.Text;
using HC.Business;
using SkiaSharp;
using Xunit;

namespace HC.Tests;

/// <summary>
/// What one upload writes into the shop's image folder: the sizes a photo is stored at, the names its files are
/// given, and the shapes they come out in.
///
/// These are the sizes the storefront actually draws (see ProductImageVariants), so a change here that is not made
/// with the storefront in mind - a size dropped, a name reshaped, a thumbnail left at the master's weight - is what
/// these tests are here to catch.
/// </summary>
public class ProductImageServiceTests
{
    /// <summary>
    /// The sizes the storefront draws, ticked the way the admin's upload screen ticks them: what the tests that are
    /// about the sizes themselves ask for, and what an upload writes when the admin changes nothing.
    /// </summary>
    private static readonly short[] DrawnSizes = ProductImageVariants.Default
        .Select(variant => variant.ImageTypeId)
        .ToArray();

    /// <summary>
    /// Every size the uploader can write, as the admin ticks them all - the two sizes no storefront screen draws
    /// included, which are what a photo is written at only when someone asks for those boxes.
    /// </summary>
    private static readonly short[] EverySize = ProductImageVariants.All
        .Select(variant => variant.ImageTypeId)
        .ToArray();

    /// <summary>
    /// The photo the screens draw from is stored at 1200x1200 and the strip under the main frame at 150x150, and one
    /// upload writes both - the whole point of the uploader, and what the admin used to have to do by hand four
    /// times over (L, S, P and T).
    /// </summary>
    [Fact]
    public async Task OneUploadWritesTheSizesTheStorefrontDraws()
    {
        using var folder = new TempImageFolder();
        using var photo = Photo(2000, 2000, SKColors.Green);

        var written = await folder.Service.GenerateAsync(photo, 10040, 2, DrawnSizes);

        Assert.Equal(
            new[] { "I10040_L_02.jpg", "I10040_S_02.jpg", "I10040_P_02.jpg", "I10040_T_02.jpg" },
            written.Select(file => file.FileName));

        Assert.Equal((1200, 1200), folder.SizeOf("I10040_L_02.jpg"));
        Assert.Equal((1200, 1200), folder.SizeOf("I10040_S_02.jpg"));
        Assert.Equal((1200, 1200), folder.SizeOf("I10040_P_02.jpg"));
        Assert.Equal((150, 150), folder.SizeOf("I10040_T_02.jpg"));

        // Every file the upload reports is really in the folder: the rows the admin screen writes from these names
        // must not point at files that were never made.
        Assert.All(written, file => Assert.True(folder.Service.Exists(file.FileName)));
    }

    /// <summary>
    /// The promo image is the type a product's card, its cart row and its order row are pictured from, and the size
    /// that is the promo is marked as such in what the upload answers - which is the file the admin screen previews the
    /// photo with, and the one the shop's listings really draw.
    /// </summary>
    [Fact]
    public async Task TheAnswerSaysWhichSizeIsThePromoImage()
    {
        using var folder = new TempImageFolder();
        using var photo = Photo(1600, 1600, SKColors.Green);

        var written = await folder.Service.GenerateAsync(photo, 10040, 1, DrawnSizes);

        Assert.True(Assert.Single(written, file => file.FileName == "I10040_P_01.jpg").IsPromoImage);
        Assert.False(Assert.Single(written, file => file.FileName == "I10040_L_01.jpg").IsPromoImage);

        // The promo is one of the sizes an upload writes unless the admin unticks it, so a shop that changes nothing
        // keeps a listing picture for every photo; and the frames fall back to the widest size ticked where a photo has
        // no promo (see ProductGallery).
        Assert.Contains(ProductImageVariants.PromoImageTypeId, DrawnSizes);

        // The index is the photo's own: it is what tells the strip's thumbnails apart and what lets a screen show
        // one photo big and small at once.
        Assert.All(written, file => Assert.Equal(1, file.ImageIndex));
    }

    /// <summary>
    /// The two sizes no screen draws are written only when the admin ticks them, and then into the boxes the
    /// 'ImageTypes' table names them by: R's box is 3:2 and M's is a small square. The photo here is a square one, so
    /// it fills neither box edge to edge - it is fitted inside them and keeps its own shape, which in R's 3:2 box is
    /// 800x800: as tall as the box and only two thirds as wide.
    /// </summary>
    [Fact]
    public async Task TheSizesNoScreenDrawsAreWrittenOnlyWhenAskedFor()
    {
        using var folder = new TempImageFolder();
        using var photo = Photo(2000, 2000, SKColors.Green);

        var written = await folder.Service.GenerateAsync(photo, 10040, 1, EverySize);

        Assert.Equal(6, written.Count);
        Assert.Equal((800, 800), folder.SizeOf("I10040_R_01.jpg"));
        Assert.Equal((480, 480), folder.SizeOf("I10040_M_01.jpg"));
    }

    /// <summary>
    /// The sizes written are the ones the admin ticked and no others: a photograph that needs only the master and the
    /// strip's thumbnail does not store the square, the promo, or the two sizes no screen draws, and no file is left in
    /// the folder for a size nobody asked for. The 99 is a type the shop's 'ImageTypes' table might name and the
    /// uploader has no size for: it is left out rather than stopping the upload.
    /// </summary>
    [Fact]
    public async Task OnlyTheSizesTheAdminTickedAreWritten()
    {
        using var folder = new TempImageFolder();
        using var photo = Photo(2000, 2000, SKColors.Green);

        var written = await folder.Service.GenerateAsync(
            photo, 10040, 1,
            new[] { ProductImageVariants.LargeImageTypeId, (short)99, ProductImageVariants.ThumbnailImageTypeId });

        Assert.Equal(new[] { "I10040_L_01.jpg", "I10040_T_01.jpg" }, written.Select(file => file.FileName));

        // Nothing else was written either: a size that was not ticked is not a file lying in the folder for rows nobody
        // has.
        Assert.Equal(
            new[] { "I10040_L_01.jpg", "I10040_T_01.jpg" },
            folder.Files().OrderBy(file => file, StringComparer.Ordinal));

        Assert.Equal((1200, 1200), folder.SizeOf("I10040_L_01.jpg"));
        Assert.Equal((150, 150), folder.SizeOf("I10040_T_01.jpg"));
    }

    /// <summary>
    /// A request that ticks no size, or none the uploader has a size for, writes nothing at all: it is refused before
    /// the photograph is even read, because answering 'written in 0 sizes' would leave the form with a photo whose rows
    /// point at no file - a gap the product page would draw, with nothing in the folder to fill it.
    /// </summary>
    [Fact]
    public async Task AnUploadThatAsksForNoSizeIsRefused()
    {
        using var folder = new TempImageFolder();
        using var photo = Photo(2000, 2000, SKColors.Green);

        await Assert.ThrowsAsync<ArgumentException>(
            () => folder.Service.GenerateAsync(photo, 10040, 1, Array.Empty<short>()));

        // A tick for a type this uploader has no size for is the same as no tick at all.
        await Assert.ThrowsAsync<ArgumentException>(
            () => folder.Service.GenerateAsync(photo, 10040, 1, new short[] { 99 }));

        Assert.Empty(folder.Files());
    }

    /// <summary>
    /// The sizes are written in the shop's own order rather than the order they were ticked in: the rows a product is
    /// saved with, and the order the storefront prefers its sizes in, must not depend on which box an admin happened to
    /// click first (see ProductGallery).
    /// </summary>
    [Fact]
    public async Task TheTickedSizesAreWrittenInTheShopsOwnOrder()
    {
        using var folder = new TempImageFolder();
        using var photo = Photo(2000, 2000, SKColors.Green);

        // M, then T, then L: ticked backwards, written the way the shop's types are listed.
        var written = await folder.Service.GenerateAsync(
            photo, 10040, 1,
            new[]
            {
                ProductImageVariants.SmallImageTypeId,
                ProductImageVariants.ThumbnailImageTypeId,
                ProductImageVariants.LargeImageTypeId
            });

        Assert.Equal(
            new[]
            {
                ProductImageVariants.LargeImageTypeId,
                ProductImageVariants.ThumbnailImageTypeId,
                ProductImageVariants.SmallImageTypeId
            },
            written.Select(file => file.Variant.ImageTypeId));
    }

    /// <summary>
    /// A photo that is wider than it is tall is narrowed to fit the box rather than cut to its shape: 2400x1600 is
    /// written 1200x800, both sides at about half, so the photograph keeps its 3:2 shape and nothing comes off its
    /// left or right edge. The photo here is red at its left and right edges and green across the middle 1600 of its
    /// 2400 pixels, and both red edges are still in the file.
    /// </summary>
    [Fact]
    public async Task AWidePhotoIsNarrowedToFitAndKeepsItsEdges()
    {
        using var folder = new TempImageFolder();
        using var photo = Photo(
            2400, 1600,
            SKColors.Red, SKColors.Lime, SKColors.Lime, SKColors.Lime, SKColors.Lime, SKColors.Red);

        await folder.Service.GenerateAsync(photo, 10040, 1, DrawnSizes);

        // The photograph's own shape at the master box's width, and at the thumbnail's: the box caps the size, it
        // does not reshape the photo.
        Assert.Equal((1200, 800), folder.SizeOf("I10040_L_01.jpg"));
        Assert.Equal((1200, 800), folder.SizeOf("I10040_P_01.jpg"));
        Assert.Equal((150, 100), folder.SizeOf("I10040_T_01.jpg"));

        // The red edges the upload carried are still where they were, so the photograph was scaled rather than
        // trimmed. The files are JPEGs, so a pixel comes back a shade or two from the colour that went in: what can
        // be held to is which band it is of - red rather than the green the photograph is mostly made of.
        AssertRed(folder.ColourAt("I10040_L_01.jpg", 0.02, 0.5));
        AssertRed(folder.ColourAt("I10040_L_01.jpg", 0.98, 0.5));
        AssertGreen(folder.ColourAt("I10040_L_01.jpg", 0.5, 0.5));
    }

    /// <summary>
    /// What an upload reports for each file is the size the file really is, not the box it was fitted into. The
    /// admin screen prints those pixels beside every file name and the rows a product is saved with come from the
    /// same payload, so a report of the box would show a 3:2 photograph as the master's 1200x1200 - a shape no file
    /// in the folder has - and the shop team would have no way of seeing that nothing had been reshaped.
    /// </summary>
    [Fact]
    public async Task AnUploadReportsTheFilesOwnSizeRatherThanItsBox()
    {
        using var folder = new TempImageFolder();
        using var photo = Photo(2400, 1600, SKColors.Green);

        var written = await folder.Service.GenerateAsync(photo, 10040, 1, EverySize);

        Assert.All(written, file => Assert.Equal(folder.SizeOf(file.FileName), (file.Width, file.Height)));

        // Spelled out for the two boxes a 3:2 photo comes out of differently shaped from the box itself: the
        // master's 1200x1200 box holds it 1200x800 and the thumbnail's 150x150 box holds it 150x100.
        var master = Assert.Single(written, file => file.FileName == "I10040_L_01.jpg");
        Assert.Equal((1200, 800), (master.Width, master.Height));

        var thumbnail = Assert.Single(written, file => file.FileName == "I10040_T_01.jpg");
        Assert.Equal((150, 100), (thumbnail.Width, thumbnail.Height));
    }


    /// <summary>
    /// A photo that is taller than it is wide is narrowed to the box in the same way: 800x2400 is written 400x1200,
    /// both sides at half, so nothing comes off its top or bottom either. Cutting it to a square instead - what the
    /// uploader used to do, and what the storefront's 'object-fit: cover' did to whatever was left - would have
    /// thrown away two thirds of its height in an 800x800 file.
    /// </summary>
    [Fact]
    public async Task ATallPhotoIsNarrowedToFitAndKeepsItsHeight()
    {
        using var folder = new TempImageFolder();
        using var photo = Photo(800, 2400, SKColors.Green);

        await folder.Service.GenerateAsync(photo, 10040, 1, DrawnSizes);

        Assert.Equal((400, 1200), folder.SizeOf("I10040_L_01.jpg"));
        Assert.Equal((400, 1200), folder.SizeOf("I10040_S_01.jpg"));
        Assert.Equal((50, 150), folder.SizeOf("I10040_T_01.jpg"));
    }

    /// <summary>
    /// An upload smaller than the size it is being written at keeps the size it has: enlarging a small photograph
    /// would fill the storefront's frames with blurred pixels rather than with detail. The thumbnail is the one box
    /// that is still bigger than the upload, so it is the only size that comes back smaller - and it comes back at
    /// the photograph's own 4:3 shape (150x113) rather than squared off to 150x150.
    /// </summary>
    [Fact]
    public async Task ASmallUploadIsNeverBlownUp()
    {
        using var folder = new TempImageFolder();
        using var photo = Photo(200, 150, SKColors.Green);

        var written = await folder.Service.GenerateAsync(photo, 10040, 1, DrawnSizes);

        Assert.Equal((200, 150), folder.SizeOf("I10040_L_01.jpg"));
        Assert.Equal((200, 150), folder.SizeOf("I10040_S_01.jpg"));
        Assert.Equal((200, 150), folder.SizeOf("I10040_P_01.jpg"));
        Assert.Equal((150, 113), folder.SizeOf("I10040_T_01.jpg"));

        // No file is bigger than the upload in either direction, whichever size it was written for.
        Assert.All(written, file =>
        {
            var (width, height) = folder.SizeOf(file.FileName);
            Assert.True(width <= 200 && height <= 150, $"{file.FileName} is {width}x{height}.");
        });
    }

    /// <summary>
    /// Every file is named after the product it belongs to - 'I{product id}_{short code}_{index:00}.jpg' - which is the
    /// same name shape every photo already in the shop's folder has, so a file can be found from its product by eye and
    /// nothing is named after anything but a product. Two products' first photos share an index and a size, so only the
    /// id in the name tells them apart: an upload of one product never lands on another's file.
    ///
    /// There is no name for a photo of a product that has not been saved, because there is no id to put in it - which is
    /// what makes adding a photo an act of the product's own page rather than of the create screen
    /// (see <c>AdminDashboardService.CreateProductAsync</c>).
    /// </summary>
    [Fact]
    public async Task EveryFileIsNamedAfterTheProductItBelongsTo()
    {
        using var folder = new TempImageFolder();

        using var first = Photo(800, 800, SKColors.Green);
        using var second = Photo(800, 800, SKColors.Blue);

        var firstUpload = await folder.Service.GenerateAsync(first, 10040, 1, DrawnSizes);
        var secondUpload = await folder.Service.GenerateAsync(second, 10041, 1, DrawnSizes);

        Assert.Equal(
            new[] { "I10040_L_01.jpg", "I10040_S_01.jpg", "I10040_P_01.jpg", "I10040_T_01.jpg" },
            firstUpload.Select(file => file.FileName));

        Assert.Empty(firstUpload.Select(file => file.FileName).Intersect(secondUpload.Select(file => file.FileName)));
        Assert.All(secondUpload, file => Assert.StartsWith("I10041_", file.FileName));

        // Both photos are really in the folder under those names: nothing was written over, and nothing is missing.
        Assert.All(
            firstUpload.Concat(secondUpload),
            file => Assert.True(folder.Service.Exists(file.FileName)));
    }

    /// <summary>
    /// A stored value is only ever read as a file's name, so nothing that is not a file of the shop - a path
    /// pointing at another folder, or nothing at all - can be answered 'yes' to, and a product page cannot be made
    /// to draw a file from outside the shop's own image folder.
    /// </summary>
    [Fact]
    public async Task OnlyTheFileNameOfAStoredValueIsRead()
    {
        using var folder = new TempImageFolder();
        using var photo = Photo(1600, 1600, SKColors.Green);

        await folder.Service.GenerateAsync(photo, 10040, 2, DrawnSizes);

        Assert.True(folder.Service.Exists("I10040_L_02.jpg"));
        Assert.True(folder.Service.Exists(@".\I10040_L_02.jpg"));

        Assert.False(folder.Service.Exists(@"..\..\appsettings.json"));
        Assert.False(folder.Service.Exists(@"C:\Windows\win.ini"));
        Assert.False(folder.Service.Exists("I10040_L_03.jpg"));
        Assert.False(folder.Service.Exists(""));
    }

    /// <summary>
    /// An upload that is not an image is refused before anything is written, so a failed upload leaves no
    /// half-written files behind and no row has to be written pointing at one.
    /// </summary>
    [Fact]
    public async Task AnUploadThatIsNotAnImageIsRefusedBeforeAnythingIsWritten()
    {
        using var folder = new TempImageFolder();
        using var notAPhoto = new MemoryStream(Encoding.UTF8.GetBytes("this is not a photo"));

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(
            () => folder.Service.GenerateAsync(notAPhoto, 10040, 1, DrawnSizes));

        Assert.Contains("not an image", refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(folder.Files());
    }

    /// <summary>
    /// Whether a pixel of a written file is the green middle of the photo it came from, rather than the red the
    /// photo carried at its edges. A JPEG comes back a shade or two from the colour that went in, so a test can hold
    /// the uploader to which band a pixel is of, not to the exact value.
    /// </summary>
    private static void AssertGreen(SKColor colour)
        => Assert.True(
            colour.Green > 200 && colour.Red < 60 && colour.Blue < 60,
            $"The pixel is {colour}, which is not the green middle of the photo.");

    /// <summary>
    /// Whether a pixel of a written file is one of the red bands the photo carried at its edges - which is how a test
    /// tells an upload that scaled the photograph from one that trimmed its edges away.
    /// </summary>
    private static void AssertRed(SKColor colour)
        => Assert.True(
            colour.Red > 200 && colour.Green < 60 && colour.Blue < 60,
            $"The pixel is {colour}, which is not a red edge of the photo.");

    /// <summary>
    /// Where the shop's image folder is, whichever way the shop is served: nothing configured means the API's own
    /// web root (a storefront served by the API host needs no configuration), and a configured root is where the
    /// files go instead - the folder the storefront's own host answers '{host}/images/products/{file}' from. A
    /// relative value is read from the API's content root, so one value names one folder however the API was
    /// started.
    /// </summary>
    [Fact]
    public void TheImageFolderIsResolvedForHowTheShopIsServed()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "hc-api");

        Assert.Equal(
            Path.Combine(contentRoot, "wwwroot", "images", "products"),
            ProductImageService.ResolveRoot(null, null, contentRoot));

        Assert.Equal(
            Path.Combine(contentRoot, "wwwroot", "images", "products"),
            ProductImageService.ResolveRoot("  ", null, contentRoot));

        // What ASP.NET reports as the web root is used as it stands when there is one.
        var webRoot = Path.Combine(Path.GetTempPath(), "hc-api-wwwroot");
        Assert.Equal(
            Path.Combine(webRoot, "images", "products"),
            ProductImageService.ResolveRoot(null, webRoot, contentRoot));

        // A configured root is read from the API's content root, not from the folder the process was started in.
        Assert.Equal(
            Path.Combine(contentRoot, "images", "products"),
            ProductImageService.ResolveRoot(@"images\products", null, contentRoot));

        Assert.Equal(
            Path.Combine(Path.GetTempPath(), "hc-storefront", "images", "products"),
            ProductImageService.ResolveRoot(
                Path.Combine(Path.GetTempPath(), "hc-storefront", "images", "products"), null, contentRoot));

        // And the configured root wins: the folder the storefront serves is the folder the uploads go to.
        Assert.Equal(
            Path.Combine(Path.GetTempPath(), "hc-storefront", "images", "products"),
            ProductImageService.ResolveRoot(
                Path.Combine(Path.GetTempPath(), "hc-storefront", "images", "products"), webRoot, contentRoot));
    }

    /// <summary>
    /// A photo of the given size as a PNG in memory, in equal vertical bands of the colours given. The bands are
    /// what a test reads back after an upload, so which part of the photo survived a crop can be told by colour
    /// alone; a band boundary that a crop must not reach is what the fixtures are chosen around.
    /// </summary>
    private static MemoryStream Photo(int width, int height, params SKColor[] bands)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));

        using (var canvas = new SKCanvas(bitmap))
        {
            for (var band = 0; band < bands.Length; band++)
            {
                using var paint = new SKPaint { Color = bands[band] };
                var left = (float)width * band / bands.Length;
                var right = (float)width * (band + 1) / bands.Length;
                canvas.DrawRect(SKRect.Create(left, 0, right - left, height), paint);
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);

        return new MemoryStream(encoded.ToArray());
    }

    /// <summary>
    /// A shop's image folder of its own for one test: a fresh, empty folder under the machine's temporary files,
    /// thrown away when the test is done, so no test can see another's files and the shop's own folder is never
    /// touched.
    /// </summary>
    private sealed class TempImageFolder : IDisposable
    {
        private readonly string _folder =
            Path.Combine(Path.GetTempPath(), "hc-product-images", Guid.NewGuid().ToString("N"));

        public TempImageFolder()
        {
            Service = new ProductImageService(_folder);
        }

        /// <summary>The uploader, pointed at this folder exactly as the API points it at the shop's own.</summary>
        public ProductImageService Service { get; }

        /// <summary>Every file in the folder, so a test can tell 'nothing was written' from 'something was'.</summary>
        public string[] Files()
            => Directory.Exists(_folder)
                ? Directory.GetFiles(_folder).Select(file => Path.GetFileName(file)!).ToArray()
                : Array.Empty<string>();

        /// <summary>The size of a written file, read back the way a browser reads it.</summary>
        public (int Width, int Height) SizeOf(string fileName)
        {
            using var decoded = SKBitmap.Decode(Path.Combine(_folder, fileName));

            Assert.NotNull(decoded);
            return (decoded.Width, decoded.Height);
        }

        /// <summary>One pixel of a written file, at a fraction of the way across it and down it.</summary>
        public SKColor ColourAt(string fileName, double across, double down)
        {
            using var decoded = SKBitmap.Decode(Path.Combine(_folder, fileName));

            Assert.NotNull(decoded);
            return decoded.GetPixel((int)(decoded.Width * across), (int)(decoded.Height * down));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_folder))
                    Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A file the machine still holds open is not worth failing a test over.
            }
        }
    }
}
