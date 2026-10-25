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
}
