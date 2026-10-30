using SkiaSharp;

namespace CollectionCatalog.Tests.Infrastructure;

/// <summary>
/// Generator of test images.
/// </summary>
public static class TestImages
{
    /// <summary>
    /// JPEG with the left half red and the right half blue – shows how it was rotated.
    /// </summary>
    public static byte[] SplitJpeg(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            using var red = new SKPaint { Color = SKColors.Red };
            using var blue = new SKPaint { Color = SKColors.Blue };
            canvas.DrawRect(0, 0, width / 2f, height, red);
            canvas.DrawRect(width / 2f, 0, width / 2f, height, blue);
        }
        return Encode(bitmap, SKEncodedImageFormat.Jpeg);
    }

    public static byte[] TransparentPng(int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.Transparent);
        return Encode(bitmap, SKEncodedImageFormat.Png);
    }

    /// <summary>
    /// Inserts an APP1 segment with EXIF into a JPEG: orientation and GPS (GPSLatitudeRef = "N"),
    /// like photos from a phone.
    /// </summary>
    public static byte[] WithExif(byte[] jpeg, ushort orientation)
    {
        // TIFF (big endian): header, IFD0 with 2 entries (Orientation, pointer to the GPS IFD), GPS IFD with 1 entry.
        byte[] tiff =
        [
            (byte)'M', (byte)'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08,
            // IFD0 @8
            0x00, 0x02,
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, (byte)(orientation >> 8), (byte)orientation, 0x00, 0x00,
            0x88, 0x25, 0x00, 0x04, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x26,
            0x00, 0x00, 0x00, 0x00,
            // GPS IFD @38
            0x00, 0x01,
            0x00, 0x01, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02, (byte)'N', 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];

        byte[] exifHeader = [(byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x00, 0x00];
        var segmentLength = 2 + exifHeader.Length + tiff.Length;

        using var result = new MemoryStream();
        result.Write(jpeg, 0, 2); // SOI
        result.Write([0xFF, 0xE1, (byte)(segmentLength >> 8), (byte)segmentLength]);
        result.Write(exifHeader);
        result.Write(tiff);
        result.Write(jpeg, 2, jpeg.Length - 2);
        return result.ToArray();
    }

    public static bool ContainsExif(byte[] data) =>
        data.AsSpan().IndexOf("Exif\0\0"u8) >= 0;

    public static SKBitmap Decode(byte[] data) =>
        SKBitmap.Decode(data) ?? throw new InvalidOperationException("The image cannot be decoded.");

    private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 95);
        return data.ToArray();
    }
}
