using System.Numerics;
using Gratis.Rendering;
using ImGuiNET;
using SkiaSharp;

namespace Gratis.Preview;

internal static partial class Program
{
    private static double headingCaptureSeconds;
    private static bool headingCaptureRaw;

    private static void RenderHeadingSheet(Options options)
    {
        const int width = 320, height = 240, caption = 36;
        var directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(options.Output))!, "player-heading");
        Directory.CreateDirectory(directory);
        using var bitmap = new SKBitmap(width * 5, (height + caption) * 2);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(22, 25, 31));
        using var paint = new SKPaint { Color = new SKColor(224, 231, 238), IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 18);
        int[] milliseconds = [0, 50, 100, 200, 400];
        for (var row = 0; row < 2; row++) {
            for (var i = 0; i < milliseconds.Length; i++) {
                headingCaptureSeconds = milliseconds[i] / 1000d;
                headingCaptureRaw = row == 0;
                var path = Path.Combine(directory, $"{row}-{milliseconds[i]}.png");
                Run(options with { Scene = "player-heading-frame", Output = path, Width = width, Height = height, Verify = false });
                using var tile = SKBitmap.Decode(path);
                var x = i * width; var y = row * (height + caption);
                canvas.DrawText($"{(row == 0 ? "Raw" : "Medium / 540 deg/s")} / {milliseconds[i]} ms", x + 12, y + 25,
                    SKTextAlign.Left, font, paint);
                canvas.DrawBitmap(tile, x, y + caption);
            }
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(options.Output);
        data.SaveTo(stream);
        Console.WriteLine($"{options.Output}: raw vs Medium; scripted 720-degree turn over 400ms; shared arrow/cone heading.");
    }

    private static void DrawHeadingFrame(Vector2 size, DrawTexture map)
    {
        var scale = MathF.Max(size.X / map.Size.X, size.Y / map.Size.Y);
        ImGui.SetCursorPos((size - map.Size * scale) / 2);
        MapTextureDrawing.DrawMapImage(map, scale);
        var heading = new PlayerHeading();
        static float Raw(double time) => (float)(-Math.PI / 2 + Math.Min(time, .4) / .4 * Math.PI * 4);
        var angle = heading.Advance(Raw(0), 0, PlayerArrowSmoothing.Medium, 1, Vector3.Zero);
        var steps = (int)Math.Round(headingCaptureSeconds * 1000);
        for (var i = 1; i <= steps; i++)
            angle = heading.Advance(Raw(i / 1000d), i / 1000d, PlayerArrowSmoothing.Medium, 1, Vector3.Zero);
        if (headingCaptureRaw) angle = Raw(headingCaptureSeconds);
        var position = ImGui.GetWindowPos() + size / 2;
        MapDrawContext.Current.Config.SoftConeLength = 65;
        PlayerDrawing.DrawLookLine(position, 1, angle);
        PlayerDrawing.DrawPlayerIcon(position, MapDrawContext.Current.GetIcon(60443), angle, 1);
    }
}
