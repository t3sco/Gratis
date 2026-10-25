using System.Numerics;
using Gratis.Controllers;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class StoryVerification {
    private static void Require(bool yes, string message) {
        if (!yes)
            throw new InvalidOperationException(message);
    }

    public static void Selection() {
        foreach (var (icon, expected) in new (uint, bool)[]{(71001,false), (71002,false), (71003,true), (71004,true),
            (71005,true), (71006,true), (71013,false), (71203,true), (71205,true), (71213,false), (71023,false),
            (71143,false), (60071,false), (0,false)}) {
            var place = new PlaceEntry("q", PlaceKind.Quest, "Quest", "", 1, 1, new(100, 0)) { Icon = icon };
            Require(MainStoryObjective.IsObjective(place) == expected, "MSQ icon/state " + icon);
        }
        var near = new PlaceEntry("near", PlaceKind.Quest, "MSQ", "", 1, 1, new(100, 0)) { Icon = 71003 };
        Require(MainStoryObjective.Select([near with { Id = "far", World = new(1000, 0) }, near], 1,
            Vector2.Zero)?.Id == "near",
            "Nearest objective on current map");
        Require(MainStoryObjective.Select([near], 2, default) == null, "Other-map selection separated");
    }

    public static void Frames() {
        using var driver = new ScriptedFrameDriver(PaletteVerification.Config(),
            new FrameScript { Palette = true, Markers = [new(71003, new(700, 0), Objective: true)] });
        driver.Config.TravelActions = true;
        driver.Step(new(0));
        driver.Step(new(.02));
        driver.Palette!.Open("msq");
        driver.Step(new(.04));
        Require(driver.Palette.Results.Any(r => r.Place.Id == MainStoryObjective.Action.Id), "Palette story action");
        driver.Step(new(.06, Keys: [ImGuiKey.Enter]));
        Require(driver.Renderer.Spotlight.Active && driver.Renderer.Spotlight.Request?.Source == SpotlightSource.Quest,
            "MSQ action starts Quest Spotlight");
        driver.Step(new(.08, Keys: []));
        driver.Markers[0].IconId = 71023;
        driver.Step(new(.10));
        driver.Context.Story.Show(driver.Renderer, [], new(1, 1, Vector2.Zero));
        Require(driver.Context.Story.Message.StartsWith("No main story objective on this map"), "No current MSQ toast");
        driver.Renderer.Spotlight.Cancel("fixture: no current objective");
        var source = new StoryTravelFixture(driver);
        var ui = driver.Context.Assistance = new MapAssistance(NullTravelProvider.Instance, source, () => true,
            () => { });
        var other = new PlaceEntry("q:other", PlaceKind.Quest, "Tracked main story", "", 2, 2,
            new(550, 0)) { Icon = 71003 };
        driver.Context.Story.Show(driver.Renderer, [other], new(1, 1, Vector2.Zero));
        driver.Step(new(.12));
        Require(driver.Context.Story.Target == other && ui.Chip?.Destination.MapId == 2
            && !driver.Renderer.Spotlight.Active,
            "Other-map objective offers matching attuned crystal without changing map");
        driver.Step(new(.14));
        driver.Step(new(.16, Mouse: ui.ChipButtonCenter));
        driver.Step(new(.18, Left: true));
        driver.Step(new(.20, Left: false));
        Require(source.Teleports == 1, "MSQ chip calls existing teleport boundary");
        driver.Step(new(13));
        Require(ui.Chip == null, "Fallback expires");
    }

    private sealed class StoryTravelFixture(ScriptedFrameDriver driver) : ITravelPlaces {
        private readonly TravelFixture source = new(driver);
        public IEnumerable<PlaceEntry> Entries => source.Entries.Select(p => p with { MapId = 2 });
        public PlaceSearchContext Context => source.Context;
        public PlaceAccess Access(PlaceEntry place) => source.Access(place);
        public void Refresh() => source.Refresh();
        public void Show(PlaceEntry place) => source.Show(place);
        public int Teleports => source.Teleports;
        public string Teleport(PlaceEntry place, bool confirmed) => source.Teleport(place, confirmed);
    }
}