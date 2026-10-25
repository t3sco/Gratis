using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;

namespace Gratis.Preview;

internal static class SmallInstanceVerification
{
    internal static nint CreateTexture(Textures textures)
    {
        const int side = 2048;
        var pixels = new byte[side * side * 4];
        for (var y = 0; y < side; y++) for (var x = 0; x < side; x++) {
            var p = (y * side + x) * 4;
            var room = x is >= 736 and < 1312 && y is >= 704 and < 1344;
            var wall = room && (x < 752 || x >= 1296 || y < 720 || y >= 1328);
            var tile = (x / 32 + y / 32) % 2;
            pixels[p] = (byte)(room ? wall ? 155 : 95 + tile * 8 : 25);
            pixels[p + 1] = (byte)(room ? wall ? 169 : 120 + tile * 8 : 36);
            pixels[p + 2] = (byte)(room ? wall ? 175 : 132 + tile * 8 : 47);
            pixels[p + 3] = 255;
        }
        return textures.Add(side, side, pixels);
    }
    internal static FrameScript Scene(bool wide = false) => new() {
        Width = wide ? 1280 : 760, Height = wide ? 760 : 1120,
        SmallInstance = true, MapSizeFactor = 2, MapId = 999,
        Title = "The Backroom · small instance", Player = new(-20, 80),
        Markers = [new(60071, new(80, 20), Objective: true), new(60460, new(-90, -50))],
    };
    internal static SystemConfig Config(bool wide = false) => new() {
        AcceptedSpoilerWarning = true, FadeMode = 0,
        WindowPosition = new(32), WindowSize = wide ? new(1160, 620) : new(540, 1030),
        MapScale = 90, ScaleWithZoom = true, RememberMapView = false, ReduceMotion = true, AutoHideChrome = false,
    };
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public static void Run()
    {
        foreach (var wide in new[] { false, true }) {
            using var driver = new ScriptedFrameDriver(Config(wide), Scene(wide));
            driver.Step(new(0)); driver.Step(new(.1));
            var renderer = driver.Renderer; var config = driver.Config; var bounds = driver.ContentBounds!.Value;
            Check(bounds.IsSmall(new(2048)) && bounds.Min.X is >= 720 and <= 736 && bounds.Max.Y is >= 1344 and <= 1360,
                "Synthetic non-background bounds must preserve the room");
            Check(renderer.ZoomLimits.Small && MathF.Abs(renderer.Scale - renderer.ZoomLimits.Fit) < .001f, "Small room must start at padded content fit: " + renderer.ViewportDiagnostics());
            var origin = renderer.DrawPosition;
            Check((origin + bounds.Min * renderer.Scale).X >= 30 && (origin + bounds.Max * renderer.Scale).X <= renderer.ViewportSize.X - 30,
                "Room must fit the narrow or wide frame with padding");
            foreach (var request in new[] { .0001f, 1000, float.NaN, 2, 80 }) {
                renderer.SetViewportScale(request); renderer.CenterOnCoordinate(new(10000, -10000));
                Check(renderer.Scale >= renderer.ZoomLimits.Minimum && renderer.Scale <= renderer.ZoomLimits.Maximum, "Programmatic targets must obey min/max");
                renderer.ZoomViewport(request, Vector2.Zero, false);
                Check(renderer.Scale >= renderer.ZoomLimits.Minimum && renderer.Scale <= 4, "Wheel/cluster zoom must obey limits");
            }
            var saved = new RememberedMapView { MapId = 999, Scale = 100, FrameExtent = 100 };
            Check(saved.ResolveScale(config, renderer.ViewportSize, new(2048)) <= 4, "Saved views must be revalidated against native density");
            renderer.BeginQuestSpotlight(new(50), 999, true, 60071);
            driver.Step(new(.2));
            Check(renderer.Scale >= renderer.ZoomLimits.Minimum && renderer.Scale <= 4, "Spotlight must obey small-room bounds");
        }
        foreach (var frame in new[] { new Vector2(540, 1030), new Vector2(1160, 620) }) {
            var limits = ViewportMath.Limits(new MapDrawingConfig(), frame, new(128));
            Check(limits.Small && limits.Minimum <= MathF.Min(frame.X, frame.Y) / 128 && limits.Maximum == 4, "Tiny textures must contain, never cover past native cap");
        }
        foreach (var zoom in new[] { .001f, .6f, 1, 4, 100, float.NaN })
            Check(ViewportMath.SymbolScale(zoom) is >= .6f and <= 1.5f, "Arrow and marker zoom multipliers must stay bounded");
        var old = new SystemConfig(); old.FitSmallRoomsToContent = false;
        SettingsLayout.Reset(old, "Map"); Check(old.FitSmallRoomsToContent, "Map settings reset must restore the new default");
        Console.WriteLine("Small instances: synthetic texture, contain/content fit, tall/wide bounds, all zoom targets, saved-view density and symbol caps passed.");
    }
}

internal static partial class Program
{
    private static bool TrySmallInstanceScene(string[] args)
    {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || args[at + 1] is not ("small-instance" or "small-instance-wide")) return false;
        var wide = args[at + 1] == "small-instance-wide";
        var outputAt = Array.IndexOf(args, "--out");
        var output = outputAt >= 0 ? args[outputAt + 1] : $"_preview/{args[at + 1]}.png";
        using var driver = new ScriptedFrameDriver(SmallInstanceVerification.Config(wide), SmallInstanceVerification.Scene(wide));
        driver.Step(new(0)); driver.Step(new(.1)); driver.Save(output);
        Console.WriteLine(driver.Renderer.ViewportDiagnostics());
        return true;
    }
}
