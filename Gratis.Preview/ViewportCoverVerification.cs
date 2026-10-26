using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class ViewportCoverVerification {
    private static readonly Vector2[] Sizes = [new(780, 700), new(1280, 360), new(1928, 335), new(2400, 300),
        new(3000, 300), new(960, 480), new(700), new(480, 960), new(360, 1280), new(300, 1800),
        new(300, 3000), new(780, 700)];
    private static readonly Vector2[] ExtremeSizes = [new(1928, 335), new(2400, 300), new(3000, 300),
        new(300, 1800), new(300, 3000)];

    private static SystemConfig Config(bool circle, bool scaling) => new() {
        AcceptedSpoilerWarning = true, FadeMode = 0, WindowPosition = new(24), WindowSize = Sizes[0],
        MapScale = .05f, ClampToMap = true, ScaleWithFrame = scaling, MapCircle = circle,
        FollowPlayer = false, RememberMapView = false, AutoHideChrome = false,
    };

    private static FrameScript Script(bool autoFit, float factor = 1) => new() {
        Width = 3120, Height = 3120, AutoFit = autoFit, MapSizeFactor = factor
    };
    private static readonly MapContentBounds CompactArtwork = new(new(574), new(1474));
    private static readonly MapContentBounds CityArtwork = new(new(749), new(1299));

    private static bool Covers(FrameSnapshot frame, Vector2 texture) {
        var max = frame.DrawPosition + texture * frame.Scale;
        return frame.DrawPosition.X <= .01f && frame.DrawPosition.Y <= .01f
            && max.X >= frame.Viewport.X - .01f && max.Y >= frame.Viewport.Y - .01f;
    }

    public static void Run() {
        var failures = new List<string>();
        var checks = 0;
        foreach (var map in new[] { (Factor: 2f, Content: (MapContentBounds?)CityArtwork),
            (Factor: 1f, Content: (MapContentBounds?)CompactArtwork), (Factor: 1f, Content: (MapContentBounds?)null) })
            foreach (var circle in new[] { false, true })
                foreach (var scaling in new[] { false, true })
                    foreach (var autoFit in new[] { false, true })
                        foreach (var reverse in new[] { false, true }) {
                            using var driver = new ScriptedFrameDriver(Config(circle, scaling), Script(autoFit, map.Factor));
                            var time = 0d;
                            var texture = new Vector2(2048);
                            var frame = driver.Step(new(time, ContentReady: false));
                            void Check(string phase) {
                                checks++;
                                if (!Covers(frame, texture))
                                    failures.Add($"{phase} circle={circle} scaling={scaling} autoFit={autoFit} "
                                        + $"reverse={reverse} factor={map.Factor}: {frame.Viewport} rect={frame.DrawPosition}.."
                                        + $"{frame.DrawPosition + texture * frame.Scale}; "
                                        + driver.Renderer.ViewportDiagnostics());
                                var minimum = MathF.Max(frame.Viewport.X / texture.X, frame.Viewport.Y / texture.Y);
                                if (driver.Renderer.ZoomLimits.Small
                                    || MathF.Abs(driver.Renderer.ZoomLimits.Minimum - minimum) > .0001f)
                                    failures.Add($"Normal map minimum must cover the viewport: {minimum}.");
                            }
                            foreach (var size in reverse ? Sizes.Reverse() : Sizes) {
                                frame = driver.Step(new(time += .025, Size: size, Redraws: 2,
                                    ContentReady: true, Content: map.Content));
                                Check("resize");
                                for (var i = 0; i < 40; i++)
                                    frame = driver.Step(new(time += .025));
                                Check("settled");
                            }
                            foreach (var size in ExtremeSizes) {
                                frame = driver.Step(new(time += .025, Size: size));
                                var mouse = frame.WindowPosition + frame.Viewport / 2;
                                for (var i = 0; i < 80; i++)
                                    frame = driver.Step(new(time += .025, Mouse: mouse, Wheel: i < 3 ? -100 : 0));
                                Check("wheel minimum");
                                if (MathF.Abs(frame.Scale - driver.Renderer.ZoomLimits.Minimum) > .0001f)
                                    failures.Add("Wheel zoom did not reach its minimum.");
                                foreach (var delta in new[] { new Vector2(10000, -10000), new Vector2(-20000, 20000) }) {
                                    driver.Renderer.PanViewport(delta);
                                    frame = driver.Step(new(time += .025));
                                    Check("pan boundary");
                                }
                            }
                        }
        CheckSavedViews(failures);
        CheckScaleCaps(failures);
        if (failures.Count > 0)
            throw new InvalidOperationException($"Viewport cover: {failures.Count} failures / {checks} checks. "
                + string.Join(Environment.NewLine, failures.Take(4)));
        Console.WriteLine($"Viewport cover: {checks} resize/settle/wheel/pan checks; normal 2048px map, "
            + "up to 10:1 / 1:10, city/sparse/full artwork, both resize directions, frame scaling, circular, "
            + "automatic/manual/saved views and coverage above zoom caps.");
    }

    private static void CheckSavedViews(List<string> failures) {
        foreach (var scaling in new[] { false, true })
            foreach (var savedScale in new[] { .05f, 4, 90 }) {
                var config = Config(false, scaling);
                config.RememberMapView = config.ZoomLocked = true;
                config.MapViews = [new RememberedMapView { MapId = 1, Manual = true, Scale = savedScale,
                    FrameExtent = 700, TextureSize = new(2048), Offset = new(10000, -10000) }];
                using var driver = new ScriptedFrameDriver(config, Script(true, 2));
                var time = 0d;
                driver.Step(new(time, Content: CityArtwork));
                foreach (var size in ExtremeSizes) {
                    var frame = driver.Step(new(time += .025, Size: size));
                    for (var i = 0; i < 40; i++)
                        frame = driver.Step(new(time += .025));
                    if (!Covers(frame, new(2048)))
                        failures.Add($"Saved view {savedScale}, scaling={scaling}, size={size} lost coverage.");
                }
            }
    }

    private static void CheckScaleCaps(List<string> failures) {
        foreach (var scaling in new[] { false, true })
            foreach (var frame in new[] { new Vector2(12000, 335), new Vector2(335, 48000) }) {
                var config = Config(false, scaling);
                var texture = new Vector2(2048);
                var minimum = MathF.Max(frame.X / texture.X, frame.Y / texture.Y);
                var limits = ViewportMath.Limits(config, frame, texture);
                var saved = new RememberedMapView { Scale = .05f, FrameExtent = 700 };
                var zoom = new ViewportZoom();
                var scale = zoom.Resolve(frame, .05f, scaling, minimum);
                scale = ViewportMath.Constrain(config, frame, texture, scale, Vector2.Zero).Scale;
                if (limits.Minimum < minimum || limits.Maximum < minimum || scale < minimum
                    || saved.ResolveScale(config, frame, texture) < minimum)
                    failures.Add($"Cover {minimum} must win over native, saved-view and user zoom caps: {limits}.");
            }
    }

    internal static void Scene(bool tall, bool circle, string output, bool extreme = false) {
        var script = Script(true, extreme ? 2 : 1);
        if (extreme) {
            script.Width = 2040;
            script.Height = 780;
        }
        using var driver = new ScriptedFrameDriver(Config(circle, true), script);
        var time = 0d;
        driver.Step(new(time, Content: extreme ? CityArtwork : CompactArtwork));
        for (var i = 0; i < 40; i++)
            driver.Step(new(time += .025));
        driver.Step(new(time += .025, Size: extreme ? new(1928, 335) : tall ? new(360, 1280) : new(1280, 360)));
        for (var i = 0; i < 40; i++)
            driver.Step(new(time += .025));
        driver.Save(output);
        Console.WriteLine(driver.Renderer.ViewportDiagnostics());
    }
}

internal static partial class Program {
    private static bool TryViewportCoverScene(string[] args) {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length
            || args[at + 1] is not ("viewport-cover-wide" or "viewport-cover-tall" or "viewport-cover-extreme"))
            return false;
        var outputAt = Array.IndexOf(args, "--out");
        ViewportCoverVerification.Scene(args[at + 1] == "viewport-cover-tall", args.Contains("--circle"),
            outputAt >= 0 ? args[outputAt + 1] : $"_preview/{args[at + 1]}.png",
            args[at + 1] == "viewport-cover-extreme");
        return true;
    }
}