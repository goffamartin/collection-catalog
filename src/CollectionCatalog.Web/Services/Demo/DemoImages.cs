using SkiaSharp;

namespace CollectionCatalog.Web.Services.Demo;

/// <summary>Kind of specimen drawn by <see cref="DemoImages"/>.</summary>
public enum SpecimenShape
{
    Crystals,
    Geode,
    Ammonite,
    Meteorite,
    Sphere,
}

/// <summary>
/// Photos for the demo collection, drawn in code – the repository ships no third-party pictures. Each specimen sits on
/// a studio backdrop with a soft shadow; the same seed always gives the same picture.
/// </summary>
public static class DemoImages
{
    private const int Width = 1200;
    private const int Height = 1500;

    /// <summary>JPEG of a specimen. <paramref name="detail"/> = a closer, rotated view for the second photo.</summary>
    public static byte[] Draw(SpecimenShape shape, SKColor color, int seed, bool detail = false)
    {
        var random = new Random(seed * 7919 + (detail ? 1 : 0));
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        var canvas = surface.Canvas;

        DrawBackdrop(canvas, random);

        canvas.Save();
        canvas.Translate(Width / 2f, Height * 0.54f);
        canvas.Scale(1.2f);
        if (detail)
        {
            canvas.Scale(1.4f);
            canvas.RotateDegrees(random.Next(-25, 25));
        }
        DrawShadow(canvas, shape);
        switch (shape)
        {
            case SpecimenShape.Crystals: DrawCrystals(canvas, color, random); break;
            case SpecimenShape.Geode: DrawGeode(canvas, color, random); break;
            case SpecimenShape.Ammonite: DrawAmmonite(canvas, color, random); break;
            case SpecimenShape.Meteorite: DrawMeteorite(canvas, color, random); break;
            case SpecimenShape.Sphere: DrawSphere(canvas, color, random); break;
        }
        canvas.Restore();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 88);
        return data.ToArray();
    }

    private static void DrawBackdrop(SKCanvas canvas, Random random)
    {
        // Seamless studio paper: warm light at the top, darker towards the bottom.
        var top = new SKColor(238, 232, 222);
        var bottom = new SKColor((byte)(196 + random.Next(10)), (byte)(186 + random.Next(10)), 172);
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, Height), [top, bottom], SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(0, 0, Width, Height, paint);

        using var vignette = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(new SKPoint(Width / 2f, Height * 0.45f), Height * 0.75f,
                [SKColors.Transparent, new SKColor(0, 0, 0, 70)], SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(0, 0, Width, Height, vignette);
    }

    private static void DrawShadow(SKCanvas canvas, SpecimenShape shape)
    {
        var width = shape == SpecimenShape.Crystals ? 760 : 640;
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(40, 30, 20, 90),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 38),
        };
        canvas.DrawOval(new SKRect(-width / 2f, 300, width / 2f, 390), paint);
    }

    private static void DrawCrystals(SKCanvas canvas, SKColor color, Random random)
    {
        // Matrix rock with a cluster of hexagonal prisms growing out of it.
        DrawRock(canvas, new SKRect(-380, 120, 380, 360), new SKColor(120, 108, 96), random);

        var count = 7 + random.Next(4);
        var crystals = Enumerable.Range(0, count)
            .Select(_ => (Angle: random.Next(-38, 38), Height: 300 + random.Next(330), Width: 70 + random.Next(60), Offset: random.Next(-230, 230)))
            .OrderBy(c => Math.Abs(c.Angle))
            .Reverse()
            .ToList();
        foreach (var crystal in crystals)
        {
            canvas.Save();
            canvas.Translate(crystal.Offset, 210);
            canvas.RotateDegrees(crystal.Angle);
            DrawPrism(canvas, crystal.Width, crystal.Height, Vary(color, random, 30));
            canvas.Restore();
        }
    }

    private static void DrawPrism(SKCanvas canvas, float width, float height, SKColor color)
    {
        var half = width / 2;
        var tip = height + width * 0.9f;
        // Three visible faces of a hexagonal prism with a pointed termination.
        var faces = new[]
        {
            (new SKPoint[] { new(-half, 0), new(-half * 0.35f, 0), new(-half * 0.35f, -height), new(0, -tip), new(-half, -height) }, Darker(color, 0.62f)),
            (new SKPoint[] { new(-half * 0.35f, 0), new(half * 0.35f, 0), new(half * 0.35f, -height), new(0, -tip), new(-half * 0.35f, -height) }, color),
            (new SKPoint[] { new(half * 0.35f, 0), new(half, 0), new(half, -height), new(0, -tip), new(half * 0.35f, -height) }, Lighter(color, 0.35f)),
        };
        foreach (var (points, faceColor) in faces)
        {
            var builder = new SKPathBuilder();
            builder.AddPoly(points, true);
            using var path = builder.Detach();
            using var fill = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, -tip),
                    [Darker(faceColor, 0.75f), faceColor.WithAlpha(235), Lighter(faceColor, 0.25f)], SKShaderTileMode.Clamp),
            };
            canvas.DrawPath(path, fill);
            using var edge = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = SKColors.White.WithAlpha(70) };
            canvas.DrawPath(path, edge);
        }
    }

    private static void DrawGeode(SKCanvas canvas, SKColor color, Random random)
    {
        var outer = new SKRect(-360, -330, 360, 330);
        DrawRock(canvas, outer, new SKColor(112, 100, 90), random, round: true);

        // Agate bands towards the hollow.
        for (var band = 0; band < 7; band++)
        {
            var inset = 26 + band * 20;
            var bandColor = band % 2 == 0 ? Lighter(color, 0.55f) : Darker(color, 0.85f);
            using var paint = new SKPaint { IsAntialias = true, Color = bandColor };
            canvas.DrawOval(SKRect.Inflate(outer, -inset, -inset), paint);
        }

        // Crystal points lining the hollow, pointing to the centre.
        var hollow = SKRect.Inflate(outer, -170, -170);
        using (var dark = new SKPaint { IsAntialias = true, Color = Darker(color, 0.35f) })
        {
            canvas.DrawOval(hollow, dark);
        }
        for (var i = 0; i < 140; i++)
        {
            var angle = random.NextDouble() * Math.PI * 2;
            var rx = hollow.Width / 2 * (0.55 + random.NextDouble() * 0.45);
            var ry = hollow.Height / 2 * (0.55 + random.NextDouble() * 0.45);
            var x = (float)(Math.Cos(angle) * rx);
            var y = (float)(Math.Sin(angle) * ry);
            var size = 18 + random.Next(26);
            var builder = new SKPathBuilder();
            builder.MoveTo(x, y);
            builder.LineTo(x * 0.55f + size * 0.4f, y * 0.55f);
            builder.LineTo(x * 0.55f - size * 0.4f, y * 0.55f + size * 0.3f);
            builder.Close();
            using var path = builder.Detach();
            using var paint = new SKPaint { IsAntialias = true, Color = Lighter(Vary(color, random, 25), (float)random.NextDouble() * 0.6f) };
            canvas.DrawPath(path, paint);
        }
        DrawGloss(canvas, hollow);
    }

    private static void DrawAmmonite(SKCanvas canvas, SKColor color, Random random)
    {
        // Logarithmic spiral shell with ribs.
        // Grows to a radius of about 300 px over the turns.
        const float growth = 0.108f;
        const float turns = 3.6f;
        var start = 26f;
        var spiral = new SKPathBuilder();
        for (var t = 0f; t <= turns * Math.PI * 2; t += 0.05f)
        {
            var r = start * MathF.Exp(growth * t);
            var point = new SKPoint(r * MathF.Cos(t), r * MathF.Sin(t));
            if (t == 0)
            {
                spiral.MoveTo(point);
            }
            else
            {
                spiral.LineTo(point);
            }
        }
        using var shell = spiral.Detach();
        var maxRadius = start * MathF.Exp(growth * turns * MathF.PI * 2);
        using (var body = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(new SKPoint(-maxRadius * 0.3f, -maxRadius * 0.3f), maxRadius * 1.4f,
                [Lighter(color, 0.45f), color, Darker(color, 0.5f)], SKShaderTileMode.Clamp),
        })
        {
            canvas.DrawCircle(0, 0, maxRadius, body);
        }
        using (var line = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 10, Color = Darker(color, 0.45f), StrokeCap = SKStrokeCap.Round })
        {
            canvas.DrawPath(shell, line);
        }
        for (var t = 1f; t <= turns * Math.PI * 2; t += 0.22f)
        {
            var r = start * MathF.Exp(growth * t);
            var rOuter = start * MathF.Exp(growth * (t + MathF.PI * 2));
            var from = new SKPoint(r * MathF.Cos(t), r * MathF.Sin(t));
            var to = new SKPoint(Math.Min(rOuter, maxRadius) * MathF.Cos(t), Math.Min(rOuter, maxRadius) * MathF.Sin(t));
            using var rib = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4, Color = Darker(color, 0.7f).WithAlpha(150) };
            canvas.DrawLine(from, to, rib);
        }
        DrawGloss(canvas, new SKRect(-maxRadius, -maxRadius, maxRadius, maxRadius));
        _ = random;
    }

    private static void DrawMeteorite(SKCanvas canvas, SKColor color, Random random)
    {
        DrawRock(canvas, new SKRect(-330, -280, 330, 300), color, random, round: true, dimples: 26);
        // Cut and polished window with a Widmanstätten pattern.
        var window = new SKRect(-120, -60, 140, 150);
        using (var metal = new SKPaint { IsAntialias = true, Color = new SKColor(178, 180, 184) })
        {
            canvas.DrawOval(window, metal);
        }
        canvas.Save();
        using (var clip = Oval(window))
        {
            canvas.ClipPath(clip, antialias: true);
        }
        using var stripe = new SKPaint { IsAntialias = true, StrokeWidth = 3, Color = new SKColor(120, 124, 130) };
        foreach (var angle in new[] { 30, 90, 150 })
        {
            for (var i = -12; i <= 12; i++)
            {
                canvas.Save();
                canvas.RotateDegrees(angle);
                canvas.DrawLine(-300, i * 22 + random.Next(-6, 6), 300, i * 22 + random.Next(-6, 6), stripe);
                canvas.Restore();
            }
        }
        canvas.Restore();
        DrawGloss(canvas, window);
    }

    private static void DrawSphere(SKCanvas canvas, SKColor color, Random random)
    {
        const float radius = 300;
        using (var stand = new SKPaint { IsAntialias = true, Color = new SKColor(60, 46, 36) })
        {
            canvas.DrawOval(new SKRect(-170, 250, 170, 330), stand);
        }
        using (var ball = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(new SKPoint(-radius * 0.35f, -radius * 0.4f), radius * 1.5f,
                [Lighter(color, 0.6f), color, Darker(color, 0.35f)], SKShaderTileMode.Clamp),
        })
        {
            canvas.DrawCircle(0, -20, radius, ball);
        }
        // Mineral veins inside.
        using var vein = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, Color = Lighter(color, 0.7f).WithAlpha(120) };
        canvas.Save();
        using (var clip = Oval(new SKRect(-radius, -radius - 20, radius, radius - 20)))
        {
            canvas.ClipPath(clip, antialias: true);
        }
        for (var i = 0; i < 9; i++)
        {
            vein.StrokeWidth = 3 + random.Next(8);
            var builder = new SKPathBuilder();
            var y = -radius + random.Next((int)(radius * 2));
            builder.MoveTo(-radius, y);
            builder.CubicTo(-radius / 3, y + random.Next(-160, 160), radius / 3, y + random.Next(-160, 160), radius, y + random.Next(-80, 80));
            using var path = builder.Detach();
            canvas.DrawPath(path, vein);
        }
        canvas.Restore();
        DrawGloss(canvas, new SKRect(-radius, -radius - 20, radius, radius - 20));
    }

    private static void DrawRock(SKCanvas canvas, SKRect bounds, SKColor color, Random random, bool round = false, int dimples = 0)
    {
        // Irregular outline around an ellipse, with a mottled surface.
        var shape = new SKPathBuilder();
        const int points = 28;
        for (var i = 0; i < points; i++)
        {
            var angle = i * Math.PI * 2 / points;
            var jitter = round ? 0.92 + random.NextDouble() * 0.12 : 0.8 + random.NextDouble() * 0.3;
            var point = new SKPoint(
                bounds.MidX + (float)(Math.Cos(angle) * bounds.Width / 2 * jitter),
                bounds.MidY + (float)(Math.Sin(angle) * bounds.Height / 2 * jitter));
            if (i == 0)
            {
                shape.MoveTo(point);
            }
            else
            {
                shape.LineTo(point);
            }
        }
        shape.Close();
        using var outline = shape.Detach();
        using (var fill = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(new SKPoint(bounds.MidX - bounds.Width * 0.2f, bounds.MidY - bounds.Height * 0.25f),
                Math.Max(bounds.Width, bounds.Height), [Lighter(color, 0.3f), color, Darker(color, 0.45f)], SKShaderTileMode.Clamp),
        })
        {
            canvas.DrawPath(outline, fill);
        }

        canvas.Save();
        canvas.ClipPath(outline, antialias: true);
        using var speck = new SKPaint { IsAntialias = true };
        for (var i = 0; i < 420; i++)
        {
            speck.Color = (random.Next(2) == 0 ? SKColors.Black : SKColors.White).WithAlpha((byte)random.Next(10, 40));
            canvas.DrawCircle(bounds.Left + random.Next((int)bounds.Width), bounds.Top + random.Next((int)bounds.Height), 2 + random.Next(9), speck);
        }
        for (var i = 0; i < dimples; i++)
        {
            var x = bounds.Left + random.Next((int)bounds.Width);
            var y = bounds.Top + random.Next((int)bounds.Height);
            var r = 24 + random.Next(30);
            using var dimple = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateRadialGradient(new SKPoint(x + r * 0.3f, y + r * 0.3f), r,
                    [Darker(color, 0.4f).WithAlpha(160), SKColors.Transparent], SKShaderTileMode.Clamp),
            };
            canvas.DrawCircle(x, y, r, dimple);
        }
        canvas.Restore();
    }

    private static SKPath Oval(SKRect bounds)
    {
        var builder = new SKPathBuilder();
        builder.AddOval(bounds);
        return builder.Detach();
    }

    private static void DrawGloss(SKCanvas canvas, SKRect bounds)
    {
        using var gloss = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(new SKPoint(bounds.MidX - bounds.Width * 0.22f, bounds.MidY - bounds.Height * 0.28f),
                bounds.Width * 0.35f, [SKColors.White.WithAlpha(90), SKColors.Transparent], SKShaderTileMode.Clamp),
        };
        canvas.DrawOval(bounds, gloss);
    }

    private static SKColor Vary(SKColor color, Random random, int amount) => new(
        (byte)Math.Clamp(color.Red + random.Next(-amount, amount), 0, 255),
        (byte)Math.Clamp(color.Green + random.Next(-amount, amount), 0, 255),
        (byte)Math.Clamp(color.Blue + random.Next(-amount, amount), 0, 255));

    private static SKColor Lighter(SKColor color, float amount) => new(
        (byte)(color.Red + (255 - color.Red) * amount),
        (byte)(color.Green + (255 - color.Green) * amount),
        (byte)(color.Blue + (255 - color.Blue) * amount),
        color.Alpha);

    private static SKColor Darker(SKColor color, float factor) => new(
        (byte)(color.Red * factor), (byte)(color.Green * factor), (byte)(color.Blue * factor), color.Alpha);
}
