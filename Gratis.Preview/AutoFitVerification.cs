using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class AutoFitVerification
{
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("AutoFit: " + message); }
    private static SystemConfig Config(Vector2 size) => new() {
        AcceptedSpoilerWarning = true, FadeMode = 0, WindowPosition = new(24), WindowSize = size,
        MapScale = .05f, ReduceMotion = true, RememberMapView = true,
    };
    private static IEnumerable<int[]> Orders(int[] values)
    {
        if (values.Length == 0) { yield return []; yield break; }
        foreach (var value in values) foreach (var tail in Orders(values.Where(x => x != value).ToArray())) yield return [value, ..tail];
    }
    private static float Expected(Vector2 frame, Vector2 texture, MapContentBounds bounds, float dpi, bool small)
    {
        if (!small) return MathF.Min(4, MathF.Max(frame.X / texture.X, frame.Y / texture.Y));
        var x = frame.X - 2 * MathF.Min(24 * dpi, frame.X * .05f);
        var y = frame.Y - 2 * MathF.Min(48 * dpi, frame.Y * .08f);
        var minimum = MathF.Min(4, MathF.Min((frame.X - 32) / texture.X, (frame.Y - 32) / texture.Y));
        return Math.Clamp(MathF.Min(x / bounds.Size.X, y / bounds.Size.Y), minimum, 4);
    }
    public static void Run()
    {
        (string Name, Vector2 Window, Vector2 Texture, bool Small)[] cases = [
            ("tiny", new(346, 336), new(128), true), ("raid", new(346, 336), new(2048), true),
            ("tall", new(540, 1030), new(512), true), ("wide", new(1160, 620), new(512), true),
            ("normal", new(640, 480), new(2048), false),
        ];
        var count = 0;
        foreach (var test in cases) {
            var config = Config(new(240));
            var bounds = test.Small ? new MapContentBounds(test.Texture * .25f, test.Texture * .75f) : new MapContentBounds(Vector2.Zero, test.Texture);
            var script = new FrameScript { Width = 1280, Height = 1120, AutoFit = true, InstanceMap = test.Small,
                MapOffset = new Vector2(1024) - test.Texture / 2 };
            using var d = new ScriptedFrameDriver(config, script);
            var now = 0d;
            foreach (var order in Orders([0, 1, 2, 3])) {
                var map = (uint)(1000 + count++);
                config.MapViews = [new RememberedMapView { MapId = map, Scale = .05f, FrameExtent = 4000, Offset = new(500) }];
                d.Step(new(now += .05, MapId: map + 10000, Size: new(240), TextureReady: false, ContentReady: false));
                foreach (var item in order) {
                    var frame = item switch {
                        0 => new ScriptFrame(now += .05, TerritoryId: (uint)(count + 1)),
                        1 => new ScriptFrame(now += .05, MapId: map),
                        2 => new ScriptFrame(now += .05, TextureReady: true, TextureSize: test.Texture),
                        _ => new ScriptFrame(now += .05, Size: test.Window),
                    };
                    d.Step(frame);
                }
                d.Step(new(now += .05, ContentReady: true, Content: bounds, Dpi: 1.25f, Redraws: 2));
                d.Step(new(now += .05));
                var renderer = d.Renderer;
                var expected = Expected(renderer.ViewportSize, test.Texture, bounds, 1.25f, test.Small);
                Check(renderer.AutoFit.Mode == ViewportViewMode.AutoFit && renderer.ViewportReady && MathF.Abs(renderer.Scale - expected) < .0002f,
                    $"{test.Name}/{string.Join(',', order)} final fit {renderer.Scale} != {expected}; {renderer.ViewportDiagnostics()}");
                Check(renderer.ViewMemory.Find(config, map)?.Manual != true, "An automatic view must never become manual memory");
                d.Step(new(now += .05, Size: test.Window * .9f, Dpi: 1.5f));
                expected = Expected(renderer.ViewportSize, test.Texture, bounds, 1.5f, test.Small);
                Check(MathF.Abs(renderer.Scale - expected) < .0002f && renderer.AutoFit.LastFitAt == now, "Preset resize/DPI must refit without changing map id");
            }
        }
        using (var d = new ScriptedFrameDriver(Config(new(346, 336)), new FrameScript { AutoFit = true, InstanceMap = true, SmallInstance = true, MapId = 999 })) {
            d.Step(new(0, ViewportSize: Vector2.One, TextureReady: false));
            Check(!d.Renderer.ViewportReady && double.IsNaN(d.Renderer.AutoFit.LastFitAt), "Invalid first viewport must defer without fitting/capturing");
            d.Step(new(.1, ViewportSize: new(346, 336), TextureReady: false));
            Check(d.Renderer.AutoFit.Readiness == "waiting-texture" && d.Renderer.TextureSize == Vector2.Zero, "No previous/default texture dimensions may leak into a new map");
            d.Renderer.ZoomViewport(4, Vector2.Zero); d.Renderer.PanViewport(new(20));
            Check(d.Renderer.AutoFit.Mode == ViewportViewMode.AutoFit, "Gestures on an unready transform must not freeze a stale view");
            d.Step(new(.2, TextureReady: true));
            Check(d.Renderer.AutoFit.Mode == ViewportViewMode.AutoFit && d.Renderer.ViewportReady, "Late resources must complete fit");
            d.Renderer.SetViewportScale(1.3f); d.Step(new(.3));
            Check(d.Renderer.AutoFit.Mode == ViewportViewMode.Manual && d.Renderer.ViewMemory.Find(d.Config, 999)?.Manual == true, "Explicit zoom must own and persist a manual view");
            d.Step(new(.4, TerritoryId: 1));
            Check(d.Renderer.AutoFit.Mode == ViewportViewMode.AutoFit, "Previous-instance manual memory must not override a new session's fit");
            d.Config.ZoomLocked = true; d.Step(new(.5));
            Check(d.Renderer.AutoFit.Mode == ViewportViewMode.Manual && d.Renderer.AutoFit.ManualReason == "viewport locked", "Explicit lock leaves AutoFit");
            Check(d.Renderer.ViewportDiagnostics().Contains("lastFitReason=") && d.Renderer.ViewportDiagnostics().Contains("lastFitAt="), "Motion/report viewport diagnostic must expose fit ownership and time");
        }
        var manualConfig = Config(new(346, 336));
        manualConfig.MapViews = [new RememberedMapView { MapId = 999, Manual = true, Scale = 1, FrameExtent = 336,
            TextureSize = new(512), MapKey = (1UL << 32) | 999, Session = 0 }];
        using (var d = new ScriptedFrameDriver(manualConfig, new FrameScript { AutoFit = true, InstanceMap = true, MapId = 999 })) {
            d.Step(new(0, TextureSize: new(512)));
            Check(d.Renderer.AutoFit.Mode == ViewportViewMode.Manual, "Verified manual memory may restore");
            d.Step(new(.1, TextureSize: new(2048)));
            Check(d.Renderer.AutoFit.Mode == ViewportViewMode.AutoFit, "Late texture metadata must invalidate a restore for a different texture");
        }
        using (var d = new ScriptedFrameDriver(Config(new(346, 336)), new FrameScript { AutoFit = true, InstanceMap = true })) {
            d.Step(new(0)); d.Renderer.SetViewportScale(1.3f); d.Step(new(.1));
            d.Step(new(.2, MapId: 999, TextureReady: false));
            d.Renderer.BeginQuestSpotlight(Vector2.Zero, 999, true);
            Check(d.Renderer.ViewMemory.Find(d.Config, 999)?.Manual != true, "A pre-texture spotlight must not capture the preceding map as manual memory");
            d.Step(new(.3, TextureReady: true));
            Check(d.Renderer.AutoFit.Mode == ViewportViewMode.AutoFit, "Pre-load spotlight must preserve automatic ownership");
        }
        Console.WriteLine($"AutoFit: {count} teleport orderings across tiny/raid/tall/wide/normal maps, late content/DPI/presets, first-frame guards, manual memory, same-map sessions and locks passed.");
    }
    internal static void Scene(string output)
    {
        var config = Config(new(220));
        using var d = new ScriptedFrameDriver(config, new FrameScript { AutoFit = true, InstanceMap = true, SmallInstance = true,
            Width = 460, Height = 450, MapId = 1, MapSizeFactor = 2, Title = "Eyes of the Creator - Life Support" });
        d.Step(new(0, TextureReady: false, ContentReady: false, ViewportSize: Vector2.One));
        d.Step(new(.05, TerritoryId: 1122)); d.Step(new(.1, MapId: 999));
        d.Step(new(.15, TextureReady: true, ViewportSize: new(220)));
        d.Step(new(.2, Size: new(346, 336), ViewportSize: new(346, 336)));
        d.Step(new(.3, ContentReady: true)); d.Step(new(.4));
        d.Save(output); Console.WriteLine(d.Renderer.ViewportDiagnostics());
    }
}

internal static partial class Program
{
    private static bool TryAutoFitScene(string[] args)
    {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || args[at + 1] != "small-raid-first-teleport") return false;
        var outputAt = Array.IndexOf(args, "--out");
        AutoFitVerification.Scene(outputAt >= 0 ? args[outputAt + 1] : "_preview/small-raid-first-teleport.png");
        return true;
    }
}
