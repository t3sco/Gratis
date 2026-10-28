using System.Numerics;
using System.Text.Json;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;

namespace Gratis.Preview;

internal static class ChromeThreeVerification {
    private static void Check(bool value, string message) {
        if (!value)
            throw new InvalidOperationException(message);
    }

    private static bool Valid(Vector4 b) => b.Z > b.X && b.W > b.Y;
    public static void State() {
        var old = JsonSerializer.Deserialize<SystemConfig>(
            "{\"LocationAnchor\":\"BottomRight\",\"CoordinateAnchor\":\"BottomRight\",\"FilterBarAnchor\":\"BottomLeft\"}",
            ScriptedFrameDriver.Json)!;
        Check(old.LocationAnchor == ChromeAnchor.BottomCenter && old.CoordinateAnchor == ChromeAnchor.BottomCenter
            && old.LocationCardShows == LocationCardContent.ZoneAndArea
            && old.CoordinatesShow == CoordinateContent.Both,
            "Unmarked bottom defaults migrate, old filter config loads");
        foreach (var path in new[] { "LocationAnchor", "CoordinateAnchor" }) {
            var picker = SettingsLayout.Entries.Single(e => e.Path == path);
            picker.Set(old, ChromeAnchor.BottomRight);
            var saved = JsonSerializer.Deserialize<SystemConfig>(JsonSerializer.Serialize(old,
                ScriptedFrameDriver.Json),
                ScriptedFrameDriver.Json)!;
            Check((ChromeAnchor)picker.Value(saved) == ChromeAnchor.BottomRight,
                "Explicit bottom corner survives reload");
            picker.Reset(old, new SystemConfig());
            Check((ChromeAnchor)picker.Value(old) == (ChromeAnchor)picker.Value(new SystemConfig()),
                "Anchor reset restores the default");
        }
        foreach (var path in new[] { "LocationCardShows", "CoordinatesShow" }) {
            var entry = SettingsLayout.Entries.Single(e => e.Path == path);
            var defaults = new SystemConfig();
            entry.Set(old, path == "LocationCardShows" ? (object)LocationCardContent.AreaOnly
                : CoordinateContent.Cursor);
            entry.Reset(old, defaults);
            Check(entry.Value(old).Equals(entry.Value(defaults)), "Content selectors reset");
        }
        Check(!SettingsLayout.Entries.Any(e => e.Path is "FilterBarAnchor" or "QuickFilterBar" or "Filters.Mode")
            && !TweakCatalog.All.Any(t => t.ConfigPath == "QuickFilterBar"),
            "Retired corner filter controls leave settings and Tweaks");
        var place = new MapLocation("La Noscea", "Limsa Lominsa Lower Decks", "Hawkers' Alley");
        Check(place.Lines(LocationCardContent.AreaOnly) == ("", "Hawkers' Alley")
            && place.Lines(LocationCardContent.Zone) == ("", "Limsa Lominsa Lower Decks")
            && place.Lines(LocationCardContent.ZoneAndArea) == ("Limsa Lominsa Lower Decks", "Hawkers' Alley")
            && place.Lines(LocationCardContent.RegionZoneArea) == ("Limsa Lominsa Lower Decks",
                "Hawkers' Alley") && place.Overline(LocationCardContent.RegionZoneArea) == "La Noscea",
            "Four levels of location detail");
        var min = new Vector2(14);
        var max = new Vector2(766, 586);
        var size = new Vector2(332, 136);
        foreach (var target in new[] { new Vector2(20, 20), new(760, 20), new(20, 580), new(760, 580),
            new(390, 300) }) {
            var b = TravelPopoverMath.Resolve(min, max, size, target, [], 20);
            Check(Valid(b) && b.X >= min.X && b.Y >= min.Y && b.Z <= max.X && b.W <= max.Y,
                "Edge popover flips and clamps inside inset");
            var next = TravelPopoverMath.Resolve(min, max, size, target, [b], 20);
            Check(next == Vector4.Zero || !ChromeLayout.Overlap(next, b), "Popover respects occupied chrome");
        }
        var bottomCard = new Vector4(240, 530, 560, 586);
        var lifted = TravelPopoverMath.Resolve(min, max, size, new(130, 566), [bottomCard], 20);
        Check(Valid(lifted) && !ChromeLayout.Overlap(lifted, bottomCard),
            "Near-bottom offer slides above the centred cards");
        Console.WriteLine("Chrome 3: migration/explicit anchors, selectors/reset/Tweaks, four location "
            + "formats and edge-popover geometry.");
    }

