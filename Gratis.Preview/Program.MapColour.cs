using System.Numerics;
using System.Runtime.InteropServices;
using Gratis.Rendering;
using SkiaSharp;

namespace Gratis.Preview;

internal static partial class Program {
    private static readonly MapColourPreset[] SheetPresets = [MapColourPreset.Original, MapColourPreset.Midnight,
        MapColourPreset.Mono, MapColourPreset.Blueprint, MapColourPreset.Sepia, MapColourPreset.Custom];

    private static nint PrepareRecolourMap(string scene, Textures textures, nint id, bool procedural) {
        if (!scene.StartsWith("map-recolor-", StringComparison.Ordinal))
            return id;
        var image = textures.Get(id);
        var pixels = image.Pixels;
        if (procedural) {
            using var bitmap = new SKBitmap(new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888,
                SKAlphaType.Unpremul));
            Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
            using var canvas = new SKCanvas(bitmap);
            using var ink = new SKPaint { Color = new SKColor(36, 42, 31), IsAntialias = true };
            using var font = new SKFont(SKTypeface.Default, image.Width / 48f);
            canvas.DrawText("SUMMERFORD", image.Width * .24f, image.Height * .34f, SKTextAlign.Left, font, ink);
            canvas.DrawText("WEST ROAD", image.Width * .63f, image.Height * .64f, SKTextAlign.Left, font, ink);
            pixels = new byte[pixels.Length];
            Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
            id = textures.Add(image.Width, image.Height, pixels);
        }
        var preset = Enum.Parse<MapColourPreset>(scene["map-recolor-".Length..], true);
        ((PreviewContext)MapDrawContext.Current).ColourPreset = preset;
        var parameters = preset == MapColourPreset.Custom ? new MapColourSettings {
            Preset = preset, Brightness = .03f, Contrast = 1.08f,
            Saturation = .65f, HueShift = -12, Tint = new Vector3(.83f, .94f, 1), TintStrength = .2f
        }.Parameters : MapColour.Preset(preset);
        var result = MapColour.Apply(pixels, parameters);
        return ReferenceEquals(result, pixels) ? id : textures.Add(image.Width, image.Height, result);
    }

    private static void DrawRecolourMarkers(Vector2 size) {
        for (var i = 0; i < 6; i++)
            Marker(size * new Vector2(.22f + i * .11f, .82f), size, (uint)(60010 + i),
                ((MarkerCategory)(i + 1)).ToString(), null, category: (MarkerCategory)(i + 1));
    }

    private static void RenderRecolourSheet(Options options) {
        const int cellWidth = 640, cellHeight = 400, caption = 36;
        using var sheet = new SKBitmap(cellWidth * 2, (cellHeight + caption) * 3);
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(new SKColor(22, 25, 31));
        using var paint = new SKPaint { Color = new SKColor(224, 231, 238), IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 18);
        var directory = Path.GetDirectoryName(Path.GetFullPath(options.Output))!;
        Directory.CreateDirectory(directory);
        for (var i = 0; i < SheetPresets.Length; i++) {
            var preset = SheetPresets[i];
            var scene = "map-recolor-" + preset.ToString().ToLowerInvariant();
            var output = Path.Combine(directory, scene + "-sheet-tile.png");
            Run(options with {
                Scene = scene,
                Output = output,
                Width = cellWidth,
                Height = cellHeight,
                Verify = false
            });
            using var tile = SKBitmap.Decode(output);
            var x = i % 2 * cellWidth;
            var y = i / 2 * (cellHeight + caption);
            canvas.DrawText(preset == MapColourPreset.Custom ? "Custom — cool paper example" : preset.ToString(),
                x + 32, y + 26, SKTextAlign.Left, font, paint);
            canvas.DrawBitmap(tile, x, y + caption);
        }
        using var image = SKImage.FromBitmap(sheet);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(options.Output);
        data.SaveTo(stream);
        Console.WriteLine($"{options.Output}: 2 × 3 Map Colour contact sheet.");
        if (options.Verify)
            MapColourVerification.Run();
    }
}