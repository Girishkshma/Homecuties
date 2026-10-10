using HC.Business;
using HC.Business.Dtos;
using Xunit;

namespace HC.Tests;

/// <summary>
/// Which file a product page draws, and from which of a product's stored rows.
///
/// A product's rows cannot answer that on their own - a photo is stored in several sizes and the older products were
/// written before all of them existed, so a row can exist whose file was never produced. That is what the shop's own
/// product 10040 looks like (see <see cref="TheRowsThatPointAtNothingAreNotDrawn"/>), and drawing those rows as they
/// stand is what put broken thumbnails in the strip and blank frames on the main-image arrows.
/// </summary>
public class ProductGalleryTests
{
    /// <summary>
    /// A photo uploaded today has all four sizes: the big frame takes the master size and the strip takes the
    /// thumbnail - the two sizes the audit found the storefront really draws.
    /// </summary>
    [Fact]
    public void TheBigFrameAndTheStripAreDrawnFromTheSizesTheStorefrontUses()
    {
        var pictures = ProductGallery.Resolve(
            new[]
            {
                Row(ProductImageVariants.LargeImageTypeId, 1, "I10040_L_01.jpg"),
                Row(ProductImageVariants.SquareImageTypeId, 1, "I10040_S_01.jpg"),
                Row(ProductImageVariants.PromoImageTypeId, 1, "I10040_P_01.jpg"),
                Row(ProductImageVariants.ThumbnailImageTypeId, 1, "I10040_T_01.jpg")
            },
            UploadedTodayFolder);

        var picture = Assert.Single(pictures);

        Assert.Equal("I10040_L_01.jpg", picture.Large);
        Assert.Equal("I10040_T_01.jpg", picture.Thumbnail);
    }

    /// <summary>
    /// A photo from before the newer sizes were written has only its square and its promo file, and those are what
    /// it is drawn from - its square for the big frame, and the same file for the strip, because there is no
    /// thumbnail of it to draw and a small frame of a big file is still a picture of the product.
    /// </summary>
    [Fact]
    public void APhotoFromBeforeTheNewerSizesIsDrawnFromTheOneItHas()
    {
        var pictures = ProductGallery.Resolve(
            new[]
            {
                Row(ProductImageVariants.SquareImageTypeId, 1, "I10040_S_01.jpg"),
                Row(ProductImageVariants.PromoImageTypeId, 1, "I10040_P_01.jpg")
            },
            Product10040Folder);

        var picture = Assert.Single(pictures);

        Assert.Equal("I10040_S_01.jpg", picture.Large);
        Assert.Equal("I10040_S_01.jpg", picture.Thumbnail);
    }

    /// <summary>
    /// The shop's own product 10040, as its rows stand: six sizes written for its first photo and two later rows for
    /// its square, but only the square and the promo files were ever produced. One photo is drawn - both frames from
    /// the file that is really there - and the rows that point at nothing are left out, which is what the strip's
    /// broken thumbnails and the main frame's blank arrows were made of.
    /// </summary>
    [Fact]
    public void TheRowsThatPointAtNothingAreNotDrawn()
    {
        var pictures = ProductGallery.Resolve(
            new[]
            {
                Row(ProductImageVariants.LargeImageTypeId, 1, "I10040_L_01.jpg"),
                Row(ProductImageVariants.SquareImageTypeId, 1, "I10040_S_01.jpg"),
                Row(ProductImageVariants.RectangleImageTypeId, 1, "I10040_R_01.jpg"),
                Row(ProductImageVariants.SmallImageTypeId, 1, "I10040_M_01.jpg"),
                Row(ProductImageVariants.ThumbnailImageTypeId, 1, "I10040_T_01.jpg"),
                Row(ProductImageVariants.PromoImageTypeId, 1, "I10040_P_01.jpg"),
                Row(ProductImageVariants.SquareImageTypeId, 3, "I10040_S_03.jpg"),
                Row(ProductImageVariants.SquareImageTypeId, 4, "I10040_S_04.jpg")
            },
            Product10040Folder);

        var picture = Assert.Single(pictures);

        Assert.Equal("I10040_S_01.jpg", picture.Large);
        Assert.Equal("I10040_S_01.jpg", picture.Thumbnail);
    }

    /// <summary>
    /// A photo whose only small file was ever produced is still drawn - big as well as small - rather than leaving
    /// the product with no picture at all.
    /// </summary>
    [Fact]
    public void APhotoThatOnlyHasAThumbnailIsStillDrawnBig()
    {
        var pictures = ProductGallery.Resolve(
            new[] { Row(ProductImageVariants.ThumbnailImageTypeId, 1, "I10040_T_01.jpg") },
            UploadedTodayFolder);

        var picture = Assert.Single(pictures);

        Assert.Equal("I10040_T_01.jpg", picture.Large);
        Assert.Equal("I10040_T_01.jpg", picture.Thumbnail);
    }

