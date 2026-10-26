using System.Numerics;
using System.Text.Json;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;

namespace Gratis.Preview;

internal static class ChromeLayoutVerification {
    private static void Require(bool value, string message) {
        if (!value)
            throw new InvalidOperationException(message);
    }

    private static bool Valid(Vector4 b) => b.Z > b.X && b.W > b.Y;
    internal static SystemConfig Config(Vector2 size) => new() {
        AcceptedSpoilerWarning = true, FadeMode = 0, PinChrome = true, ReduceMotion = true,
        FollowPlayer = false, WindowPosition = new(32), WindowSize = size, TravelActions = true,
    };
    internal static void SetAnchors(SystemConfig c, ChromeAnchor a) =>
        c.ToolbarAnchor = c.FilterBarAnchor = c.LocationAnchor = c.CoordinateAnchor = c.TravelCardAnchor = a;

    public static void Layout() {
        ChromePolishVerification.Run();
        FeedbackVerification.Run();
        SpotlightZoomVerification.Run();
        ToolbarMigration();
        ToolbarPlacement();
        var c = JsonSerializer.Deserialize<SystemConfig>("{}", new JsonSerializerOptions { IncludeFields = true })!;
        Require(c.ChromeScale == 1 && c.RestoreOpacityOnHover && c.ToolbarAnchor == ChromeAnchor.TopCenter
            && c.FilterBarAnchor == ChromeAnchor.BottomLeft && c.LocationAnchor == ChromeAnchor.TopLeft
            && c.CoordinateAnchor == ChromeAnchor.BottomCenter && c.TravelCardAnchor == ChromeAnchor.TopCenter,
            "Old configs acquire layout defaults");
        SetAnchors(c, ChromeAnchor.TopRight);
        c.ChromeScale = 1.4f;
        c.RestoreOpacityOnHover = false;
        var restored = JsonSerializer.Deserialize<SystemConfig>(JsonSerializer.Serialize(c, ScriptedFrameDriver.Json),
            ScriptedFrameDriver.Json)!;
        Require(restored.TravelCardAnchor == ChromeAnchor.TopRight && restored.ChromeScale == 1.4f
            && !restored.RestoreOpacityOnHover,
            "Layout settings round trip");
        SettingsLayout.Reset(c, "Chrome & motion");
        Require(c.ToolbarAnchor == ChromeAnchor.TopCenter && c.LocationAnchor == ChromeAnchor.TopLeft
            && c.ChromeScale == 1 && c.RestoreOpacityOnHover,
            "Section reset restores layout and hover settings");
        foreach (var a in Enum.GetValues<ChromeAnchor>()) {
            var min = new Vector2(14);
            var max = new Vector2(626, 466);
            var size = new Vector2(100, 36);
            var first = ChromeLayout.Resolve(min, max, size, a, [], 8);
            var second = ChromeLayout.Resolve(min, max, size, a, [first], 8);
            Require(Valid(first) && Valid(second) && !ChromeLayout.Overlap(first, second),
                "Every anchor stacks collisions inward");
            Require((a >= ChromeAnchor.BottomLeft ? first.W == max.Y && second.W == first.Y - 8
                : first.Y == min.Y && second.Y == first.W + 8),
                "Anchor baseline and stack gap");
            Require(((int)a % 3) switch {
                0 => first.X == min.X,
                1 => (first.X + first.Z) / 2 == (min.X + max.X) / 2,
                _ => first.Z == max.X
            },
                "Anchor horizontal alignment");
        }
        Require(ChromeLayout.Resolve(Vector2.Zero, new(64), new(100, 36), ChromeAnchor.BottomRight, [],
            8) == Vector4.Zero,
            "Impossible layouts omit rather than overflow");
        using var driver = new ScriptedFrameDriver(Config(new(780, 600)),
            new FrameScript { Width = 1440, Height = 960 });
        var travel = driver.Context.Assistance = new MapAssistance(NullTravelProvider.Instance,
            new TravelFixture(driver), () => true, () => {
            });
        double time = 0;
        foreach (var scale in new[] { 1f, 1.5f })
            foreach (var circle in new[] { false, true })
                foreach (var size in new[] { new Vector2(320, 240), new Vector2(780, 600), new Vector2(1280, 800) })
                    foreach (var anchor in Enum.GetValues<ChromeAnchor>()) {
                        c = driver.Config;
                        c.ChromeScale = scale;
                        c.MapCircle = circle;
                        SetAnchors(c, anchor);
                        travel.OfferContextTarget(new(new(400, 0), 1, SpotlightSource.Quest), time);
                        driver.Step(new(time += .04, Size: size));
                        driver.Step(new(time += .04));
                        var surfaces = new[] { MapToolbarDrawing.LastBounds, MapWindowDrawing.TitleBounds,
                MapWindowDrawing.CoordinateBounds, FilterBarDrawing.LastBounds, travel.ChipBounds };
                        var origin = c.WindowPosition;
                        var center = origin + size / 2;
                        foreach (var bounds in surfaces.Where(Valid)) {
                            foreach (var point in new[] {
                    new Vector2(bounds.X, bounds.Y),
                    new Vector2(bounds.Z, bounds.Y),
                    new Vector2(bounds.X, bounds.W),
                    new Vector2(bounds.Z, bounds.W) }) {
                                Require(point.X >= origin.X + 14 * scale - .1f && point.Y >= origin.Y + 14 * scale - .1f
                                    && point.X <= origin.X + size.X - 14 * scale + .1f
                                    && point.Y <= origin.Y + size.Y - 14 * scale + .1f,
                                    $"Chrome inset {size}/{anchor}/{scale}");
                                Require(!circle || Vector2.Distance(point, center) < MathF.Min(size.X, size.Y) / 2,
                                    "Circular chrome stays inside the rim");
                            }
                        }
                        for (var i = 0; i < surfaces.Length; i++)
                            for (var j = i + 1; j < surfaces.Length; j++)
                                Require(!ChromeLayout.Overlap(surfaces[i], surfaces[j]),
                                    $"No overlapping chrome at {size}/{anchor}/{scale}/{circle}: {i}/{j}");
                    }
        c = driver.Config;
        c.MapCircle = false;
        c.ResetChromeLayout();
        driver.Step(new(time += .04, Size: new(1280, 800)));
        var location = MapWindowDrawing.TitleBounds;
        var coords = MapWindowDrawing.CoordinateBounds;
        var filters = FilterBarDrawing.LastBounds;
        Require(Valid(location) && Valid(coords) && !Valid(filters) && location.Y == coords.Y && location.W == coords.W
            && location.Z + GratisTheme.Gap == coords.X,
            "Default [location][coords] cluster has no standalone Filters neighbour");
        c.CompactTitle = c.StyledCoordinates = false;
        c.LocationAnchor = c.CoordinateAnchor = ChromeAnchor.TopRight;
        driver.Step(new(time += .04));
        Require(Valid(MapWindowDrawing.TitleBounds) && Valid(MapWindowDrawing.CoordinateBounds)
            && !ChromeLayout.Overlap(MapWindowDrawing.TitleBounds, MapWindowDrawing.CoordinateBounds)
            && MapWindowDrawing.CoordinateBounds.Y < c.WindowPosition.Y + c.WindowSize.Y / 2,
            "Alternate title/coordinate styles honour anchors too");
        Console.WriteLine("Chrome layout: legacy/default/reset settings; six anchors; 144 live layouts "
            + "across sizes, scales and shapes; inset, collision and bottom-row alignment.");
    }

