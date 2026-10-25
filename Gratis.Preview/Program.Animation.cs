using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;
using SkiaSharp;

namespace Gratis.Preview;

internal static partial class Program {
    private static bool TryAnimationScene(string[] args) {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || !args[at + 1].StartsWith("anim-", StringComparison.Ordinal))
            return false;
        var name = args[at + 1];
        if (name is not ("anim-popover-strip" or "anim-search-strip" or "anim-toggle-strip" or "anim-scroll-strip"
            or "anim-unfade-strip" or "anim-unfade-ghost-strip"))
            throw new ArgumentException("Unknown animation scene: " + name);
        var outAt = Array.IndexOf(args, "--out");
        var output = Path.GetFullPath(outAt < 0 ? Path.Combine("_preview", name + ".png") : args[outAt + 1]);
        var frames = Path.Combine(Path.GetDirectoryName(output)!, name + "-frames");
        Directory.CreateDirectory(frames);
        var paths = new List<string>();
        double[] samples = [0, .04, .09, .18, .28, .33, .38, .60];
        if (name == "anim-toggle-strip") {
            foreach (var (sample, index) in samples.Select((s, i) => (s, i))) {
                var path = Path.Combine(frames, index + ".png");
                RenderSettings(Options.Parse(["--scene", "settings-chrome", "--size", "640x480", "--out", path])!,
                    (layout, config, frame) => {
                        layout.Search = "PinChrome"; config.PinChrome = frame >= 3 && frame < 22;
                    },
                    frames: 4 + (int)Math.Round(sample * 60));
                paths.Add(path);
            }
        }
        else {
            var c = ChromeLayoutVerification.Config(new(520, 420));
            c.ReduceMotion = false;
            var unfade = name.Contains("unfade");
            var ghost = name.Contains("ghost");
            if (unfade) {
                c.FadeMode = FadeMode.Always;
                c.FadePercent = .2f;
                c.RestoreOpacityOnHover = true;
            }
            if (ghost) {
                c.GhostMode = true;
                c.GhostHintSeen = true;
            }
            using var d = new ScriptedFrameDriver(c, new FrameScript {
                Width = 584, Height = 484, Palette = true,
                PlayerCoordinates = new(18.4f, 23.7f), Markers = [new(60453, new(-80, 0))]
            });
            var ui = d.Context.Assistance = new MapAssistance(NullTravelProvider.Instance, new TravelFixture(d),
                () => true, () => { });
            d.Step(new(0, Focused: !ghost, Moving: ghost));
            d.Step(new(.3, Moving: ghost));
            if (name.Contains("popover"))
                ui.HoverPlace(TravelFixture.Hub, d.Renderer.WorldToScreen(new(-80, 0), d.Window.MapDrawOffset));
            if (name.Contains("search") || name.Contains("scroll"))
                d.Palette!.Open(name.Contains("scroll") ? "a:" : "limsa");
            if (name.Contains("scroll")) {
                d.Step(new(.31));
                d.Step(new(.6));
            }
            var began = name.Contains("scroll") ? .62 : .32;
            for (var i = 0; i < samples.Length; i++) {
                var close = i >= 5 && !name.Contains("scroll");
                if (i == 5 && name.Contains("search"))
                    d.Palette!.Close();
                var mouse = unfade ? close ? new Vector2(-1000) : c.WindowPosition + new Vector2(24, 170)
                    : name.Contains("scroll") ? d.Palette!.PanelMin + new Vector2(80, 150) : new Vector2(-1000);
                d.Step(new(began + samples[i], Mouse: mouse, Moving: ghost,
                    Wheel: name.Contains("scroll") && i == 1 ? -3 : 0, Escape: name.Contains("popover") && i == 5));
                var path = Path.Combine(frames, i + ".png");
                d.Save(path);
                paths.Add(path);
            }
        }
        using var first = SKBitmap.Decode(paths[0]);
        using var strip = new SKBitmap(first.Width * paths.Count, first.Height + 28);
        using var canvas = new SKCanvas(strip);
        canvas.Clear(new SKColor(16, 18, 23));
        using var paint = new SKPaint { Color = new SKColor(220, 224, 232), IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 12);
        for (var i = 0; i < paths.Count; i++) {
            using var bitmap = SKBitmap.Decode(paths[i]);
            canvas.DrawBitmap(bitmap, i * first.Width, 28);
            canvas.DrawText($"{samples[i] * 1000:0} ms{(i == 5 && !name.Contains("scroll") ? " · exit" : "")}",
                i * first.Width + 12, 18, font, paint);
        }
        using var image = SKImage.FromBitmap(strip);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(output);
        data.SaveTo(file);
        Console.WriteLine(output);
        return true;
    }
}