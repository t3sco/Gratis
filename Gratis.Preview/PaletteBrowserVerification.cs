using System.Numerics;
using System.Text.Json;
using Gratis.Controllers;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;
using ImGuiNET;

namespace Gratis.Preview;

internal static class PaletteBrowserVerification
{
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public static void Run()
    {
        var expansions = PaletteBrowser.Expansions(PaletteFixture.Places);
        Require(expansions.Length == 6 && expansions[0].Name == "A Realm Reborn" && expansions[5].Name == "Dawntrail", "Expansion identities and order");
        Require(expansions.Sum(e => e.Places) == PaletteFixture.Places.Count(p => p.Kind is PlaceKind.Zone or PlaceKind.Aetheryte), "Every destination belongs to one expansion");
        Require(PaletteBrowser.Expansions([new("future", PlaceKind.Zone, "Future zone", "", 99, 99, Expansion: 9) { ExpansionName = "Future expansion" }])[0].Name == "Future expansion", "Sheet-driven future expansions");
        var generic = new PlaceEntry("one", PlaceKind.Marker, "Aetheryte", "Zone > Area", 1, 1, new(100, 20));
        PlaceResult[] duplicates = [new(generic, 1, default, 100), new(generic with { Id = "copy" }, 0, default, 100),
            new(generic with { Id = "two", World = new(200, 30) }, 0, default, 200)];
        var grouped = PaletteBrowser.Distinct(duplicates);
        Require(grouped.Length == 2 && PaletteBrowser.Name(grouped[0], grouped) != PaletteBrowser.Name(grouped[1], grouped), "Physical duplicates collapse; separate aetherytes have distinct distance labels");
        var equalDistances = new[] { grouped[0], grouped[1] with { Distance = grouped[0].Distance } };
        Require(PaletteBrowser.Name(equalDistances[0], equalDistances) != PaletteBrowser.Name(equalDistances[1], equalDistances), "Equal-distance destinations use their distinct coordinates");
        Require(PaletteBrowser.Name(grouped[0] with { Distance = null }, grouped).Contains("100, 20"), "Missing player uses coordinates for disambiguation");
        Require(PaletteBrowser.Distinct([new(generic with { Kind = PlaceKind.Quest }, 0, default, null), new(generic with { Id = "quest2", Kind = PlaceKind.Quest }, 0, default, null)]).Length == 2, "Quest objectives remain individually searchable");
        foreach (var columns in new[] { 1, 2, 3 }) {
            Require(PaletteBrowser.Navigate(0, 6, columns, 0, 1) == columns, "Down advances one grid row");
            Require(PaletteBrowser.Navigate(5, 6, columns, 1, 1) == 5 && PaletteBrowser.Navigate(0, 6, columns, -1, -1) == 0, "Card navigation clamps");
        }
        var c = JsonSerializer.Deserialize<SystemConfig>("{}", ScriptedFrameDriver.Json)!;
        Require(c.PaletteImagery, "Old configs enable location imagery"); c.PaletteImagery = false;
        SettingsLayout.Entries.Single(e => e.Path == "PaletteImagery").Reset(c, new());
        Require(c.PaletteImagery, "Imagery settings reset restores default");
        using (var driver = new ScriptedFrameDriver(PaletteVerification.Config(), new FrameScript { Palette = true })) {
            driver.Step(new(0)); driver.Step(new(.02)); var overlay = driver.Palette!; overlay.Open(); driver.Step(new(.04));
            driver.Step(new(.06, Keys: [ImGuiKey.RightArrow]));
            Require(overlay.BrowserSelection == 1, "Native Right selects the next expansion");
            driver.Step(new(.08)); driver.Step(new(.10, Keys: [ImGuiKey.Enter])); driver.Step(new(.12));
            Require(overlay.BrowsedExpansion == 1 && overlay.Results.All(p => p.Place.Expansion == 1), "Enter drills into the selected expansion");
            driver.Step(new(.14, Keys: [ImGuiKey.Enter]));
            Require(!overlay.IsOpen && driver.PlaceSource!.Shown?.Expansion == 1, "Enter shows selected place from grid");
            driver.Step(new(.16)); overlay.Open(); driver.Step(new(.18));
            var point = overlay.FirstCardCenter;
            driver.Step(new(.20, Mouse: point)); driver.Step(new(.22, Left: true)); driver.Step(new(.24, Left: false)); driver.Step(new(.26));
            Require(overlay.BrowsedExpansion == 0, "Mouse selects expansion card");
            driver.Step(new(.28, Keys: [ImGuiKey.Backspace])); driver.Step(new(.30));
            Require(overlay.BrowsedExpansion == null, "Backspace returns to expansions");
            driver.Step(new(.32, Text: "limsa")); Require(overlay.Query == "limsa" && overlay.Results.Count > 0, "Typing switches to ranked results");
        }
        foreach (var scale in new[] { .8f, 1f, 1.6f })
        foreach (var circle in new[] { false, true }) {
            var config = PaletteVerification.Config(); config.WindowSize = new(330, 480); config.ChromeScale = scale; config.MapCircle = circle;
            using var driver = new ScriptedFrameDriver(config, new FrameScript { Palette = true });
            driver.Step(new(0)); driver.Step(new(.02)); driver.Palette!.Open(); driver.Step(new(.04));
            foreach (var text in new[] { "", "a:limsa" }) {
                driver.Palette.Open(text); driver.Step(new(.06 + text.Length * .01));
                var min = driver.Palette.PanelMin; var max = driver.Palette.PanelMax;
                Require(min.X >= config.WindowPosition.X && min.Y >= config.WindowPosition.Y && max.X <= config.WindowPosition.X + config.WindowSize.X && max.Y <= config.WindowPosition.Y + config.WindowSize.Y, "Panel bounds stay inside small windows at every chrome scale");
                if (circle) Require(Vector2.Distance(min, config.WindowPosition + config.WindowSize / 2) <= config.WindowSize.X / 2, "Circular panel corners stay inside rim");
            }
        }
        Console.WriteLine("Palette browser: expansion grouping, exact duplicates, useful labels, settings/default/reset, native card navigation, drill/show/mouse/type, small/scaled/circular bounds.");
    }
}