    private static void ToolbarMigration() {
        foreach (var json in new[] { "{}", "{\"ToolbarAnchor\":0}", "{\"ToolbarAnchor\":\"TopLeft\"}",
            "{\"ToolbarAnchor\":\"TopLeft\",\"ToolbarAnchorExplicit\":false}" }) {
            var old = JsonSerializer.Deserialize<SystemConfig>(json, ScriptedFrameDriver.Json)!;
            Require(old.ToolbarAnchor == ChromeAnchor.TopCenter && !old.ToolbarAnchorExplicit,
                "Missing and unmarked old Top-left anchors migrate to Top-centre");
        }
        var c = JsonSerializer.Deserialize<SystemConfig>("{\"ToolbarAnchor\":\"BottomRight\"}",
            ScriptedFrameDriver.Json)!;
        Require(c.ToolbarAnchor == ChromeAnchor.BottomRight, "Other legacy anchors are preserved");
        var picker = SettingsLayout.Entries.Single(e => e.Path == "ToolbarAnchor");
        picker.Set(c, ChromeAnchor.TopLeft);
        var saved = JsonSerializer.Deserialize<SystemConfig>(JsonSerializer.Serialize(c, ScriptedFrameDriver.Json),
            ScriptedFrameDriver.Json)!;
        Require(saved.ToolbarAnchorExplicit && saved.ToolbarAnchor == ChromeAnchor.TopLeft,
            "Explicit Top-left picker choice persists across reload");
        picker.Reset(c, new SystemConfig());
        Require(c.ToolbarAnchor == ChromeAnchor.TopCenter && !c.ToolbarAnchorExplicit,
            "Picker reset restores new default and clears explicit choice");
        picker.Set(c, ChromeAnchor.TopRight);
        c.ResetChromeLayout();
        Require(c.ToolbarAnchor == ChromeAnchor.TopCenter && !c.ToolbarAnchorExplicit,
            "Layout reset clears explicit choice");
        picker.Set(c, ChromeAnchor.TopLeft);
        SettingsLayout.Reset(c, "Chrome & motion");
        Require(c.ToolbarAnchor == ChromeAnchor.TopCenter && !c.ToolbarAnchorExplicit,
            "Section reset clears explicit choice");
        foreach (var path in new[] { "ToolbarAnchor", "LocationAnchor", "CoordinateAnchor", "TravelCardAnchor" })
            Require(SettingsLayout.Find(c, "Chrome & motion", "").Any(e => e.Path == path),
                "Active chrome anchor pickers remain in settings");
    }

