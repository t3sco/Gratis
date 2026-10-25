using System.Numerics;
using Gratis.Controllers;
using Gratis.Data;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class PaletteVerification
{
    private static void Require(bool yes, string message) { if (!yes) throw new InvalidOperationException(message); }
    public static SystemConfig Config() => new() {
        AcceptedSpoilerWarning = true, FadeMode = 0, WindowPosition = new(32), WindowSize = new(700, 600),
        FollowPlayer = false, ClampToMap = false, ScaleWithFrame = false, PaletteHintSeen = true,
        ReduceMotion = true, AutoHideChrome = true, GlassEnabled = true,
    };

    public static void Ranking()
    {
        var recent = new Dictionary<string, long>();
        var context = new PlaceSearchContext(1, 1, Vector2.Zero);
        var queries = new (string Query, string Id)[] {
            ("z:limsa", "z:1"), ("z:lower", "z:1"), ("z:upper", "z:2"), ("z:lm ll dcks", "z:1"),
            ("a:limsa", "a:1"), ("a:LIMSA", "a:1"), ("a:lmsa", "a:1"), ("a:lm lom", "a:1"),
            ("a:summer", "a:2"), ("a:smmrfrd", "a:2"), ("a:farms", "a:2"), ("a:summerford farms", "a:2"),
            ("a:aleport", "a:3"), ("a:alprt", "a:3"), ("a:ale", "a:3"), ("a:port", "a:3"),
            ("a:horizon", "a:4"), ("a:hrzn", "a:4"), ("a:hori", "a:4"), ("a:HORIZON", "a:4"),
            ("a:bronze", "a:5"), ("a:cmp brnz", "a:5"), ("a:lake", "a:5"), ("a:bronze camp", "a:5"),
            ("a:idyll", "a:6"), ("a:idylshr", "a:6"), ("a:イディル", "a:6"), ("a:Idyllshire", "a:6"),
            ("a:cryst", "a:7"), ("a:crstrm", "a:7"), ("a:the crystarium", "a:7"), ("a:CrYsTaRiUm", "a:7"),
            ("a:sharlayan", "a:8"), ("a:old sh", "a:8"), ("a:oldshrl", "a:8"), ("a:northern", "a:8"),
            ("a:tuliy", "a:9"), ("a:tlyll", "a:9"), ("a:hawkers", "a:10"), ("a:shard", "a:10"),
            ("q:pirates", "q:1"), ("q:prb prts", "q:1"), ("m:market", "m:1"), ("m:melodie", "m:2"),
            ("m:MÉLODIE", "m:2"), ("m:moogle", "m:3"), ("a:リムサ", "a:1"), ("  Z: limsa  ", "z:1"),
        };
        foreach (var test in queries) {
            var result = PlaceSearch.Find(PaletteFixture.Places, test.Query, PlaceKind.All, context, recent, _ => new(false));
            Require(result.Length > 0 && result[0].Place.Id == test.Id, $"Ranking '{test.Query}': expected {test.Id}, got {result.FirstOrDefault().Place?.Id}");
        }
        Require(PlaceSearch.Score("aeth", "aetheryte") > PlaceSearch.Score("aeth", "the aetheryte"), "Prefix bonus");
        Require(PlaceSearch.Score("mb", "market board") > PlaceSearch.Score("mb", "a memorable"), "Word-boundary bonus");
        var aeth = PlaceSearch.Find(PaletteFixture.Places, "a:aeth", PlaceKind.All, context, recent, _ => new(false));
        Require(aeth.Length >= 9 && aeth.All(a => a.Place.Kind == PlaceKind.Aetheryte), "aeth aliases and prefix scope");
        var identical = new[] { new PlaceEntry("c", PlaceKind.Zone, "Place", "", 4, 4), new PlaceEntry("a", PlaceKind.Zone, "Place", "", 1, 1), new PlaceEntry("b", PlaceKind.Zone, "Place", "", 2, 2) };
        Require(PlaceSearch.Find(identical, "place", PlaceKind.All, context, recent, _ => new(false))[0].Place.Id == "a", "Proximity boost");
        recent["c"] = 5;
        Require(PlaceSearch.Find(identical, "place", PlaceKind.All, new(0, 0, null), recent, _ => new(false))[0].Place.Id == "c", "Recency boost");
        Require(PlaceSearch.Find(identical.Reverse(), "place", PlaceKind.All, new(0, 0, null), new Dictionary<string, long>(), _ => new(false))[0].Place.Id == "a", "Stable ties");
        Require(PlaceSearch.Find(PaletteFixture.Places, "zzzz", PlaceKind.All, context, recent, _ => new(false)).Length == 0, "No-match state");
        Require(PlaceSearch.Parse("m:market", PlaceKind.Zone).Scope == PlaceKind.Marker, "Prefix overrides chip scope");
        Console.WriteLine($"Palette: {queries.Length} ranking cases, boundaries/prefixes, JP/EN, accents, recency/proximity and stable ties.");
    }

    public static void Index()
    {
        var maps = new[] { new PlaceMapRow(1, 1, "Limsa", "La Noscea"), new PlaceMapRow(2, 2, "Idyllshire", "Dravania", Expansion: 1, ExpansionName: "Heavensward") };
        var aeth = new[] { new PlaceAetheryteRow(1, 1, 1, "Hub", true, 5, new(10)), new PlaceAetheryteRow(2, 0, 1, "Shard", false, 5, new(20)), new PlaceAetheryteRow(3, 2, 2, "Unattuned", true, 0, new(30)), new PlaceAetheryteRow(4, 99, 99, "Invalid", true, 0) };
        var places = PlaceIndexBuilder.Build(maps, aeth).ToArray();
        Require(places.Length == 5, "Fake-sheet counts (2 maps + hub + shard + unattuned)");
        Require(places.Single(p => p.AetheryteId == 2) is { HubId: 1, MapId: 1, IsShard: true }, "Aethernet grouped under hub with map fallback");
        Require(places.Single(p => p.AetheryteId == 3).Expansion == 1, "Expansion grouping preserved");
        Require(places.Single(p => p.AetheryteId == 3).Subtitle.StartsWith("Heavensward > Dravania"), "Expansion/region group labels");
        Require(PlaceIndexBuilder.MarkerWorld(new(1124, 1224), .5f, new(40, -20)) == new Vector2(240, 380), "Texture markers convert to world coordinates with factor and offsets");
        var all = PlaceSearch.Find(places, "a:", PlaceKind.All, new(1, 1, null), new Dictionary<string, long>(), p => new(p.AetheryteId == 1));
        Require(all.Length == 3 && all.Any(p => p.Place.AetheryteId == 3 && !p.Access.Attuned), "Unattuned included and flagged");
        Require(PlaceSearch.Find(places, "a:", PlaceKind.All, new(1, 1, null), new Dictionary<string, long>(), p => new(p.AetheryteId == 1), false).Length == 1, "Unattuned filter");
        using var index = new PlaceIndex(); using var done = new ManualResetEventSlim();
        var caller = Environment.CurrentManagedThreadId; var builder = caller;
        index.Start(_ => { builder = Environment.CurrentManagedThreadId; return places; }, (_, _) => done.Set());
        Require(done.Wait(TimeSpan.FromSeconds(5)), "Worker completes");
        Require(builder != caller && index.Entries.Length == 5 && index.Revision > 0, "Worker build publishes cached immutable snapshot");
        Console.WriteLine("Palette: fake-sheet counts, missing map, aethernet grouping, expansion, unattuned flags and worker publication.");
    }

    public static void Frames()
    {
        using (var driver = new ScriptedFrameDriver(Config(), new FrameScript { Palette = true })) {
            driver.Step(new(0)); driver.Step(new(.02)); driver.Step(new(2));
            driver.Step(new(2.1, Control: true, Keys: [ImGuiKey.K]));
            var overlay = driver.Palette!;
            Require(overlay.IsOpen, "Ctrl+K opens when map focused");
            driver.Step(new(2.12, Control: false)); driver.Step(new(2.14, Text: "limsa"));
            Require(overlay.Query == "limsa" && overlay.Results.Count > 0, "Native text input filters");
            driver.Step(new(2.16, Keys: [ImGuiKey.DownArrow]));
            Require(overlay.Selected == 1, "Arrow navigation");
            var target = overlay.Results[overlay.Selected].Place;
            driver.Step(new(2.18)); driver.Step(new(2.2, Keys: [ImGuiKey.Enter]));
            Require(!overlay.IsOpen && driver.PlaceSource!.Shown == target, "Enter selects and closes");
            driver.Step(new(2.22)); driver.Step(new(2.24, Control: true, Keys: [ImGuiKey.K])); driver.Step(new(2.26, Control: false));
            var before = driver.Snapshot(); var point = (overlay.PanelMin + overlay.PanelMax) / 2;
            driver.Step(new(2.28, Mouse: point, Left: true, Wheel: 1));
            var during = driver.Step(new(2.3, Mouse: point + new Vector2(30, 40)));
            Require(!during.Dragging && before.Offset == during.Offset && before.Scale == during.Scale, "Palette mouse/wheel never pan or zoom the map");
            Require(ImGui.GetIO().WantCaptureKeyboard && ImGui.GetIO().WantCaptureMouse, "Native ImGui capture flags while open");
            Require(during.ChromeProgress == 0, "Palette hover/text input does not reveal map chrome");
            driver.Step(new(2.32, Left: false, Escape: true)); Require(!overlay.IsOpen, "Esc closes palette");
            driver.Step(new(2.325, Escape: true)); driver.Step(new(2.33, Escape: true));
            Require(overlay.CapturesInput, "Held Escape remains consumed until release, protecting the underlying map");
            driver.Step(new(2.34, Mouse: new(-1000))); driver.Step(new(2.36, Control: true, Keys: [ImGuiKey.K]));
            Require(overlay.IsOpen, "Esc restores map focus for shortcut");
            driver.Step(new(2.38, Control: false, Escape: true)); driver.Step(new(2.4));
            driver.Config.PinChrome = true;
            driver.Step(new(2.41));
            var toolbar = MapToolbarDrawing.LastBounds;
            var search = new Vector2(toolbar.X + 4 + GratisTheme.ControlHeight / 2, toolbar.Y + GratisTheme.ToolbarHeight / 2);
            driver.Step(new(2.42, Mouse: search)); driver.Step(new(2.44, Left: true)); driver.Step(new(2.46, Left: false));
            Require(overlay.IsOpen, "Toolbar Search routes to the palette");
            driver.Config.PaletteTeleportButton = false; var teleports = driver.PlaceSource!.Teleports;
            driver.Step(new(2.48, Alt: true, Keys: [ImGuiKey.Enter]));
            Require(driver.PlaceSource.Teleports == teleports, "Display-only option blocks Alt+Enter teleport");
            driver.Step(new(3, Alt: false, Mouse: new(-1000)));
            overlay.Open("a:"); driver.Step(new(3.02)); driver.Step(new(3.04));
            driver.Step(new(3.06, Keys: [ImGuiKey.End])); Require(overlay.Selected == overlay.Results.Count - 1, "End selects last result");
            driver.Step(new(3.08)); driver.Step(new(3.1, Keys: [ImGuiKey.Home])); Require(overlay.Selected == 0, "Home selects first result");
            driver.Step(new(3.12)); driver.Step(new(3.14, Keys: [ImGuiKey.PageDown])); Require(overlay.Selected > 0, "PageDown advances a page");
            driver.Step(new(3.16)); driver.Step(new(3.18, Keys: [ImGuiKey.PageUp])); Require(overlay.Selected == 0, "PageUp returns a page");
            overlay.Open(); driver.Step(new(3.2)); driver.Step(new(3.22, Keys: [ImGuiKey.Tab]));
            Require(overlay.Results.All(r => r.Place.Kind == PlaceKind.Aetheryte), "Tab cycles visible scope chips");
            overlay.Open("a:limsa"); driver.Step(new(3.24)); driver.Step(new(3.26)); driver.Step(new(3.28, Keys: [ImGuiKey.Tab]));
            Require(overlay.Query == "limsa" && overlay.Results.Count > 0 && overlay.Results.All(r => r.Place.Kind == PlaceKind.Zone), "Tab advances a typed prefix into the next visible scope");
            overlay.Open("a:summer"); driver.Config.PaletteTeleportButton = true; driver.Step(new(3.3)); driver.Step(new(3.32));
            driver.Step(new(3.34, Alt: true, Keys: [ImGuiKey.Enter]));
            Require(driver.PlaceSource.Teleports == teleports && overlay.IsOpen, $"Unattuned result cannot teleport: open={overlay.IsOpen}, teleports={driver.PlaceSource.Teleports}, query={overlay.Query}, selected={overlay.Results.ElementAtOrDefault(overlay.Selected).Place?.Id}");
            var row = overlay.FirstResultCenter;
            driver.Step(new(3.36, Alt: false, Mouse: row, Left: true)); driver.Step(new(3.38, Left: false));
            Require(driver.PlaceSource.Shown?.AetheryteId == 2 && !overlay.IsOpen, "Click shows an unattuned location");
            overlay.Open("a:cryst"); driver.Step(new(3.4, Mouse: new(-1000))); driver.Step(new(3.42));
            driver.Step(new(3.44, Alt: true, Keys: [ImGuiKey.Enter]));
            Require(driver.PlaceSource.Teleports == teleports && overlay.IsOpen, "High cost waits for confirmation");
            var confirm = overlay.ConfirmationCenter;
            driver.Step(new(3.46, Alt: false, Mouse: confirm, Left: true)); driver.Step(new(3.48, Left: false));
            Require(driver.PlaceSource.Teleports == teleports + 1, "High-cost confirmation button invokes action once");
            driver.Step(new(3.5, Focused: false)); Require(!overlay.IsOpen, "Focus loss releases palette input capture");
            driver.Config.PinChrome = false;
            driver.Step(new(4, Focused: true, Mouse: driver.Window.MoveHandle.Geometry.Center));
            Require(driver.Window.MoveHandle.BlocksMapInput, "Fixture hovered the actual move handle");
            driver.Step(new(4.02, Control: true, Keys: [ImGuiKey.K]));
            driver.Step(new(4.04, Control: false, Mouse: new(-1000))); var idle = driver.Step(new(6));
            Require(overlay.IsOpen && idle.ChromeProgress == 0, "Opening from a hovered move handle must release stale move ownership and allow chrome to hide");
        }
        using (var driver = new ScriptedFrameDriver(Config(), new FrameScript { Palette = true })) {
            driver.Config.VisualStyle = 1;
            driver.Step(new(0)); driver.Step(new(.02, Control: true, Keys: [ImGuiKey.K]));
            Require(!driver.Palette!.IsOpen, "Classic never opens palette");
        }
        using (var driver = new ScriptedFrameDriver(Config(), new FrameScript { Palette = true })) {
            driver.Step(new(0)); driver.Step(new(.02, Focused: false, Control: true, Keys: [ImGuiKey.K]));
            Require(!driver.Palette!.IsOpen, "Shortcut requires map focus");
        }
        foreach (var circular in new[] { false, true }) {
            var config = Config(); config.MapCircle = circular; config.WindowSize = circular ? new(600) : new(260, 350);
            using var driver = new ScriptedFrameDriver(config, new FrameScript { Palette = true });
            driver.Step(new(0)); driver.Step(new(.02)); driver.Palette!.Open("no such zzzzz"); driver.Step(new(.04));
            var overlay = driver.Palette;
            Require(overlay.Results.Count == 0, "Empty result set is drawn safely");
            Require(overlay.PanelMin.X >= config.WindowPosition.X && overlay.PanelMax.X <= config.WindowPosition.X + config.WindowSize.X, "Narrow overlay is clamped");
            if (circular) Require(Vector2.Distance(overlay.PanelMin, config.WindowPosition + config.WindowSize / 2) < config.WindowSize.X / 2, "Palette corners fit circular frame");
        }
        Console.WriteLine("Palette: shared-frame shortcut/text/navigation/selection/Esc, toolbar routing, Classic/focus gating, mouse isolation and ImGui capture.");
    }
}
