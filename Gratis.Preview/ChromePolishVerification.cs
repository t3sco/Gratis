using System.Numerics;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class ChromePolishVerification
{
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    internal static Vector2 Center(Vector4 b) => new((b.X + b.Z) / 2, (b.Y + b.W) / 2);

    public static void Run()
    {
        DeclutterLayout();
        var config = ChromeLayoutVerification.Config(new(780, 600));
        using var driver = new ScriptedFrameDriver(config, new FrameScript { Width = 1000, Height = 800,
            Markers = [new(71001, new(80, 60)), new(60412, new(-80, -30))] });
        var places = new TravelFixture(driver);
        var travel = driver.Context.Assistance = new MapAssistance(NullTravelProvider.Instance, places, () => true, () => { });
        double time = 0;
        void Step(Vector2? mouse = null, bool? left = null, double dt = .04) => driver.Step(new(time += dt, Mouse: mouse ?? new(-1000), Left: left));
        void Click(Vector2 mouse) { Step(mouse); Step(mouse, true); Step(mouse, false); Step(mouse); }
        Step(); Step();
        var toolbar = MapToolbarDrawing.LastBounds;
        Require(toolbar.Z - toolbar.X == 6 * GratisTheme.ControlHeight + 8 && driver.Trace.WidgetCount == 6,
            "Six deliberate toolbar concepts replace the busy navigation row");
        Click(new(toolbar.X + 4 + 1.5f * GratisTheme.ControlHeight, toolbar.Y + GratisTheme.ToolbarHeight / 2));
        Require(MapToolbarDrawing.MapMenuOpen, "Lens button opens the lens/navigation menu");
        driver.Step(new(time += .04, Escape: true)); Step();
        toolbar = MapToolbarDrawing.LastBounds;
        Click(new(toolbar.X + 4 + 2.5f * GratisTheme.ControlHeight, toolbar.Y + GratisTheme.ToolbarHeight / 2));
        Require(FilterBarDrawing.PopoverOpen && FilterBarDrawing.ChipBounds[(int)MarkerGroup.Quests] != Vector4.Zero,
            $"Toolbar Filters opens the same drawer as the bottom trigger: open={FilterBarDrawing.PopoverOpen}, lens={MapToolbarDrawing.MapMenuOpen}, quests={driver.Context.Visibility.Counts[(int)MarkerGroup.Quests]}");
        Require(FilterBarDrawing.ChipBounds[(int)MarkerGroup.Housing] == Vector4.Zero, "Empty map groups do not crowd the drawer");
        driver.Context.Visibility.Solo(MarkerGroup.Services); Step();
        Require(FilterBarDrawing.SoloGroup == (int)MarkerGroup.Services, "Solo has an explicit visual state");
        driver.Step(new(time += .04, Escape: true)); Step();
        driver.Context.Visibility.ResetFilters();
        travel.OfferContextTarget(new(new(400, 0), 1, SpotlightSource.Quest), time);
        Step(); Step();
        Require(travel.Chip != null && travel.ChipBounds.W - travel.ChipBounds.Y == 104, "Travel card uses a compact shared height");
        var action = travel.ChipButtonCenter; var dismiss = travel.ChipDismissButtonCenter;
        Require(action.Y > travel.ChipBounds.Y && action.Y < travel.ChipBounds.W - 12 && dismiss.Y == action.Y,
            "Teleport and visible Dismiss share a baseline inside card padding");
        Step(dt: 3);
        var remaining = travel.ChipTimeRemaining;
        Step(Center(travel.ChipBounds), dt: 2);
        Require(MathF.Abs(travel.ChipTimeRemaining - remaining) < .001f, "Travel expiry pauses while the card is hovered");
        Step(dt: 6);
        Require(travel.Chip == null && places.Teleports == 0, "Travel offer expires after eight idle seconds without executing");
        travel.OfferContextTarget(new(new(400, 0), 1, SpotlightSource.Quest), time);
        Step(action, true); Step(action, false);
        Require(places.Teleports == 0, "An opening click under the future Teleport button is consumed safely");
        Step(); Click(travel.ChipButtonCenter);
        Require(places.Teleports == 1, "Reoffered card still teleports only by explicit click");
        config.ReduceMotion = true; Step();
        Require(travel.Chip != null, "Reduce motion retains the travel action without a ticking line");
        KeyboardAndLabels();
        Console.WriteLine("Chrome polish: six toolbar actions, lens/filter routing, present-only categories, solo cue, compact card baselines, paused expiry, explicit travel and Reduce motion.");
    }

    internal static List<ScriptMarker> DenseMarkers() => Enumerable.Range(0, 24)
        .Select(i => new ScriptMarker(60412, new(100 + i % 6 * 8, 80 + i / 6 * 8))).ToList();

    private static void DeclutterLayout()
    {
        foreach (var size in new[] { new Vector2(360, 280), new Vector2(780, 600) }) {
            var config = ChromeLayoutVerification.Config(size); config.Markers.Declutter = true;
            using var driver = new ScriptedFrameDriver(config, new FrameScript {
                Width = 1000, Height = 800, Markers = DenseMarkers(),
            });
            driver.Step(new(0)); driver.Step(new(.04));
            var state = FilterBarDrawing.DeclutterBounds; var row = FilterBarDrawing.LastBounds;
            Require(driver.Context.Markers.Clusters > 0 && state.Z > state.X && state.X >= row.X && state.Z <= row.Z && state.Y == row.Y && state.W == row.W,
                "Decluttered state is a compact baseline-aligned part of the filter row");
            Require(!ChromeLayout.Overlap(row, MapWindowDrawing.TitleBounds) && !ChromeLayout.Overlap(row, MapWindowDrawing.CoordinateBounds), "Declutter state does not overlap bottom chrome");
        }
    }

    private static void KeyboardAndLabels()
    {
        var io = ImGui.GetIO(); io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        var focused = false;
        for (var frame = 0; frame < 5; frame++) {
            io.AddMousePosEvent(-1000, -1000);
            ImGui.NewFrame();
            ImGui.SetNextWindowPos(new(60)); ImGui.SetNextWindowSize(new(240, 140));
            if (frame == 0) ImGui.SetNextWindowFocus();
            ImGui.Begin("Chrome keyboard verification", ImGuiWindowFlags.NoSavedSettings);
            if (frame == 0) ImGui.SetKeyboardFocusHere();
            var pos = ImGui.GetCursorScreenPos();
            ChromeControls.Button("##keyboard-action", new(36));
            focused |= ImGui.IsItemFocused();
            ChromeControls.Feedback(ImGui.GetWindowDrawList(), pos, pos + new Vector2(36));
            var full = "The Rising Stones — rendezvous \U0001F30D";
            var fitted = ChromeControls.FitLabel(full, 100);
            Require(ImGui.CalcTextSize(fitted).X <= 100 && fitted.EndsWith('\u2026') && !fitted.Any(char.IsSurrogate), "Long labels fit with a complete ellipsis and no split Unicode characters");
            ImGui.End(); ImGui.Render();
        }
        Require(focused, "Custom-drawn chrome participates in native keyboard focus");
    }
}