    private static void ToolbarPlacement() {
        foreach (var dpi in new[] { 1f, 2f }) {
            var c = Config(new Vector2(780, 600) * dpi);
            using var driver = new ScriptedFrameDriver(c,
                new FrameScript { Width = (int)(1440 * dpi), Height = (int)(960 * dpi), Dpi = dpi });
            var travel = driver.Context.Assistance = new MapAssistance(NullTravelProvider.Instance,
                new TravelFixture(driver), () => true, () => {
                });
            double time = 0;
            foreach (var scale in new[] { .8f, 1f, 1.5f })
                foreach (var segmented in new[] { false, true })
                    foreach (var width in new[] { 256f, 280f, 320f, 780f, 1280f }) {
                        c.ChromeScale = scale;
                        c.SegmentedToolbar = segmented;
                        c.LocationAnchor = ChromeAnchor.TopLeft;
                        c.CoordinateAnchor = ChromeAnchor.TopRight;
                        travel.OfferContextTarget(new(new(400, 0), 1, SpotlightSource.Quest), time);
                        driver.Step(new(time += .04, Size: new Vector2(width, 600) * dpi));
                        driver.Step(new(time += .04));
                        var toolbar = MapToolbarDrawing.LastBounds;
                        var handle = driver.Window.MoveHandle.Geometry;
                        var totalScale = dpi * scale;
                        var inset = MathF.Ceiling(GratisTheme.Inset * totalScale
                            + Gratis.Classes.MapShape.Rounding(new Vector2(width, 600) * dpi) * .29289322f);
                        var centeredWidth = (MapToolbarDrawing.CollapsedWidth(MapToolbarDrawing.FullButtonCount)
                            + 2 * (GratisTheme.ToolbarCenterMargin - GratisTheme.Inset)) * totalScale;
                        var centered = width * dpi - 2 * inset >= centeredWidth;
                        Require(Valid(toolbar), $"Toolbar fits at {width}/{scale}/{dpi}");
                        Require(MapToolbarDrawing.EffectiveAnchor == (centered ? ChromeAnchor.TopCenter
                            : ChromeAnchor.TopLeft),
                            "Narrow toolbar falls back to Top-left");
                        Require(toolbar.Y >= handle.HitMax.Y + GratisTheme.Gap * totalScale - .01f
                            && !ChromeLayout.Overlap(toolbar,
                                new(handle.HitMin.X, handle.HitMin.Y, handle.HitMax.X, handle.HitMax.Y)),
                            "Toolbar clears the entire move-handle hit zone with a fixed scaled gap");
                        if (centered) {
                            Require(MathF.Abs((toolbar.X + toolbar.Z) / 2
                                - (c.WindowPosition.X + width * dpi / 2)) < .01f,
                                "Toolbar is horizontally centred");
                            Require(toolbar.X >= c.WindowPosition.X + GratisTheme.ToolbarCenterMargin * totalScale
                                - .01f
                                && toolbar.Z <= c.WindowPosition.X + width * dpi - GratisTheme.ToolbarCenterMargin
                                    * totalScale + .01f,
                                "Centred toolbar has comfortable side margins");
                        }
                        else
                            Require(MathF.Abs(toolbar.X - c.WindowPosition.X - inset) < .01f,
                                "Narrow fallback uses usual left inset");
                        var surfaces = new[] {
                    toolbar,
                    MapWindowDrawing.TitleBounds,
                    MapWindowDrawing.CoordinateBounds,
                    FilterBarDrawing.LastBounds,
                    travel.ChipBounds };
                        for (var i = 0; i < surfaces.Length; i++)
                            for (var j = i + 1; j < surfaces.Length; j++)
                                Require(!ChromeLayout.Overlap(surfaces[i], surfaces[j]),
                                    $"Toolbar and corner/centre chrome do not overlap at {width}/{scale}/{dpi}");
                        if (width >= 780)
                            Require(Valid(travel.ChipBounds) && !ChromeLayout.Overlap(travel.ChipBounds, toolbar),
                                "Icon-anchored travel popover avoids the toolbar");
                    }
            c.ToolbarAnchor = ChromeAnchor.TopLeft;
            c.ToolbarAnchorExplicit = true;
            driver.Step(new(time += .04, Size: new Vector2(1280, 800) * dpi));
            Require(MapToolbarDrawing.EffectiveAnchor == ChromeAnchor.TopLeft, "Explicit Top-left never auto-centres");
        }
        Console.WriteLine("Toolbar placement: migration, explicit choices/reset, 60 live "
            + "width/scale/DPI/style layouts, handle clearance, narrow fallback and centre/corner collisions.");
    }