    /// <summary>
    /// The photos keep the order the admin uploaded them in, which is the order the strip shows them in and the
    /// order the arrows walk through.
    /// </summary>
    [Fact]
    public void ThePhotosKeepTheOrderTheyWereUploadedIn()
    {
        var pictures = ProductGallery.Resolve(
            new[]
            {
                Row(ProductImageVariants.PromoImageTypeId, 2, "I10040_P_02.jpg"),
                Row(ProductImageVariants.LargeImageTypeId, 1, "I10040_L_01.jpg"),
                Row(ProductImageVariants.PromoImageTypeId, 1, "I10040_P_01.jpg"),
                Row(ProductImageVariants.LargeImageTypeId, 2, "I10040_L_02.jpg")
            },
            UploadedTodayFolder);

        Assert.Equal(new[] { "I10040_L_01.jpg", "I10040_L_02.jpg" }, pictures.Select(picture => picture.Large));
    }

    /// <summary>
    /// A folder the server cannot read looks like a product whose every file is missing, and must not be mistaken
    /// for one: what the shop showed before is shown instead - the square and the promo, the two sizes the older
    /// products really have - rather than a product page with no picture at all.
    /// </summary>
    [Fact]
    public void AFolderThatCannotBeReadShowsWhatTheShopShowedBefore()
    {
        var pictures = ProductGallery.Resolve(
            new[]
            {
                Row(ProductImageVariants.LargeImageTypeId, 1, "I10040_L_01.jpg"),
                Row(ProductImageVariants.SquareImageTypeId, 1, "I10040_S_01.jpg"),
                Row(ProductImageVariants.PromoImageTypeId, 1, "I10040_P_01.jpg"),
                Row(ProductImageVariants.ThumbnailImageTypeId, 1, "I10040_T_01.jpg"),
                Row(ProductImageVariants.SquareImageTypeId, 3, "I10040_S_03.jpg")
            },
            _ => false);

        Assert.Equal(new[] { "I10040_S_01.jpg", "I10040_S_03.jpg" }, pictures.Select(picture => picture.Large));
        Assert.All(pictures, picture => Assert.Equal(picture.Large, picture.Thumbnail));
    }

    /// <summary>
    /// The two lists the gallery draws from are the sizes the uploader stores and the order the storefront draws
    /// them in, best first. A size added to one without the other - a stored size no screen prefers, or a preference
    /// for a size that is never written - would leave the shop drawing a file that is not there.
    /// </summary>
    [Fact]
    public void TheTypesDrawnFromAreTheOnesTheShopStoresThemAs()
    {
        Assert.Equal(
            new[]
            {
                ProductImageVariants.LargeImageTypeId,
                ProductImageVariants.SquareImageTypeId,
                ProductImageVariants.PromoImageTypeId
            },
            ProductGallery.LargePreference);

        Assert.Equal(
            new[]
            {
                ProductImageVariants.ThumbnailImageTypeId,
                ProductImageVariants.SquareImageTypeId,
                ProductImageVariants.PromoImageTypeId
            },
            ProductGallery.ThumbnailPreference);

        // Every size a frame falls back to is one an upload writes unless the admin unticks it, so a photo uploaded
        // from now on cannot fall through to a size nothing asked for.
        Assert.All(
            ProductGallery.LargePreference.Concat(ProductGallery.ThumbnailPreference),
            imageTypeId => Assert.Contains(
                ProductImageVariants.Default,
                variant => variant.ImageTypeId == imageTypeId));
    }

    /// <summary>
    /// The shop's image folder as it really stands for its own product 10040: only its square and its promo file
    /// were ever produced, so its L, R, M, T and later S rows point at files that are not there.
    /// </summary>
    private static bool Product10040Folder(string fileName)
        => fileName is "I10040_S_01.jpg" or "I10040_P_01.jpg";

    /// <summary>
    /// The folder of a product whose photos were uploaded from now on: every size an upload writes is there, for
    /// both of its photos.
    /// </summary>
    private static bool UploadedTodayFolder(string fileName)
        => fileName is "I10040_L_01.jpg" or "I10040_S_01.jpg" or "I10040_P_01.jpg" or "I10040_T_01.jpg"
            or "I10040_L_02.jpg" or "I10040_P_02.jpg";

    private static ProductImageRefDto Row(short imageTypeId, int imageIndex, string fileName)
        => new() { ImageTypeId = imageTypeId, ImageIndex = imageIndex, ImageUrl = fileName };
}
