using CollectionCatalog.Tests.Infrastructure;
using CollectionCatalog.Web.Services.Images;
using SkiaSharp;

namespace CollectionCatalog.Tests.Images;

[TestClass]
public sealed class ImageStoreTests
{
    private TestEnvironment _env = null!;

    [TestInitialize]
    public async Task Initialize() => _env = await TestEnvironment.CreateAsync();

    [TestCleanup]
    public async Task Cleanup() => await _env.DisposeAsync();

    [TestMethod]
    [DataRow((ushort)1, 300, 200, "left", DisplayName = "1 – no rotation")]
    [DataRow((ushort)3, 300, 200, "right", DisplayName = "3 – rotated by 180°")]
    [DataRow((ushort)6, 200, 300, "top", DisplayName = "6 – rotate 90° clockwise")]
    [DataRow((ushort)8, 200, 300, "bottom", DisplayName = "8 – rotate 90° counter-clockwise")]
    public async Task Process_AppliesExifOrientation(ushort orientation, int expectedWidth, int expectedHeight, string redSide)
    {
        var source = TestImages.WithExif(TestImages.SplitJpeg(300, 200), orientation);

        var processed = await _env.ImageStore.ProcessAsync(new MemoryStream(source));

        using var master = TestImages.Decode(processed.Master);
        Assert.AreEqual(expectedWidth, master.Width);
        Assert.AreEqual(expectedHeight, master.Height);

        var (x, y) = redSide switch
        {
            "left" => (master.Width / 4, master.Height / 2),
            "right" => (master.Width * 3 / 4, master.Height / 2),
            "top" => (master.Width / 2, master.Height / 4),
            _ => (master.Width / 2, master.Height * 3 / 4),
        };
        var pixel = master.GetPixel(x, y);
        Assert.IsTrue(pixel.Red > 200 && pixel.Blue < 60, $"Expected red on the {redSide} side, found {pixel}.");
    }

    [TestMethod]
    [DataRow(true, "top", DisplayName = "clockwise – left side up")]
    [DataRow(false, "bottom", DisplayName = "counter-clockwise – left side down")]
    public async Task Rotate_TurnsImageByQuarter(bool clockwise, string redSide)
    {
        var original = await _env.ImageStore.ProcessAsync(new MemoryStream(TestImages.SplitJpeg(300, 200)));
        await _env.ImageStore.WriteAsync(original);

        var rotated = await _env.ImageStore.RotateAsync(original.FileKey, clockwise);

        Assert.AreEqual(200, rotated.Width);
        Assert.AreEqual(300, rotated.Height);
        Assert.AreNotEqual(original.FileKey, rotated.FileKey);
        using var master = TestImages.Decode(rotated.Master);
        var pixel = master.GetPixel(master.Width / 2, redSide == "top" ? master.Height / 4 : master.Height * 3 / 4);
        Assert.IsTrue(pixel.Red > 200 && pixel.Blue < 60, $"Expected red ({redSide}), found {pixel}.");
    }

    [TestMethod]
    public async Task Process_StripsExifIncludingGps()
    {
        var source = TestImages.WithExif(TestImages.SplitJpeg(300, 200), orientation: 1);
        Assert.IsTrue(TestImages.ContainsExif(source), "Precondition: the input contains EXIF.");

        var processed = await _env.ImageStore.ProcessAsync(new MemoryStream(source));

        Assert.IsFalse(TestImages.ContainsExif(processed.Master));
        foreach (var variant in processed.Variants.Values)
        {
            Assert.IsFalse(TestImages.ContainsExif(variant));
        }
    }

    [TestMethod]
    public async Task Process_LimitsMasterAndCreatesVariants()
    {
        var processed = await _env.ImageStore.ProcessAsync(new MemoryStream(TestImages.SplitJpeg(3000, 1500)));

        Assert.AreEqual(ImageStore.MasterMaxSize, processed.Width);
        Assert.AreEqual(1280, processed.Height);
        CollectionAssert.AreEquivalent(ImageStore.VariantSizes.ToList(), processed.Variants.Keys.ToList());

        foreach (var (size, data) in processed.Variants)
        {
            using var codec = SKCodec.Create(new MemoryStream(data));
            Assert.AreEqual(SKEncodedImageFormat.Webp, codec.EncodedFormat);
            Assert.AreEqual(size, codec.Info.Width, $"Variant {size}: longer side.");
            Assert.AreEqual(size / 2, codec.Info.Height, $"Variant {size}: aspect ratio kept.");
        }
    }

    [TestMethod]
    public async Task Process_SmallImage_IsNotUpscaled()
    {
        var processed = await _env.ImageStore.ProcessAsync(new MemoryStream(TestImages.SplitJpeg(300, 200)));

        Assert.AreEqual(300, processed.Width);
        using var codec = SKCodec.Create(new MemoryStream(processed.Variants[1600]));
        Assert.AreEqual(300, codec.Info.Width);
    }

    [TestMethod]
    public async Task Process_TransparentPng_GetsWhiteBackground()
    {
        var processed = await _env.ImageStore.ProcessAsync(new MemoryStream(TestImages.TransparentPng(100, 100)));

        using var master = TestImages.Decode(processed.Master);
        var pixel = master.GetPixel(50, 50);
        Assert.IsTrue(pixel.Red > 245 && pixel.Green > 245 && pixel.Blue > 245, $"Expected white, found {pixel}.");
    }

    [TestMethod]
    public async Task Process_InvalidData_Throws()
    {
        await Assert.ThrowsExactlyAsync<InvalidImageException>(
            () => _env.ImageStore.ProcessAsync(new MemoryStream("not an image"u8.ToArray())));
    }

    [TestMethod]
    public async Task Process_SameImage_ProducesSameKey()
    {
        var source = TestImages.SplitJpeg(300, 200);

        var first = await _env.ImageStore.ProcessAsync(new MemoryStream(source));
        var second = await _env.ImageStore.ProcessAsync(new MemoryStream(source));

        Assert.AreEqual(first.FileKey, second.FileKey);
    }

    [TestMethod]
    public async Task Write_CreatesFiles_AndSkipsExisting()
    {
        var processed = await _env.ImageStore.ProcessAsync(new MemoryStream(TestImages.SplitJpeg(300, 200)));

        Assert.IsTrue(await _env.ImageStore.WriteAsync(processed));
        Assert.IsFalse(await _env.ImageStore.WriteAsync(processed));

        Assert.IsTrue(File.Exists(_env.ImageStore.GetMasterPath(processed.FileKey)));
        foreach (var size in ImageStore.VariantSizes)
        {
            Assert.IsTrue(File.Exists(_env.ImageStore.GetVariantPath(processed.FileKey, size)));
        }
        Assert.IsFalse(_env.StoredImageFiles().Any(f => f.EndsWith(".tmp", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task Delete_RemovesAllFiles()
    {
        var processed = await _env.ImageStore.ProcessAsync(new MemoryStream(TestImages.SplitJpeg(300, 200)));
        await _env.ImageStore.WriteAsync(processed);

        _env.ImageStore.Delete(processed.FileKey);

        Assert.IsFalse(_env.StoredImageFiles().Any());
    }
}