    public static void HoverFade() {
        var c = Config(new(640, 480));
        c.FadeMode = FadeMode.Always;
        c.FadePercent = .3f;
        c.RestoreOpacityOnHover = false;
        using var driver = new ScriptedFrameDriver(c);
        var center = c.WindowPosition + c.WindowSize / 2;
        driver.Step(new(0));
        driver.Step(new(.02, Mouse: center));
        driver.Step(new(.04));
        Require(MathF.Abs(driver.Context.Chrome.ParentAlpha - .3f) < .001f, "Disabled hover restoration preserves configured fade");
        c.RestoreOpacityOnHover = true;
        driver.Step(new(.06));
        driver.Step(new(.08));
        Require(driver.Context.Chrome.ParentAlpha == 1, "Ordinary fading restores on hover");
        driver.Step(new(.10, Mouse: new(-1000)));
        driver.Step(new(.12));
        Require(MathF.Abs(driver.Context.Chrome.ParentAlpha - .3f) < .001f, "Leaving restores configured fade");
        c.GhostMode = true;
        driver.Step(new(.14, Mouse: center));
        driver.Step(new(.16));
        Require(driver.Window.Ghost.Opacity == 1 && driver.Context.Chrome.ParentAlpha == 1,
            "Ghost retains its existing hover restoration");
    }

    public static void QuestTravel() {
        var c = Config(new(780, 600));
        using var driver = new ScriptedFrameDriver(c,
            new FrameScript { Width = 1000, Height = 800, Markers = [new(71203, new(80, 40), Objective: true)] });
        var source = new TravelFixture(driver);
        var travel = driver.Context.Assistance = new MapAssistance(NullTravelProvider.Instance, source, () => true,
            () => { });
        double time = 0;
        void Step(Vector2? mouse = null, bool? left = null, bool? right = null, bool escape = false) =>
            driver.Step(new(time += .04, Mouse: mouse, Left: left, Right: right, Escape: escape));
        Step();
        Step();
        var objective = driver.Renderer.WorldToScreen(new(80, 40), driver.Window.MapDrawOffset);
        Step(objective);
        Step(left: true);
        Step(left: false);
        Require(travel.Chip?.Destination == TravelFixture.Hub && Valid(travel.ChipBounds) && source.Teleports == 0,
            "Click objective offers transport without teleporting");
        var dismiss = travel.ChipDismissButtonCenter;
        Step(dismiss);
        Step(left: true);
        Step(left: false);
        Step();
        Require(travel.Chip == null && source.Teleports == 0, "Dismiss persists while target remains selected");
        Step(objective);
        Step(right: true);
        Step(right: false);
        var nearest = driver.Context.ContextMenu.Rows["Show nearest aetheryte"];
        Step(nearest, left: true);
        Step(left: false);
        Step();
        Require(travel.Chip != null && source.Teleports == 0, "Objective context menu offers the same card");
        Step(travel.ChipButtonCenter);
        Step(left: true);
        Step(left: false);
        Require(source.Teleports == 1, "Only explicit Teleport invokes native travel without Lifestream");
        c.TravelActions = false;
        Step();
        Require(travel.Chip == null, "TravelActions disables objective card");
        Step(objective);
        Step(left: true);
        Step(left: false);
        Require(travel.Chip == null && source.Teleports == 1, "Disabled objective travel stays silent");
    }
}