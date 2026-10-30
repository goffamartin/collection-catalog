using System.Security.Cryptography;
using CollectionCatalog.Web.Infrastructure;
using SkiaSharp;

namespace CollectionCatalog.Web.Services.Images;

/// <summary>
/// Processing and storage of item photos.
/// </summary>
/// <remarks>
/// Every photo is stored as a master JPEG (max. <see cref="MasterMaxSize"/> px) and WebP variants
/// for the web (<see cref="VariantSizes"/>, longer side). File names are the hash of the master's content,
/// so files are immutable (cacheable forever) and the same photo is stored only once.
/// Processing rotates the photo by its EXIF orientation and converts it to sRGB. The output is re-encoded
/// without metadata, so <b>EXIF including the GPS location never reaches the stored files</b>.
/// </remarks>
public sealed class ImageStore(StorageOptions storage)
{
    public const int MasterMaxSize = 2560;
    public static readonly IReadOnlyList<int> VariantSizes = [400, 800, 1600];

    private const int MasterQuality = 90;
    private const int VariantQuality = 80;

    // Mipmaps give a good result even for large downscaling (e.g. 4000 → 400 px).
    private static readonly SKSamplingOptions _sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    /// <summary>
    /// Reads an image, normalizes it and prepares the master and the variants in memory. Writes nothing to disk.
    /// </summary>
    /// <exception cref="InvalidImageException">Unsupported format or a corrupted image.</exception>
    public async Task<ProcessedImage> ProcessAsync(Stream source, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        using var decoded = Decode(buffer, out var origin);
        using var oriented = ApplyOrigin(decoded, origin);
        return Build(oriented);
    }

    /// <summary>
    /// Prepares a rotated version of a stored image (by 90° clockwise / counterclockwise).
    /// The result has a new <see cref="ProcessedImage.FileKey"/>; the original files stay – the caller deletes them.
    /// </summary>
    public async Task<ProcessedImage> RotateAsync(string fileKey, bool clockwise, CancellationToken cancellationToken = default)
    {
        var masterBytes = await File.ReadAllBytesAsync(GetMasterPath(fileKey), cancellationToken);
        using var buffer = new MemoryStream(masterBytes);
        using var decoded = Decode(buffer, out _);

        // The same transformation as for EXIF orientation: RightTop = rotate right, LeftBottom = left.
        using var rotated = ApplyOrigin(decoded, clockwise ? SKEncodedOrigin.RightTop : SKEncodedOrigin.LeftBottom);
        return Build(rotated);
    }

    /// <summary>
    /// Creates the master JPEG and the WebP variants from a correctly oriented bitmap.
    /// </summary>
    private static ProcessedImage Build(SKBitmap oriented)
    {
        var (width, height) = FitWithin(oriented.Width, oriented.Height, MasterMaxSize);
        using var master = Render(oriented, width, height);
        var masterBytes = Encode(master, SKEncodedImageFormat.Jpeg, MasterQuality);
        var fileKey = Convert.ToHexStringLower(SHA256.HashData(masterBytes))[..32];

        var variants = new Dictionary<int, byte[]>();
        foreach (var size in VariantSizes)
        {
            var (variantWidth, variantHeight) = FitWithin(master.Width, master.Height, size);
            using var variant = Render(master, variantWidth, variantHeight);
            variants[size] = Encode(variant, SKEncodedImageFormat.Webp, VariantQuality);
        }

        return new ProcessedImage(fileKey, master.Width, master.Height, masterBytes, variants);
    }

    /// <summary>
    /// Writes a processed image to disk.
    /// </summary>
    /// <returns><c>false</c> when the same image already existed (nothing was written).</returns>
    public async Task<bool> WriteAsync(ProcessedImage image, CancellationToken cancellationToken = default)
    {
        var masterPath = GetMasterPath(image.FileKey);
        if (File.Exists(masterPath))
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(masterPath)!);

        foreach (var (size, data) in image.Variants)
        {
            await WriteAtomicallyAsync(GetVariantPath(image.FileKey, size), data, cancellationToken);
        }

