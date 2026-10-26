using System.Globalization;
using System.Numerics;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class CoordinateVerification {
    public static void Run() {
        static void Near(Vector2 actual, Vector2 expected, string label) {
            if (Vector2.Distance(actual, expected) > .002f)
                throw new InvalidOperationException(label + $": {actual} != {expected}");
        }
        var cases = new (Vector2 World, Vector2 Offset, float Factor, Vector2 Expected)[] {
            (new(0), new(0), 1, new(21.5f)),
            (new(-1024), new(0), 1, new(1)),
            (new(1024), new(0), 1, new(42)),
            (new(0), new(0), 2, new(11.25f)),
            (new(-512,512), new(0), 2, new(1,21.5f)),
            (new(-100,200), new(100,-200), 1, new(21.5f)),
            (new(924,-824), new(100,-200), 1, new(42,1)),
            (new(0), new(0), .5f, new(42)),
            (new(0), new(0), 4, new(6.125f)),
        };
        foreach (var c in cases) {
            Near(CoordinateShare.WorldToMap(c.World, c.Offset, c.Factor), c.Expected, "known scale/offset sample");
            Near(CoordinateShare.MapToWorld(c.Expected, c.Offset, c.Factor), c.World, "known sample inverse");
        }
        var random = new Random(8401);
        foreach (var factor in new[] { .5f, .75f, 1, 1.25f, 2, 4 })
            for (var i = 0; i < 1000; i++) {
                var world = new Vector2(random.Next(-2000, 2000), random.Next(-2000, 2000));
                var offset = new Vector2(random.Next(-500, 500), random.Next(-500, 500));
                Near(CoordinateShare.MapToWorld(CoordinateShare.WorldToMap(world, offset, factor), offset, factor),
                    world, "world-map-world round trip");
            }
        var culture = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            if (CoordinateShare.Text(new(21.3f, 14.8f)) != "21.3, 14.8"
                || CoordinateShare.Text(new(21.3f, 14.8f), "La Noscea") != "La Noscea · 21.3, 14.8")
                throw new InvalidOperationException("invariant coordinate copy");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        Console.WriteLine("Coordinates: 9 analytic scale/offset fixtures, inverses, 6,000 round trips and "
            + "invariant copy formats.");
    }

    public static void Frames() {
        Layout();
        var config = PaletteVerification.Config();
        config.WindowSize = new(780, 600);
        using var driver = new ScriptedFrameDriver(config,
            new FrameScript { Width = 960, Height = 800, PlayerCoordinates = new(21.3f, 14.8f), CursorCoordinates = new(9.1f, 99.9f) });
        driver.Context.ContextMenu.Actions = new ContextMenuFixture();
        for (var i = 0; i < 5; i++)
            driver.Step(new(i * .02));
        var b = MapWindowDrawing.CoordinateBounds;
        var point = new Vector2(b.X + 40, (b.Y + b.W) / 2);
        driver.Step(new(.12, Mouse: point));
        if (CoordinateCopy.PlayerBounds != new Vector4(b.X, b.Y, CoordinateCopy.PlayerBounds.Z, b.W))
            throw new InvalidOperationException("Player container stays fixed on hover");
        driver.Step(new(.14, Left: true));
        driver.Step(new(.16, Left: false));
        if (driver.Context.ContextMenu.Toast != "Copied 21.3, 14.8" || driver.Window.Dragging)
            throw new InvalidOperationException("coordinate click copies exact displayed value without pan");
        driver.Step(new(.18, Shift: true, Left: true));
        driver.Step(new(.20, Left: false));
        if (driver.Context.ContextMenu.Toast != "Copied Middle La Noscea · 21.3, 14.8")
            throw new InvalidOperationException("Shift-click includes map name");
        b = CoordinateCopy.CursorBounds;
        point = new((b.X + b.Z) / 2, (b.Y + b.W) / 2);
        driver.Step(new(.22, Mouse: point, Shift: false));
        driver.Step(new(.24, Left: true));
        driver.Step(new(.26, Left: false));
        if (driver.Context.ContextMenu.Toast != "Copied 9.1, 99.9" || driver.Window.Dragging)
            throw new InvalidOperationException("Right container copies cursor coordinates without pan");
        config.ShowEorzeaTime = true;
        driver.Step(new(.28));
        if (CoordinateCopy.CursorBounds.Z >= MapWindowDrawing.CoordinateBounds.Z)
            throw new InvalidOperationException("ET suffix must be outside coordinate copy targets");
        Console.WriteLine("Coordinate bar: actual scripted click/Shift-click, toast, pan isolation and ET bounds.");
    }
    private static void Layout() {
        foreach (var questLog in new[] { false, true }) {
            var c = ChromeLayoutVerification.Config(new(780, 600));
            c.LocationAnchor = ChromeAnchor.TopLeft;
            c.QuestLogOpenWithMap = questLog;
            using var driver = new ScriptedFrameDriver(c, new FrameScript {
                Width = 1440, Height = 960, QuestLog = questLog,
                PlayerCoordinates = new(18.7f, 25.6f), CursorCoordinates = new(9.1f, 99.9f)
            });
            double time = 0;
            foreach (var circle in new[] { false, true })
                foreach (var width in new[] { 256, 320, 360, 780, 1280 })
                    foreach (var mode in Enum.GetValues<CoordinateContent>()) {
                        c.MapCircle = circle;
                        c.CoordinatesShow = mode;
                        driver.Step(new(time += .04, Size: new(width, 600)));
                        driver.Step(new(time += .04));
                        var union = MapWindowDrawing.CoordinateBounds;
                        var player = CoordinateCopy.PlayerBounds;
                        var cursor = CoordinateCopy.CursorBounds;
                        if (union == Vector4.Zero) {
                            if (player != Vector4.Zero || cursor != Vector4.Zero || !questLog && width >= 780)
                                throw new InvalidOperationException("Impossible coordinate layout clears hit targets");
                            continue;
                        }
                        if ((player != Vector4.Zero) != (mode != CoordinateContent.Cursor)
                            || (cursor != Vector4.Zero) != (mode != CoordinateContent.Player))
                            throw new InvalidOperationException("Content mode selects its containers");
                        if (MathF.Abs(union.W - union.Y - BottomCardsDrawing.CoordinateHeight) > .01f)
                            throw new InvalidOperationException("Coordinate containers use compact height");
                        var first = player == Vector4.Zero ? cursor : player;
                        var last = cursor == Vector4.Zero ? player : cursor;
                        if (union != new Vector4(first.X, first.Y, last.Z, last.W)
                            || ChromeLayout.Overlap(player, cursor)
                            || mode == CoordinateContent.Both && MathF.Abs(cursor.X - player.Z - GratisTheme.Gap) > .01f)
                            throw new InvalidOperationException("Coordinate union and one-gap side-by-side containers");
                        if (!questLog && MathF.Abs((union.X + union.Z) / 2 - (c.WindowPosition.X + width / 2f)) > .1f)
                            throw new InvalidOperationException("BottomCenter centres coordinates as a unit");
                        foreach (var other in new[] { MapWindowDrawing.TitleBounds, MapToolbarDrawing.LastBounds,
                            FilterBarDrawing.LastBounds, driver.Context.QuestLog.ReservesSpace ? driver.Context.QuestLog.Bounds : Vector4.Zero })
                            if (ChromeLayout.Overlap(union, other))
                                throw new InvalidOperationException($"Coordinate pair clears other map chrome: quest={questLog}, circle={circle}, width={width}, mode={mode}, coords={union}, other={other}");
                    }
        }
    }

}