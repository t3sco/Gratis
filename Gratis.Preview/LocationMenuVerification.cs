using System.Numerics;
using Gratis.Controllers;
using Gratis.Data;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class LocationMenuVerification {
    private static void Require(bool value, string message) => MarkerVisibilityVerification.Require(value, message);
    private static Vector2 Center(Vector4 b) => new((b.X + b.Z) / 2, (b.Y + b.W) / 2);
    private static bool Valid(Vector4 b) => b.Z > b.X && b.W > b.Y;
    private static SystemConfig Config(Vector2? size = null) => new() {
        AcceptedSpoilerWarning = true, FollowPlayer = false, PinChrome = true, ReduceMotion = true,
        WindowPosition = new(24), WindowSize = size ?? new(820, 660), MapScale = .5f,
        ShowCoordinateBar = false, LocationAnchor = ChromeAnchor.TopLeft,
    };
    private static FrameScript Scene(int width = 1100, int height = 850) => new() {
        Width = width, Height = height, Palette = true,
        Title = "La Noscea - Limsa Lominsa Lower Decks",
    };

    public static void Run() {
        using (var driver = new ScriptedFrameDriver(Config(), Scene())) {
            var source = new Source(driver);
            driver.Context.LocationSource = source;
            driver.Context.Assistance = new(NullTravelProvider.Instance, new TravelFixture(driver), () => false, () => { });
            double time = 0;
            FrameSnapshot Step(Vector2? mouse = null, bool left = false, string? text = null, bool escape = false,
                ImGuiKey[]? keys = null, float wheel = 0, double dt = .04) =>
                driver.Step(new(time += dt, Mouse: mouse ?? new(-1000), Left: left, Text: text,
                    Escape: escape, Keys: keys, Wheel: wheel));
            void Click(Vector4 b) {
                Require(Valid(b), "Location menu target has bounds");
                var mouse = Center(b);
                Step(mouse);
                Step(mouse, true);
                Step(mouse);
                Step(mouse);
            }
            void Open() {
                Step();
                Click(LocationMenuDrawing.CardBounds);
                Require(LocationMenuDrawing.IsOpen, "Clicking the location card opens its dropdown");
            }
            Step();
            Step();
            var offset = driver.Renderer.DrawOffset;
            Open();
            Require(!driver.Window.Dragging && driver.Renderer.DrawOffset == offset,
                "Pressing the location card never pans the map");
            Require(ImGui.GetIO().WantCaptureKeyboard && LocationMenuDrawing.InputActive,
                "The open menu captures keyboard and map input");
            var siblings = LocationMenuDrawing.Rows.Where(r => r.Id.StartsWith("z:")).Select(r => r.Id).ToArray();
            Require(!siblings.Contains("z:1") && !siblings.Contains("z:other-expansion")
                && !siblings.Contains("z:other-region") && siblings.Contains("z:2"),
                "Region rows omit the current map and exclude other regions and expansions");
            var names = siblings.Select(id => source.Entries.First(p => p.Id == id).Name).ToArray();
            Require(names.SequenceEqual(names.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)),
                "Region maps are alphabetical");
            Step(text: "limsa");
            Step();
            Require(LocationMenuDrawing.Query == "limsa" && LocationMenuDrawing.Results.Count == 2
                && LocationMenuDrawing.Results.All(r => r.Place.Kind == PlaceKind.Zone),
                "Typing in the focused field narrows results to zones");
            var result = LocationMenuDrawing.Results.First(r => r.Place.MapId == 2).Place;
            Click(LocationMenuDrawing.Rows.First(r => r.Id == result.Id).Bounds);
            Require(driver.PlaceSource!.Shown == result && !LocationMenuDrawing.IsOpen,
                "Clicking a zone uses the palette map action with the right map");
            Open();
            Step(text: "limsa upper");
            Step();
            Step(keys: [ImGuiKey.Enter]);
            Require(driver.PlaceSource!.Shown?.MapId == 2 && !LocationMenuDrawing.IsOpen,
                "Enter opens the first zone result");
            Open();
            Click(LocationMenuDrawing.Rows.First(r => r.Id == "parent:0").Bounds);
            Require(source.ParentCalls == 1 && !LocationMenuDrawing.IsOpen,
                "The parent row calls the supplied parent action");
            Open();
            Step(escape: true);
            Require(!LocationMenuDrawing.IsOpen && LocationMenuDrawing.CapturesKeyboard,
                "Escape closes and remains captured on dismissal");
            Step(escape: true);
            Require(LocationMenuDrawing.CapturesKeyboard, "Held Escape cannot reach the map window");
            Step();
            Open();
            driver.Config.PinChrome = false;
            var before = driver.Renderer.DrawOffset;
            Step(new(-1000), dt: 4);
            Require(driver.Context.Chrome.Progress == 1 && LocationMenuDrawing.IsOpen,
                "The dropdown keeps chrome visible while the pointer is away");
            var row = LocationMenuDrawing.Rows.First(r => r.Id.StartsWith("z:")).Bounds;
            Step(Center(row), true);
            Step(Center(row) + new Vector2(20, 15), true, wheel: 1);
            Require(!driver.Window.Dragging && driver.Renderer.DrawOffset == before,
                "Dragging and scrolling in the dropdown cannot pan the map");
            Step(escape: true);
            Step();
            driver.Config.PinChrome = true;
            Open();
            var output = Path.Combine(Path.GetTempPath(), "claude", "location-menu", "dropdown.png");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            driver.Save(output);
            Step(escape: true);
            Step();
            driver.Config.VisualStyle = 1;
            Step();
            Click(LocationMenuDrawing.CardBounds);
            Require(!LocationMenuDrawing.IsOpen && !LocationMenuDrawing.InputActive,
                "Classic draws no dropdown and captures no location input");
        }
        foreach (var anchor in Enum.GetValues<ChromeAnchor>()) {
            using var driver = new ScriptedFrameDriver(Config(new(300, 220)), Scene(400, 300));
            driver.Config.LocationAnchor = anchor;
            driver.Context.LocationSource = new Source(driver);
            driver.Step(new(0));
            driver.Step(new(.04));
            var point = Center(LocationMenuDrawing.CardBounds);
            driver.Step(new(.08, Mouse: point));
            driver.Step(new(.12, Left: true));
            driver.Step(new(.16, Left: false));
            driver.Step(new(.2));
            var bounds = LocationMenuDrawing.PopoverBounds;
            Require(LocationMenuDrawing.IsOpen && Valid(bounds) && bounds.X >= 0 && bounds.Y >= 0
                && bounds.Z <= driver.Width && bounds.W <= driver.Height,
                $"Small-window dropdown stays in the viewport at {anchor}");
            var card = LocationMenuDrawing.CardBounds;
            Require(bounds.W <= card.Y || bounds.Y >= card.W,
                $"Dropdown stays above or below the card at {anchor}");
        }
        Console.WriteLine("Location menu: card, zone search, map actions, parents, Escape, input, chrome, Classic and six anchors verified.");
    }

    private sealed class Source(ScriptedFrameDriver driver) : ILocationMenuSource, IPaletteSource {
        public int ParentCalls { get; private set; }
        public IPaletteSource Places => this;
        public IReadOnlyList<LocationMenuParent> Parents => [new("La Noscea", () => ParentCalls++)];
        public IEnumerable<PlaceEntry> Entries => PaletteFixture.Places.Concat(Enumerable.Range(20, 12)
            .Select(i => new PlaceEntry("z:" + i, PlaceKind.Zone, i == 20 ? "Eastern La Noscea" : "Western coast " + i, "La Noscea > Coast", (uint)i, 1)))
            .Append(new("z:other-expansion", PlaceKind.Zone, "Another coast", "La Noscea > Coast", 80, 1, Expansion: 1))
            .Append(new("z:other-region", PlaceKind.Zone, "Another region", "Thanalan > Desert", 81, 1));
        public int Revision => 1;
        public bool Building => false;
        public PlaceSearchContext Context => new(driver.MapId, 1, driver.PlayerPosition);
        public PlaceAccess Access(PlaceEntry place) => default;
        public void Refresh() { }
        public void Show(PlaceEntry place) => driver.PlaceSource!.Show(place);
        public void ShowMainStory() { }
        public string Teleport(PlaceEntry place, bool confirmed) => "";
        public void Used() { }
    }
}
