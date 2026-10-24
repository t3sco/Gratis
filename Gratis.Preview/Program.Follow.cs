using System.Numerics;
using Gratis.Classes;
using Gratis.Rendering;
using ImGuiNET;
using SkiaSharp;

namespace Gratis.Preview;

internal static partial class Program
{
    private static readonly string[] FollowScenes = ["follow-return", "follow-return-frame"];
    private static double followCaptureSeconds;

    private static void RenderFollowReturnSheet(Options options)
    {
        const int width = 576, height = 400, caption = 34;
        var directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(options.Output))!, "follow-return");
        Directory.CreateDirectory(directory);
        using var bitmap = new SKBitmap(width * 4, (height + caption) * 2);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(22, 25, 31));
        using var paint = new SKPaint { Color = new SKColor(224, 231, 238), IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 18);
        int[] milliseconds = [0, 100, 250, 400];
        for (var shape = 0; shape < 2; shape++) {
            for (var i = 0; i < milliseconds.Length; i++) {
                followCaptureSeconds = milliseconds[i] / 1000.0;
                var path = Path.Combine(directory, $"{shape}-{milliseconds[i]}.png");
                Run(options with { Scene = "follow-return-frame", Output = path, Width = width, Height = height,
                    Circle = shape == 1, Verify = false });
                using var tile = SKBitmap.Decode(path);
                var x = i * width; var y = shape * (height + caption);
                canvas.DrawText($"{(shape == 0 ? "Rectangle" : "Circle")} / {milliseconds[i]} ms / Follow ON", x + 24, y + 25,
                    SKTextAlign.Left, font, paint);
                canvas.DrawBitmap(tile, x, y + caption);
            }
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(options.Output);
        data.SaveTo(stream);
        Console.WriteLine($"{options.Output}: same viewport engine as the game; return at 0/100/250/400ms, 350ms spring, rectangle + circle, fixed user zoom.");
    }

    private static void DrawFollowReturnFrame(Vector2 size, DrawTexture texture)
    {
        var config = MapDrawContext.Current.Config;
        config.Motion.EaseFollow = false;
        var motion = new ViewportMotion();
        var scale = MathF.Max(size.X / texture.Size.X, size.Y / texture.Size.Y) * 2.5f;
        var target = new Vector2(1024) - texture.Size / 2;
        var offset = target;
        motion.Advance(config, 1, 0, 0, size, texture.Size, Vector2.Zero, target, false, false, false, ref scale, ref offset);
        motion.Interact(config, .01, true);
        offset = target + new Vector2(-120, 60);
        motion.Advance(config, 1, .02, 1, size, texture.Size, Vector2.Zero, target, true, true, true, ref scale, ref offset);
        motion.Advance(config, 1, .03, 2, size, texture.Size, Vector2.One, target, false, false, false, ref scale, ref offset);
        var frames = Math.Max(1, (int)Math.Ceiling(followCaptureSeconds * 120));
        for (var i = 1; i <= frames; i++)
            motion.Advance(config, 1, .03 + followCaptureSeconds * i / frames, i + 2, size, texture.Size,
                Vector2.One, target, false, false, false, ref scale, ref offset);
        var origin = ViewportMath.DrawPosition(size, offset, scale);
        ImGui.SetCursorPos(origin);
        MapTextureDrawing.DrawMapImage(texture, scale);
        var player = ImGui.GetWindowPos() + origin + texture.Size / 2 * scale;
        PlayerDrawing.DrawLookLine(player, .8f, -1.25f);
        PlayerDrawing.DrawPlayerIcon(player, MapDrawContext.Current.GetIcon(60443), .25f, 1);
        var center = ImGui.GetWindowPos() + size / 2;
        ImGui.GetWindowDrawList().AddCircle(center, 8, 0x66FFFFFF, 24, 1);
    }
}
