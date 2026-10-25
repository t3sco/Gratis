using System.Numerics;
using Gratis.Classes;
using Gratis.Data;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class FrameRegressionVerification
{
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static SystemConfig Config() => new() {
        AcceptedSpoilerWarning = true, FadeMode = 0, MapScale = .5f, HideWindowFrame = true,
        WindowPosition = new Vector2(32), WindowSize = new Vector2(640, 480),
    };
    private static void Counts(FrameSnapshot snapshot, int passes = 1)
    {
        Require(snapshot.MotionAdvances == 1, $"t={snapshot.Time}: motion advanced {snapshot.MotionAdvances} times");
        Require(snapshot.TexturePasses == passes, "Requested real texture redraws must execute");
        Require(snapshot.IdCollisions == 0 && snapshot.DuplicateChrome == 0,
            $"t={snapshot.Time}: duplicate ImGui IDs/chrome {snapshot.IdCollisions}/{snapshot.DuplicateChrome}");
    }

    public static void AutoHide()
    {
        using var driver = new ScriptedFrameDriver(Config());
        driver.Step(new(0, SettingsOpen: true));
        var fading = driver.Step(new(1.25));
        Require(fading.ChromeProgress is > 0 and < 1, "Settings-open idle must enter the fade");
        var hidden = driver.Step(new(1.5));
        Require(hidden.ChromeProgress == 0 && hidden.Widgets == 0, "Settings-open idle must hide chrome and remove its widgets");
        driver.Step(new(1.6, Mouse: new Vector2(320, 240)));
        var shown = driver.Step(new(1.62));
        Require(shown.ChromeProgress == 1 && shown.Widgets > 0, "Real map hover reveals chrome");
        driver.Step(new(1.7, Mouse: new Vector2(-1000), SettingsOpen: false));
        var gone = driver.Step(new(3.2, SettingsOpen: true, Focused: false));
        Require(gone.ChromeProgress == 0 && gone.Widgets == 0, "Leaving the map hides chrome again despite settings/focus");
        Counts(gone);
    }

    private static List<FrameSnapshot> ZoomTimeline(int passes)
    {
        var config = Config(); config.FollowPlayer = false;
        using var driver = new ScriptedFrameDriver(config);
        var snapshots = new List<FrameSnapshot>();
        var time = 0.0;
        for (var index = 0; index < 100; index++) {
            time += index == 15 ? .3 : index == 9 ? 0 : 1.0 / 60;
            var snapshot = driver.Step(new(time, Mouse: new Vector2(350, 240),
                Wheel: index is >= 3 and <= 6 ? 1 : 0, Redraws: passes));
            Counts(snapshot, passes);
            if (index == 3) Require(driver.Renderer.viewportMotion.PendingZoom.HasValue, "Wheel must reach MapWindow zoom input");
            if (index == 9) Require(snapshot.Scale == snapshots[^1].Scale, "Zero elapsed must preserve the spring, never snap");
            if (index == 15) Require(Math.Abs(driver.Renderer.viewportMotion.LastStep - .3f) < .00001, "300ms hitch integrates the spring");
            snapshots.Add(snapshot);
        }
        Require(snapshots[4].Scale > .5f && snapshots[4].Scale < snapshots[^1].Scale, "Wheel burst must show intermediate zoom");
        Require(snapshots[7].Scale < snapshots[^1].Scale - .01f, "Zoom must not snap after a burst");
        Require(Math.Abs(snapshots[^1].Scale - .5f * MathF.Pow(1.25f, 4)) < .001f, "Wheel impulses accumulate and settle");
        return snapshots;
    }
    public static void Zoom()
    {
        var once = ZoomTimeline(1); var twice = ZoomTimeline(2);
        for (var index = 0; index < once.Count; index++)
            Require(once[index].Scale == twice[index].Scale && once[index].Offset == twice[index].Offset,
                $"Zoom differs with a second texture redraw at frame {index}");
    }

    private static List<FrameSnapshot> FollowTimeline(int passes)
    {
        var config = Config(); config.FollowReturnToPlayer = false;
        using var driver = new ScriptedFrameDriver(config);
        driver.Step(new(0, Redraws: passes));
        driver.Step(new(.1));
        var snapshots = new List<FrameSnapshot>();
        foreach (var time in new[] { .12, .12, .14, .44, .46, .7, 1.0, 1.5 }) {
            var snapshot = driver.Step(new(time, Player: new Vector2(10, 0)));
            Counts(snapshot, passes); snapshots.Add(snapshot);
        }
        Require(snapshots[0].Offset.X is < 0 and > -10, "Follow must ease moving player through intermediate offsets");
        Require(snapshots[0].Offset == snapshots[1].Offset, "Zero elapsed follow must not snap");
        Require(Math.Abs(snapshots[^1].Offset.X + 10) < .01f, "Follow must settle at player");
        return snapshots;
    }
    public static void Follow()
    {
        var once = FollowTimeline(1); var twice = FollowTimeline(2);
        for (var index = 0; index < once.Count; index++)
            Require(once[index].Offset == twice[index].Offset, $"Follow differs with a second texture redraw at {index}");
    }

    public static void PanReturn()
    {
        var config = Config();
        config.Motion.PanInertia = false;
        config.FollowReturnToPlayer = true; config.FollowReturnTrigger = FollowReturnTrigger.AfterIdle;
        config.FollowReturnIdleSeconds = .5f;
        using var driver = new ScriptedFrameDriver(config);
        driver.Step(new(0, Mouse: new Vector2(300, 250)));
        driver.Step(new(.02)); driver.Step(new(.04));
        var down = driver.Step(new(.06, Left: true));
        Require(down.Dragging, "Mouse-down must start pan through MapWindow");
        var pan = driver.Step(new(.08, Mouse: new Vector2(380, 250)));
        Require(pan.Offset.X > 100 && pan.Follow == FollowState.Detached && config.FollowPlayer, "Pan detaches following without turning it off");
        driver.Step(new(.1, Left: false));
        driver.Step(new(.12, Mouse: new Vector2(-1000), Focused: false));
        var held = driver.Step(new(.4));
        Require(held.Follow == FollowState.Detached, "Return waits for idle threshold");
        driver.Step(new(.8));
        var returning = driver.Step(new(.9));
        Require(returning.Follow == FollowState.Returning && returning.Offset.X > 0 && returning.Offset.X < pan.Offset.X,
            "Pan must ease back after release/idle");
        var settled = driver.Step(new(3));
        Require(settled.Follow == FollowState.Following && settled.Offset.Length() < .05f, "Return settles at player");
        var teleport = driver.Step(new(3.02, Player: new Vector2(100, 0)));
        Require(teleport.Offset == new Vector2(-100, 0), "Teleport reattaches immediately");
        var resized = driver.Step(new(3.04, Size: new Vector2(700, 400), MapId: 2));
        Require(resized.Viewport == new Vector2(700, 400) && !resized.Dragging, "Resize/map change updates the real viewport");
        Counts(resized);
    }

    public static void Glass()
    {
        foreach (var segmented in new[] { true, false }) {
            var config = Config(); config.PinChrome = true; config.SegmentedToolbar = segmented;
            config.BlurBackground = config.GlassEnabled = config.GlassBlur = true;
            using var driver = new ScriptedFrameDriver(config);
            driver.Step(new(0));
            var snapshot = driver.Step(new(.02));
            Require(snapshot.ToolbarBlur == "blurred" && snapshot.TitleBlur == "blurred" && snapshot.CoordinatesBlur == "blurred",
                $"All chrome must use blur ({segmented}): {snapshot.ToolbarBlur}/{snapshot.TitleBlur}/{snapshot.CoordinatesBlur}");
            Require(snapshot.Blurred >= 4 && snapshot.Blurred == driver.Context.Blurs.Count && snapshot.Refused == 0, driver.Context.Blur.Status(config));
            Require(driver.Context.Blurs[1].List == driver.MapDrawList,
                "Toolbar blur must use the full map-list surface, outside the child clip");
            if (segmented) Require(driver.Context.Blurs[1].Max - driver.Context.Blurs[1].Min == new Vector2(MapToolbarDrawing.CollapsedWidth(MapToolbarDrawing.FullButtonCount), GratisTheme.ToolbarHeight), "Blur covers the full toolbar rim");
            Require(snapshot.Widgets == MapToolbarDrawing.FullButtonCount, "Each toolbar action is submitted exactly once");
            Counts(snapshot);
        }
    }

    public static void Markers()
    {
        foreach (var declutter in new[] { true, false }) {
            var config = Config(); config.Markers.Declutter = declutter; config.PinChrome = true;
            config.Markers.DimIrrelevant = true;
            var scene = new FrameScript();
            uint[] quests = [71021, 71025, 71203, 71151, 71022, 71031];
            for (var index = 0; index < quests.Length; index++) {
                var position = new Vector2(-200 + index * 65, 30);
                scene.Markers.Add(new(quests[index], position, Relevant: false));
                scene.Markers.Add(new(61000, position + Vector2.One, Relevant: false));
            }
            using var driver = new ScriptedFrameDriver(config, scene);
            driver.Step(new(0)); var snapshot = driver.Step(new(.02));
            Require(snapshot.QuestsDrawn == quests.Length, $"Declutter={declutter}: rendered {snapshot.QuestsDrawn}/{quests.Length} quests");
            Require(snapshot.UnknownObjectives == 0, "Unmapped marker must not render as Objective");
            Require(driver.Trace.DrawnMarkers.Any(marker => marker.IconId == 61000), "Unknown marker must reach real marker drawing");
            foreach (var marker in driver.Markers.Where(marker => marker.IconId == 61000))
                Require(FlatMarkerDrawing.Category(marker) == MarkerCategory.Poi && !marker.IsObjective, "Unmapped marker stays neutral");
            Counts(snapshot);
        }
    }

    public static void Allocations()
    {
        var config = Config(); config.PinChrome = true;
        using var driver = new ScriptedFrameDriver(config);
        var input = new ScriptFrame(0);
        for (var index = 0; index < 120; index++) driver.Step(input with { Time = index / 60.0 });
        var samples = new long[600];
        var retainedBefore = GC.GetTotalMemory(true);
        for (var index = 0; index < samples.Length; index++) {
            input = input with { Time = 2 + index / 60.0 };
            var before = GC.GetAllocatedBytesForCurrentThread();
            var snapshot = driver.Step(input);
            samples[index] = GC.GetAllocatedBytesForCurrentThread() - before;
            Counts(snapshot);
        }
        var retained = GC.GetTotalMemory(true) - retainedBefore;
        var early = samples.Take(100).Average(); var late = samples.TakeLast(100).Average();
        Require(late <= early * 1.05 + 256, $"Per-frame allocations grew: {early:F0} -> {late:F0} bytes");
        Require(retained < 16 * 1024, $"600 stationary frames retained {retained} bytes");
        Console.WriteLine($"600 frames: allocated/frame {early:F0} -> {late:F0} B; retained delta {retained} B (existing drawing allocations are bounded, not zero).");
    }

    public static void Instrumentation()
    {
        var config = Config(); config.PinChrome = true;
        using var driver = new ScriptedFrameDriver(config) { RepeatToolbarForVerification = true };
        driver.Step(new(0));
        var duplicated = driver.Step(new(.02));
        Require(duplicated.IdCollisions == MapToolbarDrawing.FullButtonCount && duplicated.DuplicateChrome > 0,
            "Repeated actual toolbar submissions must report real ImGui ID collisions and chrome duplication");
        driver.RepeatToolbarForVerification = false;
        var normal = driver.Step(new(.04));
        Counts(normal);

    }
}
