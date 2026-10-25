using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class MoveHandleVerification {
    private static void Require(bool value, string message) {
        if (!value)
            throw new InvalidOperationException(message);
    }

    internal static SystemConfig Config() => new() {
        AcceptedSpoilerWarning = true, FadeMode = 0, FollowPlayer = false, MapScale = .5f,
        WindowPosition = new(32), WindowSize = new(640, 480), HideWindowFrame = true,
        ChromeIdleDelay = 1.2f, ChromeFadeDurationMs = 180,
    };
    public static void Maths() {
        var config = new SystemConfig();
        Require(config.MoveHandle && config.AltDragMovesWindow && config.MoveHandleSnap && !config.ClassicMoveHandle,
            "Missing saved settings must default on in Gratis, with Classic opt-in");
        foreach (var scale in new[] { 1f, 1.5f, 2f }) {
            var screen = new MoveWorkArea(new(-1920, -300), new(0, 780));
            var size = new Vector2(640, 480);
            var handle = MoveHandleMath.Geometry(Vector2.Zero, size, false, scale);
            var clamped = MoveHandleMath.Clamp(new(-9000, -9000), size, screen, handle, scale);
            Require(handle.HitMin.X + clamped.X >= screen.Min.X && handle.HitMin.Y + clamped.Y >= screen.Min.Y,
                "Clamp must retain the grab target above/left of an offset monitor");
            clamped = MoveHandleMath.Clamp(new(9000), size, screen, handle, scale);
            Require(handle.HitMax.X + clamped.X <= screen.Max.X && handle.HitMax.Y + clamped.Y <= screen.Max.Y,
                "Clamp must retain the grab target below/right");
            Require(clamped.X <= screen.Max.X - 48 * scale && clamped.Y <= screen.Max.Y - 48 * scale,
                "At least 48 scaled pixels of the window must remain visible");
            var near = screen.Min + new Vector2(8 * scale);
            Require(MoveHandleMath.SnapDrag(near + new Vector2(11 * scale), size, screen, scale) == near,
                "Shift edge snap has a 12 px threshold");
            var far = screen.Max - size - new Vector2(8 * scale);
            Require(MoveHandleMath.SnapDrag(far - new Vector2(11 * scale), size, screen, scale) == far,
                "Bottom/right edge snap preserves the margin");
            var grid = MoveHandleMath.SnapDrag(screen.Min + new Vector2(103 * scale, 91 * scale), size, screen, scale)
                - screen.Min;
            Require(grid == new Vector2(104, 88) * scale, "Shift grid is relative to this monitor's origin");
            Require(MoveHandleMath.NearestEdge(screen.Min + new Vector2(32), size, screen, scale) == near,
                "Nearest corner snap must align both edges");
            var circle = MoveHandleMath.Geometry(new(80, 0), new(560, 480), true, scale);
            Require(Vector2.Distance(circle.Center, new(320, 240)) < 240 && circle.Contains(circle.Center),
                "Circular handle must sit inside the tangent rim and its hit target");
        }
    }

    private static FrameSnapshot Warm(ScriptedFrameDriver driver) {
        driver.Step(new(0));
        driver.Step(new(.02));
        driver.Step(new(2));
        driver.Step(new(2.02));
        return driver.Step(new(2.2));
    }

    public static void Frames() {
        foreach (var circle in new[] { false, true })
            foreach (var scale in new[] { 1f, 1.5f })
                foreach (var origin in new[] { Vector2.Zero, new Vector2(1920, 900) }) {
                    var config = Config();
                    config.MapCircle = circle;
                    config.WindowPosition += origin;
                    using var driver = new ScriptedFrameDriver(config,
                        new FrameScript { Dpi = scale, ScreenOrigin = origin });
                    var idle = Warm(driver);
                    Require(idle.ChromeProgress == 0 && idle.HandleVisibility == 0,
                        "Handle and toolbar must hide at rest");
                    var handle = idle.HandleCenter;
                    driver.Step(new(2.22, Mouse: handle));
                    var hover = driver.Step(new(2.28));
                    Require(hover.HandleVisibility is > 0 and < 1 && hover.ChromeProgress == 0,
                        $"Handle fade/reveal: circle={circle}, scale={scale}, origin={origin}, "
                            + $"visibility={hover.HandleVisibility}, chrome={hover.ChromeProgress}, "
                            + $"center={hover.HandleCenter}");
                    var down = driver.Step(new(2.36, Left: true));
                    Require(down.MovingWindow && !down.Dragging,
                        "A handle press owns the gesture and never starts pan");
                    var delta = new Vector2(45, 27);
                    var moved = driver.Step(new(2.38, Mouse: handle + delta));
                    Require(moved.WindowPosition == idle.WindowPosition + delta,
                        "Window must move by the exact screen-space mouse delta at every DPI/monitor");
                    Require(driver.Window.MapDrawOffset == moved.WindowPosition,
                        "Child map position must update in the same rendered frame");
                    Require(moved.Viewport == idle.Viewport,
                        "Moving must retain the full viewport in the same frame, without stale Begin/content bounds");
                    Require(moved.Offset == idle.Offset && !moved.Dragging && moved.ChromeProgress == 0,
                        "Moving must not pan or reveal the toolbar");
                    var stalled = driver.Step(new(2.68));
                    Require(stalled.WindowPosition == moved.WindowPosition,
                        "Frame time must not accumulate position drift");
                    driver.Step(new(2.7, Left: false));
                    Require(driver.PositionSaves == 1, "Save movement once on release");
                    var diagnostics = driver.Context.Chrome.TakeDiagnostics(config, driver.Now);
                    Require(diagnostics.Contains("window-move=") && diagnostics.Contains("hover=0")
                        && diagnostics.Contains("drag=0"),
                        "Chrome diagnostics must distinguish window movement from map interaction");
                }
        AltDrag();
        SnapRestore();
        LockedAndClassic();
        NormalPan();
    }

    private static void AltDrag() {
        using var driver = new ScriptedFrameDriver(Config());
        var idle = Warm(driver);
        var mouse = new Vector2(400, 240);
        var contextCalls = driver.ContextMenuCalls;
        driver.Step(new(2.22, Mouse: mouse, Alt: true));
        driver.Step(new(2.24, Left: true, Right: true));
        var moved = driver.Step(new(2.26, Mouse: mouse + new Vector2(70, 40)));
        Require(moved.WindowPosition == idle.WindowPosition + new Vector2(70, 40) && moved.MovingWindow,
            "Alt+left drag must move anywhere on the map");
        Require(moved.Offset == idle.Offset && !moved.Dragging && moved.ChromeProgress == 0,
            "Alt move must preserve map offset and hidden toolbar");
        Require(driver.ContextMenuCalls == contextCalls, "Alt must suppress context-menu and other map input");
        var released = driver.Step(new(2.28, Alt: false, Mouse: mouse + new Vector2(140, 80)));
        Require(!released.MovingWindow && released.WindowPosition == moved.WindowPosition,
            "Releasing Alt mid-drag must end movement immediately");
        var continued = driver.Step(new(2.3, Mouse: mouse + new Vector2(160, 90)));
        Require(!continued.Dragging && continued.Offset == idle.Offset && continued.ChromeProgress == 0,
            "A cancelled move may not transfer its held press into pan or toolbar reveal");
        driver.Step(new(2.32, Left: false, Right: false));
        Require(driver.PositionSaves == 1, "Alt release must persist its final position");
    }

    private static void SnapRestore() {
        using var driver = new ScriptedFrameDriver(Config());
        var idle = Warm(driver);
        var handle = idle.HandleCenter;
        driver.Step(new(2.22, Mouse: handle));
        driver.Step(new(2.24, Left: true));
        driver.Step(new(2.26, Left: false));
        var snapped = driver.Step(new(2.36, Left: true));
        Require(snapped.WindowPosition == new Vector2(8) && snapped.WindowSize == idle.WindowSize,
            "Double-click must snap to nearest screen corner with an 8 px margin");
        driver.Step(new(2.38, Left: false));
        var resized = driver.Step(new(3, Size: new Vector2(600, 420), Mouse: new Vector2(-1000)));
        driver.Step(new(3.02, Mouse: resized.HandleCenter));
        driver.Step(new(3.04, Left: true));
        driver.Step(new(3.06, Left: false));
        var restored = driver.Step(new(3.16, Left: true));
        Require(restored.WindowPosition == idle.WindowPosition && restored.WindowSize == idle.WindowSize,
            "The next double-click must restore both saved position and size");
        driver.Step(new(3.18, Left: false));
        var ready = driver.Step(new(4, Mouse: driver.Window.MoveHandle.Geometry.Center));
        driver.Step(new(4.02, Left: true));
        var aligned = driver.Step(new(4.04, Shift: true, Mouse: ready.HandleCenter + new Vector2(71, 59)));
        Require(aligned.WindowPosition == new Vector2(104, 88),
            "Shift drag must align window position to the 8 px grid");
        var clamped = driver.Step(new(4.06, Shift: false, Mouse: new Vector2(-9000)));
        Require(driver.Window.MoveHandle.Geometry.HitMin.X >= 0 && driver.Window.MoveHandle.Geometry.HitMin.Y >= 0
            && !clamped.Dragging,
            "Even an extreme drag must leave the handle reachable");
        driver.Step(new(4.08, Left: false));
    }

    private static void LockedAndClassic() {
        foreach (var classic in new[] { false, true }) {
            var config = Config();
            config.VisualStyle = classic ? 1 : 0;
            config.LockWindow = !classic;
            using var driver = new ScriptedFrameDriver(config);
            var idle = Warm(driver);
            driver.Step(new(2.22, Mouse: idle.HandleCenter, Alt: true));
            driver.Step(new(2.24, Left: true));
            var moved = driver.Step(new(2.26, Mouse: idle.HandleCenter + new Vector2(100)));
            Require(moved.WindowPosition == idle.WindowPosition && !moved.MovingWindow,
                "Lock position and default Classic must disable custom window movement");
            if (classic)
                Require(moved.HandleVisibility == 0, "Classic must draw no handle without explicit opt-in");
        }
        var optedIn = Config();
        optedIn.VisualStyle = 1;
        optedIn.ClassicMoveHandle = true;
        using var enabled = new ScriptedFrameDriver(optedIn);
        Require(Warm(enabled).HandleVisibility == 1, "Classic can explicitly enable the handle");
    }

    private static void NormalPan() {
        using var driver = new ScriptedFrameDriver(Config());
        Warm(driver);
        driver.Step(new(2.22, Mouse: new Vector2(400, 240)));
        driver.Step(new(2.24, Left: true));
        var pan = driver.Step(new(2.26, Mouse: new Vector2(450, 260)));
        Require(pan.Dragging && pan.Offset != Vector2.Zero && pan.WindowPosition == new Vector2(32),
            "Unmodified map dragging must continue to pan");
    }
}