using System.Numerics;
using Gratis.Controllers;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static partial class QuestLogVerification {
    private static void DetailFrames() {
        using (var driver = new ScriptedFrameDriver(Program.QuestLogConfig(1100, 700),
            new FrameScript { Width = 1100, Height = 700, QuestLog = true })) {
            var panel = driver.Context.QuestLog;
            var source = (QuestLogFixture)driver.Context.QuestSource!;
            double time = 0;
            FrameSnapshot Step(Vector2? mouse = null, bool left = false, bool right = false, bool escape = false,
                bool control = false, bool shift = false, ImGuiKey[]? keys = null, string? text = null) =>
                driver.Step(new(time += .04, Mouse: mouse ?? new(-1000), Left: left, Right: right,
                    Escape: escape, Control: control, Shift: shift, Keys: keys, Text: text));
            void Settle() { for (var i = 0; i < 12; i++) Step(); }
            void Click(Vector4 bounds, bool right = false) {
                Require(Valid(bounds), "Quest control has usable bounds");
                var mouse = Center(bounds);
                Step(mouse); Step(mouse, left: !right, right: right); Step(mouse); Step(mouse);
            }
            void Escape() { Step(escape: true); Step(); Settle(); }
            Settle();
            Require(!panel.SearchOpen && panel.SearchBounds == Vector4.Zero, "Search starts collapsed");
            var listTop = panel.ListBounds.Y;
            Step(control: true, keys: [ImGuiKey.F]); Settle();
            Require(panel.SearchOpen && Valid(panel.SearchBounds) && panel.ListBounds.Y > listTop,
                "Ctrl+F reveals search and gives it list space");
            Step(text: "Beyond");
            Require(panel.Query == "Beyond", "Revealing search focuses its field");
            Escape();
            Require(!panel.SearchOpen && panel.Query == "" && MathF.Abs(panel.ListBounds.Y - listTop) < .5f,
                $"Escape clears search and returns its space to the list (open={panel.SearchOpen}, query={panel.Query}, top={panel.ListBounds.Y}/{listTop})");
            var scenarios = source.Snapshot.Quests.Where(q => q.Bucket == QuestBucket.Scenario).ToArray();
            Require(scenarios.Select(q => q.Expansion).Distinct().Count() == 2
                && scenarios.All(q => QuestLogPanel.RowIcon(q).Handle != default
                    && QuestLogPanel.RowIcon(q) == driver.Context.GetExpansionCrest(q.ExpansionIcon))
                && QuestLogPanel.RowIcon(scenarios[0] with { ExpansionIcon = uint.MaxValue })
                    == driver.Context.GetIcon(scenarios[0].Icon),
                "Two MSQ expansions use their crest and a missing crest falls back to the game icon");
            var sideQuest = source.Snapshot.Quests.First(q => q.Bucket == QuestBucket.SideQuests);
            Require(QuestLogPanel.RowIcon(sideQuest with { ExpansionIcon = 61880 }) == driver.Context.GetIcon(sideQuest.Icon),
                "Other buckets retain the game's quest icon");

            Click(panel.Rows.Single(r => r.Id == 70964).Bounds); Settle();
            Require(panel.DetailOpen && panel.DetailQuestId == 70964 && panel.IsOpen && source.Shown == null,
                "Left click opens a summary without showing its map location");
            Require(Valid(panel.DetailBounds) && panel.DetailBounds.Z < panel.Bounds.X
                && !ChromeLayout.Overlap(panel.DetailBounds, MapToolbarDrawing.LastBounds),
                "Wide summary docks left of the log below the toolbar");
            var offset = driver.Renderer.DrawOffset;
            var scale = driver.Config.MapScale;
            var blank = new Vector2(panel.DetailBounds.X + 8, panel.DetailBounds.Y + 8);
            Step(blank); Step(blank, left: true); Step(blank + new Vector2(-30, 10), left: true);
            Step();
            Require(!driver.Window.Dragging && driver.Renderer.DrawOffset == offset && driver.Config.MapScale == scale,
                "Summary input cannot drag the map");
            Click(panel.SearchToggleBounds); Settle();
            Step(text: "Beyond");
            Escape();
            Require(!panel.SearchOpen && panel.DetailOpen && panel.IsOpen, "Escape closes search before summary");
            Escape();
            Require(!panel.DetailOpen && panel.IsOpen, "The next Escape closes summary before log");
            Click(panel.Rows.Single(r => r.Id == 70964).Bounds); Settle();
            Click(panel.Rows.Single(r => r.Id == 70964).Bounds); Settle();
            Require(!panel.DetailOpen && panel.IsOpen, "Clicking the same row toggles its summary closed");
            Click(panel.Rows.Single(r => r.Id == 70964).Bounds); Settle();
            Click(panel.Rows.Single(r => r.Id == 70001).Bounds); Settle();
            Require(panel.DetailQuestId == 70001, "Selecting another quest swaps summary content");
            Click(panel.DetailCloseBounds); Settle();
            Require(!panel.DetailOpen && panel.IsOpen, "Summary X leaves the log open");
            Click(panel.Rows.Single(r => r.Id == 70001).Bounds); Settle();
            Click(panel.Rows.Single(r => r.Id == 70001).Bounds); Settle();
            var enterFrame = Step(keys: [ImGuiKey.Enter]); Step(); Settle();
            Require(panel.DetailQuestId == 70001 && panel.IsOpen, $"Enter on a highlighted row opens its summary (pressed={enterFrame.QuestDetailId}, settled={panel.DetailQuestId}, capture={panel.CapturesKeyboard})");
            Step(keys: [ImGuiKey.M]); Step(); Settle();
            Require(source.Shown == 70001 && !panel.IsOpen, "M on a highlighted row shows its map location");
            Click(panel.Layout.Toggle); Settle();
            Click(panel.Rows.Single(r => r.Id == 70001).Bounds); Settle();
            Step(shift: true, keys: [ImGuiKey.Enter]); Step(); Settle();
            Require(source.Shown == 70001 && !panel.IsOpen, "Shift+Enter shows the highlighted quest on the map");
            Click(panel.Layout.Toggle); Settle();
            Click(panel.Rows.Single(r => r.Id == 70964).Bounds, right: true); Settle();
            Require(source.Shown == 70964 && !panel.IsOpen && driver.Renderer.Spotlight.Active,
                "Right click closes the unpinned log and begins Quest Spotlight");
            Click(panel.Layout.Toggle); Settle();
            Click(panel.Rows.Single(r => r.Id == 70001).Bounds); Settle();
            Click(panel.DetailMapBounds); Settle();
            Require(source.Shown == 70001 && !panel.IsOpen && !panel.DetailOpen, "Map button uses the spotlight action");

            Click(panel.Layout.Pin); Settle();
            Require(driver.Config.QuestLogPinned && panel.IsOpen, "Pin opens and locks the log");
            Click(panel.Rows.Single(r => r.Id == 70964).Bounds); Settle();
            Escape();
            Require(!panel.DetailOpen && panel.IsOpen, "Pinned summary still closes with Escape");
            Escape();
            driver.Step(new(time += .04, TerritoryId: 9));
            Require(panel.IsOpen && driver.Config.QuestLogPinned, "Pin survives Escape and zone changes");
            Click(panel.Rows.Single(r => r.Id == 70001).Bounds, right: true); Settle();
            Require(panel.IsOpen && source.Shown == 70001, "Showing a pinned quest keeps the log visible");
            panel.OnClose();
            driver.Config.QuestLogOpenWithMap = false;
            panel.OnOpen(driver.Config); Settle();
            Require(panel.IsOpen, "Pin overrides the initial closed policy when reopening the map");
            Click(panel.Layout.Toggle); Settle();
            Require(panel.IsOpen, "Log toggle respects the visibility lock");
            Click(panel.Layout.Pin); Settle();
            Require(!driver.Config.QuestLogPinned && panel.IsOpen, "Unpin keeps the log open until dismissed");
            Escape();
            Require(!panel.IsOpen, "Unpinned log closes with Escape");
        }
        foreach (var mode in Enum.GetValues<InterfaceAnimationMode>())
            foreach (var circle in new[] { false, true }) {
                var config = Program.QuestLogConfig(540, 1030);
                config.InterfaceAnimations = mode;
                config.MapCircle = circle;
                using var driver = new ScriptedFrameDriver(config,
                    new FrameScript { Width = 540, Height = 1030, QuestLog = true });
                double time = 0;
                void Settle() { for (var i = 0; i < 12; i++) driver.Step(new(time += .04, Left: false)); }
                Settle();
                var panel = driver.Context.QuestLog;
                foreach (var anchor in Enum.GetValues<ChromeAnchor>()) {
                    driver.Config.ToolbarAnchor = anchor;
                    driver.Step(new(time += .04));
                    Require(panel.Rows.Count > 0, $"Narrow quest rows remain usable ({mode}, circle={circle}, anchor={anchor}, panel={panel.Bounds}, list={panel.ListBounds})");
                    var row = Center(panel.Rows.First().Bounds);
                    driver.Step(new(time += .04, Mouse: row, Left: true));
                    driver.Step(new(time += .04, Mouse: row, Left: false)); Settle();
                    Require(panel.DetailOpen && panel.Layout.DetailOverlay && panel.DetailBounds == panel.Bounds
                        && panel.Rows.Count == 0 && Valid(MapToolbarDrawing.LastBounds)
                        && !ChromeLayout.Overlap(panel.DetailBounds, MapToolbarDrawing.LastBounds),
                        "Narrow summary overlays the list below every toolbar anchor in every motion mode");
                    driver.Step(new(time += .04, Escape: true));
                    driver.Step(new(time += .04)); Settle();
                    Require(!panel.DetailOpen && panel.IsOpen && panel.Rows.Count > 0,
                        "Escape from a narrow summary returns to the quest list");
                }
                var selectedRow = Center(panel.Rows.First().Bounds);
                driver.Step(new(time += .04, Mouse: selectedRow, Left: true));
                driver.Step(new(time += .04, Mouse: selectedRow, Left: false)); Settle();
                Require(panel.DetailOpen && panel.CapturesKeyboard, "Narrow summary retains isolated keyboard input");
                driver.Step(new(time += .04, Keys: [ImGuiKey.M])); Settle();
                Require(!panel.IsOpen && !panel.DetailOpen && driver.Renderer.Spotlight.Active,
                    "M from a narrow summary shows its quest on the map");
            }
        ScenarioFrames();
        BottomCardFrames();
    }

    private static void ScenarioFrames() {
        foreach (var mode in Enum.GetValues<InterfaceAnimationMode>()) {
            var config = Program.QuestLogConfig(1100, 700);
            config.InterfaceAnimations = mode;
            using var driver = new ScriptedFrameDriver(config,
                new FrameScript { Width = 1100, Height = 700, QuestLog = true });
            var source = (QuestLogFixture)driver.Context.QuestSource!;
            source.Snapshot = source.Snapshot with { Quests = source.Snapshot.Quests.Reverse().ToArray() };
            for (var i = 0; i < 12; i++) driver.Step(new(i * .04));
            var panel = driver.Context.QuestLog;
            var bounds = panel.Rows.Single(r => r.Id == 70964).Bounds;
            var opacity = panel.ScenarioRimOpacity;
            var listTop = panel.ListBounds.Y;
            Require(panel.Rows.First().Id == 70964 && panel.ScenarioRimQuestId == 70964,
                "The current MSQ sorts first under Scenario and owns the only gold rim");
            Require(Valid(panel.ProgressBounds) && MathF.Abs(panel.ProgressBounds.W - panel.ProgressBounds.Y
                    - 42 * GratisTheme.ChromeScale) < .5f,
                "Header contains only the compact expansion progress block");
            driver.Step(new(1.25));
            Require(panel.Rows.Single(r => r.Id == 70964).Bounds == bounds,
                "The current MSQ rim never changes row geometry");
            if (mode == InterfaceAnimationMode.Full)
                Require(MathF.Abs(opacity - panel.ScenarioRimOpacity) > .02f
                    && QuestLogPanel.ScenarioRimAlpha(1.25, true) == QuestLogPanel.ScenarioRimAlpha(6.25, true),
                    "The faint full-motion rim breathes on a five-second period");
            else
                Require(opacity == .12f && panel.ScenarioRimOpacity == opacity,
                    "Reduced and Off motion retain a static faint rim");
            source.Snapshot = source.Snapshot with { Revision = 2, CurrentScenario = "", Progress = null };
            driver.Step(new(1.29));
            Require(panel.ProgressBounds == Vector4.Zero && panel.ScenarioRimQuestId == 0
                && panel.ListBounds.Y < listTop - 40 * GratisTheme.ChromeScale,
                "Missing MSQ progress leaves no header gap or glowing quest");
        }
    }

    private static void BottomCardFrames() {
        foreach (var width in new[] { 1100, 540 }) {
            var config = Program.QuestLogConfig(width, 800);
            config.QuestLogOpenWithMap = false;
            config.ShowTitleOnMap = config.CompactTitle = true;
            config.LocationAnchor = config.CoordinateAnchor = ChromeAnchor.BottomCenter;
            using var driver = new ScriptedFrameDriver(config,
                new FrameScript { Width = width, Height = 800, QuestLog = true });
            var panel = driver.Context.QuestLog;
            double time = 0;
            void Settle() { for (var i = 0; i < 12; i++) driver.Step(new(time += .04)); }
            Settle();
            var location = MapWindowDrawing.TitleBounds;
            var coordinates = MapWindowDrawing.CoordinateBounds;
            var player = CoordinateCopy.PlayerBounds;
            var cursor = CoordinateCopy.CursorBounds;
            Require(Valid(location) && Valid(coordinates), "Bottom cards are present before the log opens");
            void Check(bool detail) {
                Require(MapWindowDrawing.TitleBounds == location && MapWindowDrawing.CoordinateBounds == coordinates
                    && CoordinateCopy.PlayerBounds == player && CoordinateCopy.CursorBounds == cursor,
                    "Location and both coordinate containers keep identical bounds with log closed, open and showing detail");
                foreach (var card in new[] { location, coordinates, player, cursor })
                    Require(!ChromeLayout.Overlap(panel.Bounds, card)
                        && (!detail || !ChromeLayout.Overlap(panel.DetailBounds, card)),
                        "Quest panels end above the bottom-card row");
                Require(!ChromeLayout.Overlap(panel.Bounds, MapToolbarDrawing.LastBounds)
                    && (!detail || !ChromeLayout.Overlap(panel.DetailBounds, MapToolbarDrawing.LastBounds)),
                    "Quest panels also clear the toolbar");
            }
            panel.Toggle(); Settle(); Check(false);
            Require(panel.Rows.Count > 0, $"Bottom-card layout leaves usable quest rows ({width}, panel={panel.Bounds}, list={panel.ListBounds})");
            var row = Center(panel.Rows.First().Bounds);
            driver.Step(new(time += .04, Mouse: row, Left: true));
            driver.Step(new(time += .04, Mouse: row, Left: false)); Settle();
            Require(panel.DetailOpen, "The summary opens beside fixed bottom cards");
            Check(true);
        }
    }
}