    public static void BottomRow() {
        foreach (var width in new[] { 360, 780, 1280 }) {
            var c = ChromeLayoutVerification.Config(new(width, 600));
            c.LocationAnchor = ChromeAnchor.BottomCenter;
            using var d = new ScriptedFrameDriver(c, new FrameScript {
                Width = 1440, Height = 800,
                Title = "Eorzea - La Noscea - Limsa Lominsa Lower Decks - Hawkers' Alley###GratisMapWindow",
                PlayerCoordinates = new(18.4f, 23.7f)
            });
            d.Step(new(0));
            d.Step(new(.04));
            var area = MapWindowDrawing.TitleBounds;
            var coords = MapWindowDrawing.CoordinateBounds;
            Check(Valid(area) && Valid(coords) && !ChromeLayout.Overlap(area, coords),
                "Location and coordinate cards remain separate");
            if (width >= 780) {
                Check(area.Y + area.W == coords.Y + coords.W && area.Z + GratisTheme.Gap == coords.X,
                    "Mixed-height bottom cards align by their centres with one gap");
                Check(MathF.Abs((area.X + coords.Z) / 2 - (c.WindowPosition.X + width / 2f)) < .1f,
                    "Location and coordinates centre as a group");
            }
            else
                Check(MathF.Abs((coords.X + coords.Z) / 2 - (c.WindowPosition.X + width / 2f)) < .1f,
                    "Narrow fallback keeps the coordinate pair centred");
            Check(Valid(CoordinateCopy.PlayerBounds) && Valid(CoordinateCopy.CursorBounds)
                && !ChromeLayout.Overlap(CoordinateCopy.PlayerBounds, CoordinateCopy.CursorBounds),
                "Both coordinate containers are distinct");
            d.Step(new(.08, Mouse: c.WindowPosition + c.WindowSize / 2));
            d.Step(new(.12));
            Check(MapWindowDrawing.CoordinateBounds == coords && MapWindowDrawing.TitleBounds == area,
                "Cursor appearance never resizes or shifts either card");
            Check(BottomCardsDrawing.LocationPrimary == "Hawkers' Alley"
                && BottomCardsDrawing.LocationSecondary == "Limsa Lominsa Lower Decks",
                "Real title data reaches both location lines");
            c.CoordinatesShow = CoordinateContent.Player;
            d.Step(new(.16));
            Check(Valid(CoordinateCopy.PlayerBounds) && CoordinateCopy.CursorBounds == Vector4.Zero,
                "Player-only mode");
            c.CoordinatesShow = CoordinateContent.Cursor;
            d.Step(new(.20));
            Check(CoordinateCopy.PlayerBounds == Vector4.Zero && Valid(CoordinateCopy.CursorBounds),
                "Cursor-only mode");
        }
        Console.WriteLine("Bottom row: small/normal/large centred group, compact height, stable cursor layout "
            + "and all coordinate modes.");
    }

