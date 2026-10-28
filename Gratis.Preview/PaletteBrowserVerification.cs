using System.Numerics;
using System.Text.Json;
using Gratis.Controllers;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;
using ImGuiNET;

namespace Gratis.Preview;

internal static class PaletteBrowserVerification {
    private static void Require(bool value, string message) {
        if (!value)
            throw new InvalidOperationException(message);
    }

    public static void Run() {
        var expansions = PaletteBrowser.Expansions(PaletteFixture.Places);
        Require(expansions.Length == 6 && expansions[0].Name == "A Realm Reborn" && expansions[5].Name == "Dawntrail",
            "Expansion identities and order");
        Require(expansions.Sum(e => e.Places) == PaletteFixture.Places.Count(p =>
            p.Kind is PlaceKind.Zone or PlaceKind.Aetheryte),
            "Every destination belongs to one expansion");
        Require(PaletteBrowser.Expansions([new("future", PlaceKind.Zone, "Future zone", "", 99, 99,
            Expansion: 9) { ExpansionName = "Future expansion" }])[0].Name == "Future expansion",
            "Sheet-driven future expansions");
        var generic = new PlaceEntry("one", PlaceKind.Marker, "Aetheryte", "Zone > Area", 1, 1, new(100, 20));
        PlaceResult[] duplicates = [new(generic, 1, default, 100), new(generic with { Id = "copy" }, 0, default, 100),
            new(generic with { Id = "two", World = new(200, 30) }, 0, default, 200)];
        var grouped = PaletteBrowser.Distinct(duplicates);
        Require(grouped.Length == 2
            && grouped[0].Distance != grouped[1].Distance
            && PaletteBrowser.Name(grouped[0], grouped) == "Area",
            "Physical duplicates collapse; separate aetherytes have distinct distance labels");
        var equalDistances = new[] { grouped[0], grouped[1] with { Distance = grouped[0].Distance } };
        Require(PaletteBrowser.Coordinates(equalDistances[0], equalDistances) != PaletteBrowser.Coordinates(equalDistances[1],
            equalDistances),
            "Equal-distance destinations use their distinct coordinates");
        Require(PaletteBrowser.Coordinates(grouped[0] with { Distance = null }, grouped) == "X 100, Z 20",
            "Missing player uses coordinates for disambiguation");
        Require(PaletteBrowser.Distinct([new(generic with { Kind = PlaceKind.Quest }, 0, default, null),
            new(generic with { Id = "quest2", Kind = PlaceKind.Quest }, 0, default, null)]).Length == 2,
            "Quest objectives remain individually searchable");
        var cache = new PaletteRowNames();
        var rowText = cache.Text(grouped[0], grouped);
        Require(rowText.Name == "Area" && rowText.Detail == "Area" && rowText.Distance == "100y"
            && rowText.Expansion == "A Realm Reborn", "Name, context, distance and expansion remain separate fields");
        for (var i = 0; i < 64; i++) _ = cache.Text(grouped[0], grouped);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1024; i++) _ = cache.Text(grouped[0], grouped);
        Require(GC.GetAllocatedBytesForCurrentThread() == before, "Warmed row metadata must not allocate");
        Require(cache.Text(grouped[0] with { Distance = null }, grouped).Distance == "",
            "Missing distance does not invent proximity");
        var unknown = new[] { grouped[0] with { Distance = null } };
        Require(cache.Text(unknown[0], unknown).Coordinates == "X 100, Z 20",
            "A single destination without player distance retains its coordinates");
        var unlocated = equalDistances.Select(r => r with { Place = r.Place with { World = null }, Distance = null }).ToArray();
        var firstUnlocated = cache.Text(unlocated[0], unlocated);
        Require(firstUnlocated.Coordinates == "Map 1" && firstUnlocated.Identifier == "one"
            && firstUnlocated.Name == "Area", "Unknown coordinates keep map and identity separate from the name");
        foreach (var columns in new[] { 1, 2, 3 }) {
            Require(PaletteBrowser.Navigate(0, 6, columns, 0, 1) == columns, "Down advances one grid row");
            Require(PaletteBrowser.Navigate(5, 6, columns, 1, 1) == 5
                && PaletteBrowser.Navigate(0, 6, columns, -1, -1) == 0,
                "Card navigation clamps");
        }
        var c = JsonSerializer.Deserialize<SystemConfig>("{}", ScriptedFrameDriver.Json)!;
        Require(c.PaletteImagery, "Old configs enable location imagery");
        c.PaletteImagery = false;
        SettingsLayout.Entries.Single(e => e.Path == "PaletteImagery").Reset(c, new());
        Require(c.PaletteImagery, "Imagery settings reset restores default");
        var browserConfig = PaletteVerification.Config();
        browserConfig.FlatCornerRadius = 10;
        using (var driver = new ScriptedFrameDriver(browserConfig, new FrameScript { Palette = true })) {
            driver.Step(new(0));
            driver.Step(new(.02));
            const string longLabel = "A long destination shared by rows with and without a distance";
            for (var i = 0; i < 64; i++) {
                _ = cache.Fit(longLabel, 90);
                _ = cache.Fit(longLabel, 120);
            }
            var fittedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1024; i++) {
                _ = cache.Fit(longLabel, 90);
                _ = cache.Fit(longLabel, 120);
            }
            Require(GC.GetAllocatedBytesForCurrentThread() == fittedBefore,
                "Repeated names at different row widths must not allocate after warm-up");
            var overlay = driver.Palette!;
            overlay.Open();
            driver.Step(new(.04));
            var source = driver.PlaceSource!;
            source.CrestRequests.Clear();
            driver.Step(new(.06, Keys: [ImGuiKey.RightArrow]));
            Require(source.CrestRequests.Count > 0
                && source.CrestRequests.All(p => expansions.Any(e => e.Cover.Id == p.Id))
                && source.CrestRequests.Select(p => p.Id).Distinct().Count() == source.CrestRequests.Count,
                "Only visible expansion cards request crests; nearby cards do not");
            Require(overlay.BrowserSelection == 1, "Native Right selects the next expansion");
            source.CrestsReady = false;
            source.CrestRequests.Clear();
            driver.Step(new(.08));
            Require(source.CrestRequests.Count > 0 && !source.Crest(source.CrestRequests[0]).Ready,
                "Visible expansion cards tolerate unavailable crests");
            source.CrestsReady = true;
            driver.Step(new(.10, Keys: [ImGuiKey.Enter]));
            source.CrestRequests.Clear();
            driver.Step(new(.12));
            Require(source.CrestRequests.Count == 0, "Zone and aetheryte cards do not request crests");
            Require(overlay.BrowsedExpansion == 1 && overlay.Results.All(p => p.Place.Expansion == 1),
                "Enter drills into the selected expansion");
            driver.Step(new(.125, Keys: [ImGuiKey.Enter]));
            driver.Step(new(.13, Keys: [ImGuiKey.DownArrow]));
            driver.Step(new(.135));
            driver.Step(new(.14, Keys: [ImGuiKey.Enter]));
            Require(!overlay.IsOpen && driver.PlaceSource!.Shown?.Expansion == 1,
                "Enter shows selected place under its zone");
            driver.Step(new(.16));
            overlay.Open();
            driver.Step(new(.18));
            for (var i = 0; i < 6; i++)
                driver.Step(new(.185 + i * .001, Mouse: overlay.PanelMin + (overlay.PanelMax - overlay.PanelMin) / 2,
                    Wheel: -3));
            driver.Step(new(.19));
            var point = overlay.FirstCardCenter;
            driver.Step(new(.20, Mouse: point));
            driver.Step(new(.22, Left: true));
            driver.Step(new(.24, Left: false));
            driver.Step(new(.26));
            Require(overlay.BrowsedExpansion == 0, "Mouse selects expansion card");
            driver.Step(new(.28, Keys: [ImGuiKey.Backspace]));
            driver.Step(new(.30));
            Require(overlay.BrowsedExpansion == null, "Backspace returns to expansions");
            source.CrestRequests.Clear();
            driver.Step(new(.32, Text: "limsa"));
            Require(source.CrestRequests.Count == 0, "Search result thumbnails do not request crests");
            Require(overlay.Query == "limsa" && overlay.Results.Count > 0, "Typing switches to ranked results");
        }
        CategoryFrames();
        foreach (var scale in new[] { .8f, 1f, 1.6f })
            foreach (var circle in new[] { false, true }) {
                var config = PaletteVerification.Config();
                config.WindowSize = new(330, 480);
                config.ChromeScale = scale;
                config.MapCircle = circle;
                using var driver = new ScriptedFrameDriver(config, new FrameScript { Palette = true });
                driver.Step(new(0));
                driver.Step(new(.02));
                driver.Palette!.Open();
                driver.Step(new(.04));
                foreach (var text in new[] { "", "a:limsa" }) {
                    driver.Palette.Open(text);
                    driver.Step(new(.06 + text.Length * .01));
                    var min = driver.Palette.PanelMin;
                    var max = driver.Palette.PanelMax;
                    Require(min.X >= config.WindowPosition.X && min.Y >= config.WindowPosition.Y
                        && max.X <= config.WindowPosition.X + config.WindowSize.X
                        && max.Y <= config.WindowPosition.Y + config.WindowSize.Y,
                        "Panel bounds stay inside small windows at every chrome scale");
                    if (circle)
                        Require(Vector2.Distance(min,
                            config.WindowPosition + config.WindowSize / 2) <= config.WindowSize.X
                            / 2,
                            "Circular panel corners stay inside rim");
                }
            }
        Console.WriteLine("Palette browser: expansion grouping, exact duplicates, useful labels, "
            + "settings/default/reset, native card navigation, drill/show/mouse/type, small/scaled/circular bounds.");
    }

    private static void CategoryFrames() {
        foreach (var kind in new[] { PlaceKind.Aetheryte, PlaceKind.Zone, PlaceKind.Quest, PlaceKind.Marker }) {
            using var driver = new ScriptedFrameDriver(PaletteVerification.Config(), new FrameScript { Palette = true });
            var source = driver.PlaceSource!;
            source.AdditionalPlaces.Add(new("z:browse", PlaceKind.Zone, "A browse zone", "Region > A browse zone", 50, 50));
            if (kind != PlaceKind.Zone)
                for (var i = 0; i < 10; i++)
                    source.AdditionalPlaces.Add(new($"browse:{i}", kind, $"Browse sample {i}", "Region > A browse zone",
                        50, 50, new(i * 10)));
            source.AdditionalPlaces.Add(new("browse:other", kind, "Browse other expansion", "Dravania > Idyllshire",
                7, 6, new(40), Expansion: 1));
            if (kind == PlaceKind.Marker)
                source.AdditionalPlaces.Add(new("browse:current", kind, "Browse current map", "La Noscea > Limsa Lominsa",
                    1, 1, new(80)));
            driver.Step(new(0));
            driver.Step(new(.02));
            var overlay = driver.Palette!;
            overlay.Open();
            var time = .04;
            driver.Step(new(time));
            for (var tab = 0; tab < (int)kind; tab++) {
                driver.Step(new(time += .02, Keys: [ImGuiKey.Tab]));
                driver.Step(new(time += .02));
            }
            var expected = PaletteBrowser.Distinct(PlaceSearch.Find(source.Entries, "", kind, source.Context,
                new Dictionary<string, long>(), source.Access, currentMapMarkersOnly: false));
            Require(overlay.Query == "" && overlay.BrowsedExpansion == null
                && overlay.Results.Select(r => r.Place.Id).SequenceEqual(expected.Select(r => r.Place.Id)),
                $"{kind}: empty tab uses the browser's complete kind snapshot; query='{overlay.Query}', "
                    + $"actual={string.Join(',', overlay.Results.Select(r => r.Place.Id))}, "
                    + $"expected={string.Join(',', expected.Select(r => r.Place.Id))}");
            Require(overlay.Expansions.Sum(e => e.Places) == expected.Length
                && overlay.Expansions.All(e => e.Places == expected.Count(r => r.Place.Expansion == e.Id)),
                $"{kind}: expansion counts contain only eligible entries of this kind");
            var covers = PaletteBrowser.Expansions(source.Entries);
            Require(overlay.Expansions.All(e => e.Cover == covers.Single(c => c.Id == e.Id).Cover),
                $"{kind}: cards reuse the All browser's covers");
            driver.Step(new(time += .02, Keys: [ImGuiKey.Enter]));
            driver.Step(new(time += .02));
            Require(overlay.BrowsedExpansion == 0 && overlay.Results.All(r => r.Place.Kind == kind
                && r.Place.Expansion == 0), $"{kind}: Enter drills into the expansion");
            var zones = PaletteBrowser.Zones(overlay.Results, source.Entries);
            Require(zones[0].Name == "A browse zone" && zones[0].MapId == 50,
                $"{kind}: headers use zone metadata rather than an objective or shard name");
            driver.Step(new(time += .02, Keys: [ImGuiKey.End]));
            Require(overlay.BrowserSelection == zones.Length - 1, $"{kind}: zones start collapsed");
            driver.Step(new(time += .02, Keys: [ImGuiKey.Home]));
            driver.Step(new(time += .02, Keys: [ImGuiKey.RightArrow]));
            driver.Step(new(time += .02, Keys: [ImGuiKey.End]));
            Require(overlay.BrowserSelection == zones.Length - 1 + Math.Min(4, zones[0].Places.Length)
                + (zones[0].Places.Length > 4 ? 1 : 0), $"{kind}: an open zone is limited to four entries");
            if (kind != PlaceKind.Zone) {
                driver.Step(new(time += .02, Keys: [ImGuiKey.Home]));
                for (var row = 0; row < 5; row++) {
                    driver.Step(new(time += .02, Keys: [ImGuiKey.DownArrow]));
                    driver.Step(new(time += .02));
                }
                driver.Step(new(time += .02, Keys: [ImGuiKey.Enter]));
                driver.Step(new(time += .02, Keys: [ImGuiKey.End]));
                Require(overlay.BrowserSelection == zones.Length - 1 + 8 + 1,
                    $"{kind}: Show more reveals one further page");
            }
            driver.Step(new(time += .02, Keys: [ImGuiKey.Home]));
            driver.Step(new(time += .02, Keys: [ImGuiKey.LeftArrow]));
            driver.Step(new(time += .02, Keys: [ImGuiKey.End]));
            Require(overlay.BrowserSelection == zones.Length - 1, $"{kind}: Left collapses the selected zone");
            driver.Step(new(time += .02, Keys: [ImGuiKey.Backspace]));
            driver.Step(new(time += .02));
            Require(overlay.IsOpen && overlay.BrowsedExpansion == null, $"{kind}: Backspace returns home");
            driver.Step(new(time += .02, Keys: [ImGuiKey.Enter]));
            driver.Step(new(time += .02));
            driver.Step(new(time += .02, Escape: true));
            Require(overlay.IsOpen && overlay.BrowsedExpansion == null, $"{kind}: Escape returns home");
            driver.Step(new(time += .02, Escape: false));
            driver.Step(new(time += .02, Keys: [ImGuiKey.Enter]));
            driver.Step(new(time += .02));
            driver.Step(new(time += .02, Keys: [ImGuiKey.RightArrow]));
            driver.Step(new(time += .02));
            driver.Step(new(time += .02));
            var rowCenter = overlay.FirstResultCenter;
            driver.Step(new(time += .02, Mouse: rowCenter));
            driver.Step(new(time += .02));
            Require(overlay.BrowserSelection == 1 && overlay.FirstResultCenter == rowCenter,
                $"{kind}: hovering selects a row without scrolling its zone");
            driver.Step(new(time += .02, Text: "browse"));
            var ranked = PaletteBrowser.Distinct(PlaceSearch.Find(source.Entries, "browse", kind, source.Context,
                new Dictionary<string, long>(), source.Access));
            Require(overlay.Query == "browse"
                && overlay.Results.Select(r => r.Place.Id).SequenceEqual(ranked.Select(r => r.Place.Id))
                && (kind == PlaceKind.Marker || overlay.Results.Any(r => r.Place.Expansion == 1)),
                $"{kind}: typing ignores the drilled expansion and preserves ranked search; "
                    + $"query='{overlay.Query}', open={overlay.IsOpen}, expansion={overlay.BrowsedExpansion}, "
                    + $"actual={string.Join(',', overlay.Results.Select(r => r.Place.Id))}, "
                    + $"expected={string.Join(',', ranked.Select(r => r.Place.Id))}");
            driver.Step(new(time += .02, Escape: true));
            Require(!overlay.IsOpen, $"{kind}: Escape closes ranked search");
        }
        Console.WriteLine("Palette categories: four empty-query tabs, scoped counts/covers, zone headers, "
            + "collapse/page/navigation, Backspace/Escape and unchanged ranked query override.");
    }
}
