using System.Security.Cryptography;
using Gratis.Rendering;
using SkiaSharp;

namespace Gratis.Preview;

internal static partial class Program {
    private static readonly string[] FrameScenes = ["chrome-tiny",
        "chrome-tiny-circle",
        "map-circular-nonsquare",
        "frame-presets"];

    private static readonly string[] FrameNames = ["Hairline",
        "Double ring",
        "Inset glow",
        "Notched corners",
        "Soft shadow",
        "None"];

    private static void RenderFramePresets(Options options) {
        const int cellWidth = 576, cellHeight = 400, caption = 34;
        var directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(options.Output))!, "frame-presets");
        Directory.CreateDirectory(directory);
        using var sheet = new SKBitmap(cellWidth * 2, (cellHeight + caption) * 6);
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(new SKColor(22, 25, 31));
        using var paint = new SKPaint { Color = new SKColor(224, 231, 238), IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 18);
        foreach (var preset in Enum.GetValues<FramePreset>()) {
            for (var shape = 0; shape < 2; shape++) {
                var circle = shape == 1;
                var output = Path.Combine(directory, $"{preset}-{(circle ? "circle" : "rect")}.png");
                Run(options with {
                    Scene = circle ? "map-circular" : "map-default",
                    Output = output,
                    Width = cellWidth,
                    Height = cellHeight,
                    Verify = false,
                    Frame = preset,
                    Circle = circle
                });
                using var tile = SKBitmap.Decode(output);
                var x = shape * cellWidth;
                var y = (int)preset * (cellHeight + caption);
                canvas.DrawText($"{FrameNames[(int)preset]} / {(circle ? "Circle" : "Rounded rectangle")}", x + 32,
                    y + 26, SKTextAlign.Left, font, paint);
                canvas.DrawBitmap(tile, x, y + caption);
            }
        }
        using var image = SKImage.FromBitmap(sheet);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(options.Output);
        data.SaveTo(stream);
        Console.WriteLine($"{options.Output}: six frame presets, rounded rectangle + circle, independent "
            + "preset edges, default feather enabled; shadow on viewport background.");
        if (options.Verify)
            VerifyFramePresets(options, directory);
    }

    private static void VerifyFramePresets(Options options, string directory) {
        var config = new MapDrawingConfig();
        var json = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        var saved = System.Text.Json.JsonSerializer.Deserialize<MapDrawingConfig>("{}", json)!;
        if (saved.FramePreset != FramePreset.Hairline)
            throw new InvalidOperationException("Missing frame preset changed defaults.");
        byte[]? classic = null;
        foreach (var preset in Enum.GetValues<FramePreset>()) {
            config.FramePreset = preset;
            var restored = System.Text.Json.JsonSerializer.Deserialize<MapDrawingConfig>(
                System.Text.Json.JsonSerializer.Serialize(config,
                json),
                json)!;
            if (restored.FramePreset != preset)
                throw new InvalidOperationException("Frame preset did not survive saving.");
            var output = Path.Combine(directory, $"Classic-{preset}.png");
            Run(options with {
                Scene = "map-classic",
                Output = output,
                Width = 1280,
                Height = 800,
                Verify = false,
                Frame = preset,
                Circle = false
            });
            var hash = SHA256.HashData(File.ReadAllBytes(output));
            classic ??= hash;
            if (!hash.AsSpan().SequenceEqual(classic))
                throw new InvalidOperationException($"Classic changed for {preset}.");
        }
        config.ResetFramePreset();
        if (config.FramePreset != FramePreset.Hairline)
            throw new InvalidOperationException("Frame reset failed.");
        config.FramePreset = FramePreset.DoubleRing;
        config.ResetFrameMaterials();
        if (config.FramePreset != FramePreset.Hairline)
            throw new InvalidOperationException("Materials reset omitted frame preset.");
        Console.WriteLine("Verified frame defaults, save/load, both resets, and identical Classic PNGs "
            + "across all six presets.");
    }
}