using System.Numerics;
using System.Text.Json;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;
using ImGuiNET;

namespace Gratis.Preview;

internal static class InteractionLegibilityVerification
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Inertia()
    {
        var velocity = new Vector2(600, -200);
        var splitVelocity = velocity;
        var distance = MotionMath.Glide(ref velocity, .3f, .35f);
        var split = Vector2.Zero;
        for (var i = 0; i < 18; i++) split += MotionMath.Glide(ref splitVelocity, 1f / 60, .35f);
        Check(Vector2.Distance(distance, split) < .001f && Vector2.Distance(velocity, splitVelocity) < .001f,
            "Glide must preserve travel and velocity across frame rates");
        var old = JsonSerializer.Deserialize<SystemConfig>("{\"Motion\":{\"EaseZoom\":false}}", ScriptedFrameDriver.Json)!;
        Check(old.Motion.PanInertia && old.Motion.PanInertiaStrength == .35f, "Old settings must acquire inertia defaults");
        old.Motion.PanInertia = false; old.Motion.PanInertiaStrength = .9f;
        SettingsLayout.Reset(old, "Chrome & motion");
        Check(old.Motion.PanInertia && old.Motion.PanInertiaStrength == .35f, "Motion reset must restore both inertia controls");
        var precise = new Vector2(86.37732f, 43.18866f);
        Check(ViewportMath.ClampOffset(precise, new(640, 480), new(2048), 1, false) == precise,
            "An interior clamp must preserve exact offset bits, so it cannot falsely kill velocity");

        var config = new SystemConfig { FollowPlayer = true, MapScale = 1, ScaleWithFrame = false,
            FollowReturnTrigger = FollowReturnTrigger.AfterIdle, FollowReturnIdleSeconds = .5f,
            WindowPosition = new(32), WindowSize = new(640, 480), AcceptedSpoilerWarning = true, FadeMode = 0 };
        using var driver = new ScriptedFrameDriver(config);
        driver.Step(new(0)); driver.Step(new(.02, Mouse: new(350, 250)));
        var down = driver.Step(new(.04, Left: true));
        Check(down.Dragging, "Inertia test must begin a real map drag");
        driver.Step(new(.06, Mouse: new(380, 265)));
        var moved = driver.Step(new(.08, Mouse: new(410, 280)));
        Check(moved.Offset.Length() > 10 && driver.Renderer.viewportMotion.PanVelocity.Length() > 0,
            $"Held drag must collect release velocity: offset={moved.Offset} velocity={driver.Renderer.viewportMotion.PanVelocity}");
        driver.Step(new(.10, Left: false));
        var release = driver.Renderer.DrawOffset;
        var glide = driver.Step(new(.12));
        Check(Vector2.Distance(glide.Offset, release) > 1 && driver.Renderer.viewportMotion.PanVelocity.Length() > 0,
            $"Real map drag must glide after release: release={release} next={glide.Offset} velocity={driver.Renderer.viewportMotion.PanVelocity} state={glide.Follow}");
        Check(glide.Follow == FollowState.Detached && config.FollowPlayer, "Flick must stay Detached without disabling follow preference");
        driver.Step(new(.14, Left: true));
        Check(driver.Renderer.viewportMotion.PanVelocity == Vector2.Zero, "New click cancels inertia");
        driver.Step(new(.16, Left: false));
        driver.Renderer.PanViewport(new(20, 10));
        driver.Renderer.ZoomViewport(1.2f, Vector2.Zero);
        Check(driver.Renderer.viewportMotion.PanVelocity == Vector2.Zero, "Wheel cancels inertia");
        driver.Renderer.PanViewport(new(20, 10));
        driver.Renderer.CenterOnCoordinate(Vector2.Zero);
        Check(driver.Renderer.viewportMotion.PanVelocity == Vector2.Zero, "Programmatic centre cancels inertia");
        driver.Renderer.PanViewport(new(20, 10));
        driver.Step(new(.18, Mouse: new(-1000), Focused: false));
        for (var i = 1; i <= 120; i++) driver.Step(new(.18 + i / 60.0));
        Check(driver.Renderer.viewportMotion.Follow.State == FollowState.Following && driver.Renderer.DrawOffset.Length() < .1f,
            "After the glide stops, idle follow-return must finish normally");
        driver.Renderer.PanViewport(new(20, 10));
        driver.Step(new(2.20, Player: new(200, 0)));
        Check(driver.Renderer.viewportMotion.PanVelocity == Vector2.Zero, "Teleport/follow snap cancels inertia");
        config.FollowPlayer = false;
        driver.Renderer.PanViewport(new(10000, 10000));
        var edge = driver.Step(new(2.22));
        var stopped = driver.Step(new(2.24));
        Check(Vector2.Distance(edge.Offset, stopped.Offset) < .001f && driver.Renderer.viewportMotion.PanVelocity == Vector2.Zero,
            "Bounds stop both glide axes without overshoot");
        foreach (var mode in new[] { "off", "reduced", "classic" }) {
            config.Motion.PanInertia = mode != "off"; config.ReduceMotion = mode == "reduced"; config.VisualStyle = mode == "classic" ? 1 : 0;
            driver.Renderer.PanViewport(new(-20, -10));
            Check(driver.Renderer.viewportMotion.PanVelocity == Vector2.Zero, mode + " disables inertia");
        }
        Console.WriteLine("Inertia: exact integral, real drag/release, detachment, click/zoom/centre cancellation, bounds, settings/reset and opt-outs passed.");
    }

    public static void Bounds()
    {
        foreach (var circle in new[] { false, true }) {
            var config = new SystemConfig { FollowPlayer = false, MapScale = .01f, ClampToMap = true, MapCircle = circle,
                AcceptedSpoilerWarning = true, FadeMode = 0, WindowPosition = new(32), WindowSize = new(640, 480) };
            using var driver = new ScriptedFrameDriver(config);
            driver.Step(new(0)); driver.Step(new(.02));
            void Bounded(string path)
            {
                var scale = config.MapScale;
                var frame = driver.Renderer.ViewportSize;
                Check(scale >= ViewportMath.MinimumScale(frame, new(2048), circle), path + " minimum zoom");
                var center = new Vector2(1024) - driver.Renderer.DrawOffset;
                Check(Vector2.Distance(center, ViewportMath.ClampCenter(center, frame, new(2048), scale, circle)) < .001f,
                    path + " map bounds");
            }
            driver.Renderer.CenterOnOffset(new(6000, -6000)); Bounded("quest locate");
            driver.Renderer.SetViewportScale(.001f); Bounded("auto zoom");
            driver.Renderer.CenterOnCoordinate(new(-5000, 5000)); Bounded("flag/search centre");
            driver.Renderer.ZoomViewport(.0001f, new(100, -100), false); Bounded("immediate zoom");
            driver.Renderer.ZoomToCluster(new(-5000), driver.Renderer.Scale); Bounded("cluster target");
            var time = .02;
            foreach (var source in Enum.GetValues<SpotlightSource>()) {
                driver.Renderer.BeginSpotlight(new(new(5000, -5000), driver.MapId, source));
                for (var i = 0; i < 12; i++) { driver.Step(new(time += .02, Redraws: 2)); Bounded(source.ToString()); }
                driver.Renderer.Spotlight.End(time, "test restore");
                for (var i = 0; i < 12; i++) { driver.Step(new(time += .02)); Bounded("restore " + source); }
            }
            driver.Renderer.BeginQuestSpotlight(new(9000), driver.MapId, true, 60071);
            for (var i = 0; i < 15; i++) { driver.Step(new(time += .02)); Bounded("quest link"); }
            driver.Step(new(time += .02, Size: new(900, 650))); Bounded("resize");
        }
        Console.WriteLine("Programmatic bounds: quest locate, auto zoom, flag/search, clusters, every SpotlightSource, restore and resize passed in rectangle/circle.");
    }

    public static void Text()
    {
        using var driver = new ScriptedFrameDriver();
        driver.Step(new(0));
        ImGui.NewFrame();
        ImGui.Begin("Text pixel verification");
        foreach (var role in Enum.GetValues<ChromeFontRole>())
            foreach (var requested in new[] { 8f, 10.5f, 13.8f, 17.5f, 21f, 31.9f, 47f, 80f, 128f }) {
                using var font = driver.Context.GetChromeFont(requested, role)!.Push();
                Check(ImGui.GetFont().FontSize == TextDrawing.PixelSize(requested), "Atlas must contain the exact selected integer size");
            }
        Check(TextDrawing.Snap(new(10.3f, 20.8f)) == new Vector2(10, 21), "Text origin must snap to screen pixels");
        Check(TextDrawing.PixelSize(float.NaN) == 14 && TextDrawing.PixelSize(float.PositiveInfinity) == 14,
            "Invalid saved size must fall back safely");
        ImGui.End();
        ImGui.Render();
        Console.WriteLine("Text: native atlas sizes for all Geist/Mono roles and label sizes, pixel origins and invalid-size fallback passed.");
    }
}
