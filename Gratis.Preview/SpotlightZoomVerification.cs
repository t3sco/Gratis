using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class SpotlightZoomVerification {
    public static void Run() {
        var config = new SystemConfig {
            AcceptedSpoilerWarning = true, FadeMode = 0, WindowPosition = new(24), WindowSize = new(540, 1030),
            MapScale = .05f, ReduceMotion = false, RememberMapView = true, FollowPlayer = false,
        };
        var content = new MapContentBounds(new(380, 60), new(640, 980));
        var script = new FrameScript { Width = 700, Height = 1100, AutoFit = true, InstanceMap = false,
            MapOffset = new Vector2(1024) - new Vector2(1024) / 2 };
        using var driver = new ScriptedFrameDriver(config, script);
        double time = 0;
        driver.Step(new(time, MapId: 3000, Size: new(540, 1030), TextureReady: true, TextureSize: new(1024),
            ContentReady: true, Content: content, Dpi: 1, Redraws: 2));
        for (var i = 0; i < 60; i++)
            driver.Step(new(time += .016));
        var target = new Vector2(0, -400);
        if (!driver.Renderer.BeginSpotlight(new(target, 3000, SpotlightSource.Quest, IncludePlayer: false)))
            throw new InvalidOperationException("Spotlight zoom: the spotlight did not start.");
        for (var i = 0; i < 100; i++)
            driver.Step(new(time += .016));
        Vector2 Screen() => driver.Renderer.WorldToScreen(target, driver.Window.MapDrawOffset);
        var before = Screen();
        var scaleBefore = driver.Renderer.Scale;
        for (var i = 0; i < 60; i++)
            driver.Step(new(time += .016, Mouse: new Vector2(270, 515), Wheel: i == 2 ? -1 : 0));
        var after = Screen();
        if (driver.Renderer.Scale >= scaleBefore)
            throw new InvalidOperationException("Spotlight zoom: the wheel did not zoom out.");
        if (Vector2.Distance(before, after) > 150)
            throw new InvalidOperationException(
                $"Spotlight zoom: the target jumped from {before} to {after} instead of staying put.");
        Console.WriteLine("Spotlight zoom: wheel zoom-out keeps the focused marker in place instead of restoring the old view.");
    }
}
