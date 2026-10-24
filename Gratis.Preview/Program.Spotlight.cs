using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;
using SkiaSharp;

namespace Gratis.Preview;

internal static partial class Program
{
    private static void RenderSpotlightSheet(Options options)
    {
        const int width = 576, height = 420, caption = 34;
        var circle = options.Scene == "spotlight-circular";
        var directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(options.Output))!, options.Scene);
        Directory.CreateDirectory(directory);
        using var sheet = new SKBitmap(width * 4, (height + caption) * 2);
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(new SKColor(20, 24, 31));
        using var paint = new SKPaint { Color = new SKColor(224, 231, 238), IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 16);
        double[] times = [0, .22, .7, 8.1];
        string[] labels = ["Quest Map / start", "Framing / 220 ms", "Pulse / 700 ms", "Fade / 8100 ms"];
        for (var row = 0; row < 2; row++) {
            var config = new SystemConfig {
                AcceptedSpoilerWarning = true, FadeMode = 0, HideWindowFrame = true,
                WindowPosition = new Vector2(24, 18), WindowSize = new Vector2(528, 384),
                MapCircle = circle, MapScale = .55f, FollowPlayer = true,
                PinChrome = true,
            };
            config.MapColour.Preset = row == 0 ? MapColourPreset.Mono : MapColourPreset.Midnight;
            var scene = new FrameScript {
                Width = width, Height = height, Player = new Vector2(-200, 110),
                Markers = [new(71001, new Vector2(300, -110), Objective: true),
                    new(60412, new Vector2(-80, -100)), new(60434, new Vector2(130, 100)),
                    new(60450, new Vector2(-270, -140)), new(61000, new Vector2(390, 140))],
            };
            using var driver = new ScriptedFrameDriver(config, scene);
            driver.Step(new(0)); driver.Step(new(.01));
            driver.Step(new(.02, QuestLink: new(new Vector2(300, -110), 1, Icon: 71001)));
            var time = .02;
            for (var column = 0; column < times.Length; column++) {
                var capture = .02 + times[column];
                while (time + 1.0 / 60 < capture) { time += 1.0 / 60; driver.Step(new(time)); }
                time = capture; driver.Step(new(time));
                var path = Path.Combine(directory, $"{(row == 0 ? "mono" : "midnight")}-{column}.png");
                driver.Save(path);
                using var tile = SKBitmap.Decode(path);
                var x = column * width; var y = row * (height + caption);
                canvas.DrawText($"{(row == 0 ? "Mono" : "Midnight")} / {labels[column]}", x + 20, y + 24, SKTextAlign.Left, font, paint);
                canvas.DrawBitmap(tile, x, y + caption);
            }
        }
        using var image = SKImage.FromBitmap(sheet);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var output = File.Create(options.Output);
        data.SaveTo(output);
        Console.WriteLine($"{options.Output}: shared quest Spotlight at start/framing/pulse/fade, Mono + Midnight, {(circle ? "circle" : "rectangle")}; tiles in {directory}");
    }
}
