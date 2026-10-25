using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class FilterBarVerification
{
    private static void Require(bool value, string message) => MarkerVisibilityVerification.Require(value, message);
    private static SystemConfig Config(Vector2 size) => new() { AcceptedSpoilerWarning = true, FollowPlayer = false,
        PinChrome = true, ReduceMotion = true, MapScale = .5f, WindowPosition = new Vector2(32), WindowSize = size };
    private static FrameScript Fixture() => new() { Width = 1100, Height = 800, Markers = [
        new(71001, new Vector2(-180, -80)), new(71021, new Vector2(-110, -80)), new(60501, new Vector2(10, -80)),
        new(60453, new Vector2(160, -80)), new(60412, new Vector2(-160, 60)), new(60434, new Vector2(-40, 60)),
        new(60436, new Vector2(40, 60)), new(60437, new Vector2(130, 60)), new(60003, new Vector2(210, 70)),
    ] };
    private static Vector2 Center(Vector4 bounds) => new((bounds.X + bounds.Z) / 2, (bounds.Y + bounds.W) / 2);
    private static bool Valid(Vector4 bounds) => bounds.Z > bounds.X && bounds.W > bounds.Y;
    private static bool Overlap(Vector4 a, Vector4 b) => Valid(a) && Valid(b) && a.X < b.Z && a.Z > b.X && a.Y < b.W && a.W > b.Y;
    public static void Run()
    {
        Presentation();
        using (var driver = new ScriptedFrameDriver(Config(new Vector2(960, 640)), Fixture())) {
            double time = 0;
            FrameSnapshot Step(Vector2? mouse = null, bool left = false, bool right = false, bool control = false, bool alt = false) =>
                driver.Step(new ScriptFrame(time += .04, Mouse: mouse ?? new Vector2(-1000), Left: left, Right: right, Control: control, Alt: alt));
            void Click(MarkerGroup group, bool right = false, bool control = false, bool alt = false) {
                Step();
                if (!Valid(FilterBarDrawing.ChipBounds[(int)group])) {
                    var overflow = Center(FilterBarDrawing.OverflowBounds);
                    Step(overflow); Step(overflow, left: true); Step(overflow); Step(overflow);
                }
                var bounds = FilterBarDrawing.ChipBounds[(int)group]; Require(Valid(bounds), $"Expected {group} chip in bar/overflow; row={FilterBarDrawing.LastBounds}; overflow={FilterBarDrawing.OverflowBounds}; input={FilterBarDrawing.InputActive}");
                var position = Center(bounds);
                Step(position, control: control, alt: alt); Step(position, !right, right, control, alt);
                Step(position); Step();
                driver.Step(new ScriptFrame(time += .04, Escape: true)); Step();
            }
            driver.Context.GetIconSetting(60436).Hide = true;
            Step(); Step();
            var model = driver.Context.Visibility;
            Require(model.Counts[(int)MarkerGroup.Services] == 2, "Counts exclude the per-icon hidden/game-ineligible input");
            var total = model.Counts.Sum();
            Click(MarkerGroup.Services);
            Require(!driver.Trace.DrawnMarkers.Any(m => m.IconId is 60412 or 60434) && driver.Trace.DrawnMarkers.Any(m => m.IconId == 71001), "Chip mute hides services and preserves quests");
            Require(model.Counts.Sum() == total, "Muting must not erase map counts");
            ImGui.GetIO().AddKeyEvent(ImGuiKey.RightAlt, true); Step();
            Require(model.Peek && driver.Trace.DrawnMarkers.Any(m => m.IconId == 60412), "Peek reveals explicitly muted services");
            ImGui.GetIO().AddKeyEvent(ImGuiKey.RightAlt, false); Step();
            Require(!model.Peek && !driver.Trace.DrawnMarkers.Any(m => m.IconId == 60412), "Peek release restores exact mute");
            var originalPosition = driver.Config.WindowPosition;
            foreach (var alt in new[] { false, true }) {
                Click(MarkerGroup.GatheringHunts, control: !alt, alt: alt);
                Require(model.State(MarkerGroup.GatheringHunts) == MarkerVisibilityState.Normal && model.State(MarkerGroup.Quests, true) == MarkerVisibilityState.Quiet, "Ctrl/Alt solo quiets other groups");
                Require(driver.Trace.DrawnMarkers.Any(m => m.IconId == 71001) && driver.Config.WindowPosition == originalPosition && !driver.Window.Dragging && !driver.Window.MoveHandle.Dragging, "Solo keeps priority and never pans/moves the window");
            }
            Click(MarkerGroup.Services, right: true); Require(model.State(MarkerGroup.Services) == MarkerVisibilityState.Loud, "Right click promotes");
            Click(MarkerGroup.Services, right: true); Require(model.State(MarkerGroup.Services) == MarkerVisibilityState.Quiet, "Right click quiets");
            driver.Config.ActiveLens = "Minimal"; Step();
            Require(model.EffectiveLens == "Minimal" && model.Counts.Sum() == total && driver.Trace.DrawnMarkers.Any(m => m.IconId == 71001) && !driver.Trace.DrawnMarkers.Any(m => m.IconId == 60412), "Lens switch recomputes counts before visibility culling");
            Click(MarkerGroup.Quests);
            Require(!driver.Trace.DrawnMarkers.Any(m => m.IconId == 71001) && driver.Trace.DrawnMarkers.Any(m => m.IconId == 60501), "First click in Minimal explicitly mutes visible priority group");
            Click(MarkerGroup.Quests);
            Require(driver.Trace.DrawnMarkers.Any(m => m.IconId == 71001), "Second click restores priority group");
            Require(driver.Renderer.BeginSpotlight(new SpotlightRequest(new Vector2(-160, 60), 1, SpotlightSource.Search, Icon: 60412, IncludePlayer: false)), "Start real spotlight");
            Step();
            Require(driver.Trace.DrawnMarkers.Any(m => m.IconId == 60412 && m.IsSpotlighted), "Minimal preserves spotlight target");
            model.Overrides[(int)MarkerGroup.Services] = MarkerVisibilityState.Hidden; Step();
            Require(driver.Renderer.Spotlight.Active && !driver.Trace.DrawnMarkers.Any(m => m.IconId == 60412), "Explicit category mute also controls the active spotlight target");
            driver.Renderer.Spotlight.Cancel("filter verification complete");
            driver.Config.LensCycleKey = (int)ImGuiKey.F6;
            driver.Config.CustomLenses.Add(new Lens { Name = "Saved" });
            ImGui.GetIO().AddKeyEvent(ImGuiKey.F6, true); Step();
            ImGui.GetIO().AddKeyEvent(ImGuiKey.F6, false); Step();
            Require(driver.Config.ActiveLens == "Saved", "Registered cycle includes saved custom lenses");
            Click(MarkerGroup.SideQuests);
            Require(!driver.Trace.DrawnMarkers.Any(m => m.IconId == 71021) && driver.Trace.DrawnMarkers.Any(m => m.IconId == 71001), "Explicit side-quest category mute is allowed, main quests survive");
            driver.Config.PinChrome = false; driver.Config.ChromeTitleMinAlpha = 0; Step();
            driver.Step(new ScriptFrame(time + 3, Mouse: new Vector2(-1000), Focused: false));
            Require(!Valid(FilterBarDrawing.LastBounds), "Filter bar follows chrome auto-hide");
        }
        foreach (var size in new[] { new Vector2(960,640), new Vector2(640,480), new Vector2(400,300), new Vector2(300,220), new Vector2(256,192) })
            foreach (var circle in new[] { false, true }) {
                var config = Config(size); config.MapCircle = circle;
                using var driver = new ScriptedFrameDriver(config, Fixture());
                driver.Step(new ScriptFrame(0)); driver.Step(new ScriptFrame(.1));
                var row = FilterBarDrawing.LastBounds;
                Require(Valid(row), $"Overflow control exists at {size}, circle={circle}");
                var handle = driver.Window.MoveHandle.Geometry;
                Require(!Overlap(row, MapToolbarDrawing.LastBounds) && !Overlap(row, MapWindowDrawing.TitleBounds) && !Overlap(row, MapWindowDrawing.CoordinateBounds) &&
                    !Overlap(row, new Vector4(handle.HitMin.X, handle.HitMin.Y, handle.HitMax.X, handle.HitMax.Y)), $"Row overlaps chrome at {size}, circle={circle}");
                var center = config.WindowPosition + size / 2;
                foreach (var point in new[] { new Vector2(row.X,row.Y), new Vector2(row.Z,row.Y), new Vector2(row.X,row.W), new Vector2(row.Z,row.W) })
                    Require(circle ? Vector2.Distance(point, center) < MathF.Min(size.X,size.Y) / 2 : point.X >= config.WindowPosition.X && point.Y >= config.WindowPosition.Y && point.X <= config.WindowPosition.X + size.X && point.Y <= config.WindowPosition.Y + size.Y, "Row stays inside frame");
                if (size.X < 430) {
                    Require(Valid(FilterBarDrawing.OverflowBounds), "Narrow row offers overflow");
                    var button = Center(FilterBarDrawing.OverflowBounds);
                    driver.Step(new ScriptFrame(.2, Mouse: button));
                    driver.Step(new ScriptFrame(.24, Mouse: button, Left: true));
                    driver.Step(new ScriptFrame(.28, Mouse: button, Left: false));
                    Require(FilterBarDrawing.InputActive && FilterBarDrawing.ChipBounds.All(Valid), $"Overflow rows at {size}, circle={circle}: active={FilterBarDrawing.InputActive}, rows={FilterBarDrawing.ChipBounds.Count(Valid)}, button={button}, move={driver.Window.MoveHandle.BlocksMapInput}, drag={driver.Window.Dragging}");
                }
            }
        Console.WriteLine("Quick filters: native chip mute, Ctrl/Alt solo, priority protection, right click, peek/restore, counts, custom cycling, auto-hide, and 10 real-frame chrome/shape layouts verified.");
    }

    private static void Presentation()
    {
        var json = ScriptedFrameDriver.Json;
        var saved = global::System.Text.Json.JsonSerializer.Deserialize<SystemConfig>("{}", json)!;
        Require(saved.FilterOptions.Mode == FilterBarMode.Collapsed && Enum.GetValues<MarkerGroup>().All(saved.FilterOptions.Contains), "Old configs default to collapsed with all categories available");
        saved.Filters = null!;
        Require(saved.FilterOptions.Quests, "Explicit null filter options acquire safe defaults");
        saved.FilterOptions.Mode = FilterBarMode.AlwaysExpanded; saved.FilterOptions.Services = false;
        saved = global::System.Text.Json.JsonSerializer.Deserialize<SystemConfig>(global::System.Text.Json.JsonSerializer.Serialize(saved, json), json)!;
        Require(saved.FilterOptions.Mode == FilterBarMode.AlwaysExpanded && !saved.FilterOptions.Services, "Mode and category selection round trip");
        Gratis.Windows.SettingsLayout.Reset(saved, "Filters");
        Require(saved.FilterOptions.Mode == FilterBarMode.Collapsed && saved.FilterOptions.Services && saved.ActiveLens == "Explore", "Filters section resets mode, categories and lenses");
        Require(Gratis.Windows.SettingsLayout.Find(saved, "Filters", "").Any(e => e.Path == "Filters.Mode") &&
            Gratis.Windows.SettingsLayout.Find(saved, "Filters", "").Any(e => e.Path == "CustomLenses"), "Filter/lens controls share the dedicated settings section");
        Require(FilterBarDrawing.TooltipLabel(MarkerGroup.Fates, MarkerVisibilityState.Normal) == "FATEs" &&
            FilterBarDrawing.TooltipLabel(MarkerGroup.Fates, MarkerVisibilityState.Hidden) == "FATEs \u00b7 Muted" &&
            Enum.GetValues<MarkerVisibilityState>().All(s => !FilterBarDrawing.TooltipLabel(MarkerGroup.Fates, s).Contains('\n')), "One-line tooltips contain only category and state");
        foreach (var size in new[] { new Vector2(360, 280), new Vector2(960, 640) }) {
            using var driver = new ScriptedFrameDriver(Config(size), Fixture());
            double time = 0;
            void Step(Vector2? mouse = null, bool left = false) => driver.Step(new(time += .04, Mouse: mouse ?? new(-1000), Left: left));
            void Click(Vector4 bounds) { var mouse = Center(bounds); Step(mouse); Step(mouse, true); Step(mouse); Step(mouse); }
            Step(); Step();
            Require(FilterBarDrawing.LastBounds.Z - FilterBarDrawing.LastBounds.X == GratisTheme.ControlHeight &&
                FilterBarDrawing.ChipBounds.All(b => !Valid(b)) && FilterBarDrawing.ActiveCount == 0, "Each session starts with one compact Filters button");
            Click(FilterBarDrawing.OverflowBounds);
            Require(FilterBarDrawing.PopoverOpen && FilterBarDrawing.ChipBounds.All(Valid), "Click expands all selected categories as icon chips");
            var fate = Center(FilterBarDrawing.ChipBounds[(int)MarkerGroup.Fates]);
            Step(fate); Require(FilterBarDrawing.LastTooltip == "FATEs", "Actual hover uses the short FATEs label");
            Step(fate, true); Step(fate);
            Require(FilterBarDrawing.LastTooltip == "FATEs \u00b7 Muted" && FilterBarDrawing.ActiveCount == 1, "Muted state has a compact cue and active-filter badge");
            driver.Context.Visibility.ResetFilters(); driver.Config.FilterOptions.Quests = false;
            Step();
            Require(!Valid(FilterBarDrawing.ChipBounds[(int)MarkerGroup.Quests]) && driver.Trace.DrawnMarkers.Any(m => m.IconId == 71001), "Removing a category control never hides its priority markers");
            Click(FilterBarDrawing.OverflowBounds);
            Require(!FilterBarDrawing.PopoverOpen && FilterBarDrawing.ChipBounds.All(b => !Valid(b)), "Clicking the Filters button again collapses");
            Click(FilterBarDrawing.OverflowBounds);
            Click(new(1080, 780, 1084, 784)); Step();
            Require(!FilterBarDrawing.PopoverOpen, "Clicking away collapses");
            driver.Config.FilterOptions.Mode = FilterBarMode.AlwaysExpanded; Step();
            Require(FilterBarDrawing.ChipBounds.Any(Valid) && Valid(FilterBarDrawing.SettingsBounds), "Always expanded retains an icon row and settings gear at both sizes");
            Require(!Overlap(FilterBarDrawing.LastBounds, MapToolbarDrawing.LastBounds) &&
                !Overlap(FilterBarDrawing.LastBounds, MapWindowDrawing.TitleBounds) &&
                !Overlap(FilterBarDrawing.LastBounds, MapWindowDrawing.CoordinateBounds), "Always-expanded row avoids the other anchored controls");
            var gear = FilterBarDrawing.SettingsBounds; Click(gear);
            Require(driver.Context.RequestedSettingsSection == "Filters", "Gear opens the Filters settings section");
        }
        Console.WriteLine("Filter presentation: collapsed sessions, icon popovers/rows, button-toggle/outside dismissal, short hover text, badge, category-only preferences, settings route, old/null configs and reset at small/normal sizes.");
    }
}