    public static void TravelInput() {
        var c = ChromeLayoutVerification.Config(new(780, 600));
        c.ClampToMap = false;
        using var d = new ScriptedFrameDriver(c, new FrameScript {
            Width = 1000, Height = 800,
            Markers = [new(60453, TravelFixture.Hub.World!.Value)]
        });
        d.Markers[0].PrimaryText = () => TravelFixture.Hub.Name;
        var ready = true;
        var places = new TravelFixture(d);
        var ui = d.Context.Assistance = new MapAssistance(NullTravelProvider.Instance, places, () => ready, () => { });
        double t = 0;
        FrameSnapshot Step(Vector2? p = null, bool? left = null, bool moving = false, bool escape = false,
            double dt = .04) =>
            d.Step(new(t += dt, Mouse: p, Left: left, Moving: moving, Escape: escape));
        Step();
        Step();
        foreach (var blocked in new[] { "moving", "game condition", "display-only marker" }) {
            ready = blocked != "game condition";
            d.Markers[0].IconFamily = blocked == "display-only marker" ? MarkerIconFamily.Neutral
                : MarkerIconFamily.Aetheryte;
            var point = d.Renderer.WorldToScreen(TravelFixture.Hub.World.Value, d.Window.MapDrawOffset);
            var before = d.Renderer.DrawOffset;
            Step(point, moving: blocked == "moving");
            Step(left: true, moving: blocked == "moving");
            var drag = Step(point + new Vector2(50, 30), moving: blocked == "moving");
            Step(left: false, moving: blocked == "moving");
            Check(drag.Dragging && Vector2.Distance(before, d.Renderer.DrawOffset) > 1 && ui.CardMax == Vector2.Zero
                && ui.Chip == null,
                blocked + " must pan and never open travel on a drag");
        }
        d.Markers[0].IconFamily = MarkerIconFamily.Aetheryte;
        ready = true;
        var icon = d.Renderer.WorldToScreen(TravelFixture.Hub.World.Value, d.Window.MapDrawOffset);
        Step(icon);
        Step(left: true);
        Check(ui.CardMax == Vector2.Zero, "Press waits for release before opening travel");
        Step(left: false);
        Step();
        Check(ui.CardMax.X > ui.CardMin.X && places.Teleports == 0,
            "Stationary click opens confirmation without teleporting");
        Check(Vector2.Distance(Vector2.Clamp(icon, ui.CardMin, ui.CardMax), icon) <= 30,
            "Popover remains beside clicked icon");
        Step(escape: true);
        Step();
        Check(ui.CardMax == Vector2.Zero, "Escape dismisses travel popover");
        Step(icon);
        Step(left: true);
        Step(left: false);
        Step();
        Step(c.WindowPosition + new Vector2(30, 160));
        Step(left: true);
        Step(left: false);
        Check(ui.CardMax == Vector2.Zero && places.Teleports == 0, "Outside click dismisses without teleporting");
        Step(icon);
        Step(left: true);
        Step(left: false);
        Step();
        Step(new(-1000), dt: 9);
        Check(ui.CardMax == Vector2.Zero && places.Teleports == 0, "Idle popover expires without acting");
        Step(icon);
        Step(left: true);
        Step(left: false);
        Step();
        var previous = ui.CardMin + new Vector2(30, 30);
        ready = false;
        var beforeCardDrag = d.Renderer.DrawOffset;
        Step(previous);
        Step(left: true);
        Step(previous + new Vector2(-40, 30));
        Step(left: false);
        Check(ui.CardMax == Vector2.Zero && Vector2.Distance(beforeCardDrag, d.Renderer.DrawOffset) > 1,
            "Unavailable travel card releases its previous hit area to map drag");
        ready = true;
        icon = d.Renderer.WorldToScreen(TravelFixture.Hub.World.Value, d.Window.MapDrawOffset);
        Step(icon);
        Step(left: true);
        Step(left: false);
        Step();
        Step(ui.TravelButtonCenter);
        Step(left: true);
        Step(left: false);
        Check(places.Teleports == 1, "Explicit Teleport button alone executes travel");
        places.RequireConfirmation = true;
        Step(icon);
        Step(left: true);
        Step(left: false);
        Step();
        Step(ui.TravelButtonCenter);
        Step(left: true);
        Step(left: false);
        Check(places.Teleports == 1 && ui.Message.StartsWith("Confirm "),
            "Native gil confirmation does not execute on first click");
        Step(ui.TravelButtonCenter);
        Step(left: true);
        Step(left: false);
        Check(places.Teleports == 1, "A double-click cannot skip the confirm step");
        Step(dt: .5);
        Step(ui.TravelButtonCenter);
        Step(left: true);
        Step(left: false);
        Check(places.Teleports == 2, "Second explicit click confirms native gil cost without Lifestream");
        Console.WriteLine("Travel input: moving/game-blocked/inert marker drags pan, stationary click "
            + "opens near icon, Esc and stale-card fallthrough, explicit teleport.");
    }
}