        // The master last – its existence means the image is complete.
        await WriteAtomicallyAsync(masterPath, image.Master, cancellationToken);
        return true;
    }

    /// <summary>
    /// Deletes all files of an image. The caller is responsible for <paramref name="fileKey"/> not being
    /// referenced by any record anymore (the same photo can belong to several items).
    /// </summary>
    public void Delete(string fileKey)
    {
        File.Delete(GetMasterPath(fileKey));
        foreach (var size in VariantSizes)
        {
            File.Delete(GetVariantPath(fileKey, size));
        }
    }

    public string GetMasterPath(string fileKey) => Path.Combine(GetDirectory(fileKey), $"{fileKey}.jpg");

    public string GetVariantPath(string fileKey, int size) => Path.Combine(GetDirectory(fileKey), $"{fileKey}_{size}.webp");

    private string GetDirectory(string fileKey) => Path.Combine(storage.ImagesPath, fileKey[..2]);

    private static SKBitmap Decode(Stream stream, out SKEncodedOrigin origin)
    {
        using var codec = SKCodec.Create(stream, out var createResult)
            ?? throw new InvalidImageException($"Unsupported or corrupted image ({createResult}).");

        origin = codec.EncodedOrigin;

        var info = codec.Info
            .WithColorType(SKColorType.Rgba8888)
            .WithAlphaType(SKAlphaType.Premul)
            .WithColorSpace(SKColorSpace.CreateSrgb());

        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bitmap.Dispose();
            throw new InvalidImageException($"The image cannot be decoded ({result}).");
        }

        return bitmap;
    }

    /// <summary>
    /// Rotates / mirrors the bitmap by its EXIF orientation, so it is upright as it was photographed.
    /// </summary>
    private static SKBitmap ApplyOrigin(SKBitmap source, SKEncodedOrigin origin)
    {
        var swapsAxes = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = swapsAxes ? source.Height : source.Width;
        var height = swapsAxes ? source.Width : source.Height;

        // Maps a source point (x, y) to the target: x' = sx*x + kx*y + tx, y' = ky*x + sy*y + ty
        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, width, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, width, -1, 0, height, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, height, 0, 0, 1),
            _ => SKMatrix.Identity,
        };

        var result = new SKBitmap(new SKImageInfo(width, height, source.ColorType, source.AlphaType, source.ColorSpace));
        using var canvas = new SKCanvas(result);
        using var image = SKImage.FromBitmap(source);
        canvas.SetMatrix(matrix);
        canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default);
        return result;
    }

    /// <summary>
    /// Renders the image in the given size on a white background (JPEG has no transparency; a transparent PNG would turn black).
    /// </summary>
    private static SKBitmap Render(SKBitmap source, int width, int height)
    {
        var result = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb()));
        using var canvas = new SKCanvas(result);
        using var image = SKImage.FromBitmap(source);
        canvas.Clear(SKColors.White);
        canvas.DrawImage(image, SKRect.Create(width, height), _sampling);
        return result;
    }

    private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality)
            ?? throw new InvalidOperationException($"Encoding to {format} failed.");
        return data.ToArray();
    }

    private static (int Width, int Height) FitWithin(int width, int height, int maxSize)
    {
        if (width <= maxSize && height <= maxSize)
        {
            return (width, height);
        }

        var ratio = Math.Min((double)maxSize / width, (double)maxSize / height);
        return (Math.Max(1, (int)Math.Round(width * ratio)), Math.Max(1, (int)Math.Round(height * ratio)));
    }

    private static async Task WriteAtomicallyAsync(string path, byte[] data, CancellationToken cancellationToken)
    {
        var tempPath = path + ".tmp";
        await File.WriteAllBytesAsync(tempPath, data, cancellationToken);
        File.Move(tempPath, path, overwrite: true);
    }
}

/// <summary>
/// Processed image ready to be written.
/// </summary>
public sealed record ProcessedImage(string FileKey, int Width, int Height, byte[] Master, IReadOnlyDictionary<int, byte[]> Variants);

/// <summary>
/// The file is not a supported image or it is corrupted.
/// </summary>
public sealed class InvalidImageException(string message) : Exception(message);
