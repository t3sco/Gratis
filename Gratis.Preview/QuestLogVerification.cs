using System.Numerics;
using System.Text.Json;
using Gratis.Compat.Configuration;
using Gratis.Controllers;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;
using ImGuiNET;

namespace Gratis.Preview;

internal static partial class QuestLogVerification {
    private static void Require(bool value, string message) => MarkerVisibilityVerification.Require(value, message);
    private static Vector2 Center(Vector4 b) => new((b.X + b.Z) / 2, (b.Y + b.W) / 2);
    private static bool Valid(Vector4 b) => b.Z > b.X && b.W > b.Y;

    public static void Model() {
        SummaryModel();
        ObjectiveText();
        var json = Configuration.SerializerOptions;
        var legacy = JsonSerializer.Deserialize<SystemConfig>("{\"CenterOnQuest\":false}", json)!;
        Require(!legacy.QuestLogPinned && !legacy.QuestLogOpenWithMap && legacy.QuestLogWidth == 320 && !legacy.CenterOnQuest,
            "Old configs retain quest-centering and default to a closed log at 320 units");
        legacy.QuestLogWidth = float.NaN;
        Require(legacy.EffectiveQuestLogWidth == 320, "Invalid width resolves safely");
        legacy.QuestLogWidth = 400;
        legacy.QuestLogOpenWithMap = true;
        legacy.QuestLogPinned = true;
        var restored = JsonSerializer.Deserialize<SystemConfig>(JsonSerializer.Serialize(legacy, json), json)!;
        Require(restored.QuestLogPinned && restored.QuestLogOpenWithMap && restored.QuestLogWidth == 400, "Quest settings round trip");
        var position = restored.WindowPosition;
        var lens = restored.ActiveLens;
        restored.ResetQuestLog();
        Require(!restored.QuestLogPinned && !restored.QuestLogOpenWithMap && restored.QuestLogWidth == 320
            && restored.WindowPosition == position && restored.ActiveLens == lens && !restored.CenterOnQuest,
            "Quest reset preserves position, lens and legacy centering");
        foreach (var path in new[] { "QuestLogOpenWithMap", "QuestLogPinned", "QuestLogWidth" }) {
            var entry = SettingsLayout.Entries.Single(e => e.Path == path);
            Require(entry.Visible(restored), "Quest setting is visible in Gratis");
            restored.VisualStyle = 1;
            Require(!entry.Visible(restored), "Quest setting is hidden in Classic");
            restored.VisualStyle = 0;
        }
        Require(new QuestLogProgress(5, "Dawntrail", 61880, 127, 143).Percentage == 89,
            "Progress is the rounded raw expansion fraction");
        Require(QuestLogModel.ScenarioCategories(0).SequenceEqual([1u, 2u])
            && QuestLogModel.ScenarioCategories(5).SequenceEqual([13u, 14u, 15u]),
            "MSQ sets include expansion base story and patches");
        SettingsLayout.Reset(legacy, "Map");
        Require(!legacy.QuestLogPinned && !legacy.QuestLogOpenWithMap && legacy.QuestLogWidth == 320, "Map settings reset includes quest log");
    }

    private static void ObjectiveText() {
        Require(QuestLogIcon.Accepted(71000, 6, 1) == 71003
            && QuestLogIcon.Accepted(71020, 6, 3) == 71023
            && QuestLogIcon.Accepted(71140, 6, 3) == 71143
            && QuestLogIcon.Accepted(71140, 6, 6) == 71143
            && QuestLogIcon.Accepted(71140, 6, 2) == 71143
            && QuestLogIcon.Accepted(71020, 6, 7) == 71023
            && QuestLogIcon.Accepted(71060, 6, null) == 71063,
            "EventIconType families preserve MSQ, side, feature, job, chronicles, repeatable and leve icons");
        Require(QuestLogIcon.Accepted(71140, 6, 1) == 71143
            && QuestLogIcon.Accepted(0, 0, 1) == 71003
            && QuestLogIcon.Accepted(0, 0, 6) == 71143
            && QuestLogIcon.Accepted(0, 0, 3) == 71023
            && QuestLogIcon.Accepted(0, 0, null) == 71023
            && QuestLogIcon.Accepted(61731, 1, 3) == 61731,
            "Quest icon metadata wins over category, and missing or single-icon families stay usable");
        Require(QuestLogText.SheetName("HeaVny002_02112") == "quest/021/HeaVny002_02112"
            && QuestLogText.SheetName("ClsArc000_00021") == "quest/000/ClsArc000_00021"
            && QuestLogText.SheetName("missing") == "" && QuestLogText.SheetName("bad_abc") == "",
            "Quest text paths use the numeric suffix, including leading zeroes");
        byte[] reap = [1, 2, 255, 0, 0];
        string[] text = ["Speak with Tonu Vanu.", "Enter Neverreap.", "Deliver the waukkeon scale."];
        Require(QuestLogText.ActiveTodos(reap, 1).Select(i => text[i]).Single() == "Speak with Tonu Vanu."
            && QuestLogText.ActiveTodos(reap, 2).SequenceEqual([1])
            && QuestLogText.ActiveTodos(reap, 255).SequenceEqual([2]),
            "Untracked Reap What You Sow resolves the current Journal objective through the final stage");
        Require(QuestLogText.ActiveTodos([1, 3, 3, 255, 0], 2).SequenceEqual([1, 2])
            && QuestLogText.ActiveTodos([1, 3, 3, 255, 0], 3).SequenceEqual([1, 2])
            && QuestLogText.ActiveTodos([1, 3, 3, 255, 0], 4).SequenceEqual([3])
            && QuestLogText.ActiveTodos([0, 0], 0).Length == 0
            && QuestLogText.ActiveTodos([1, 2, 0], 3).Length == 0,
            "Skipped sequences, parallel objectives and missing TODOs preserve stage boundaries");
        Require(QuestLogText.DescriptionIndex(reap, 1) == 1
            && QuestLogText.DescriptionIndex(reap, 255) == 3
            && QuestLogText.DescriptionIndex([1, 3, 3, 255], 3) == 2,
            "Journal paragraphs follow distinct stages, including the final sequence");
        Require(QuestLogText.PreferTracked("Slay enemies.", "Speak with Tonu Vanu.") == "Slay enemies."
            && QuestLogText.PreferTracked(" ", "Speak with Tonu Vanu.") == "Speak with Tonu Vanu."
            && QuestLogText.PreferTracked(null, null) == "",
            "Tracked progress wins and missing text falls back safely");
        Require(QuestLogText.BaseExperience(1000, 65000, 100) == 65000000
            && QuestLogText.BaseExperience(0, 65000, 100) == 0,
            "Base EXP uses a wide intermediate and preserves quests without EXP");
    }

    public static void Frames() {
        using (var driver = new ScriptedFrameDriver(Program.QuestLogConfig(1100, 700),
            new FrameScript { Width = 1100, Height = 700, QuestLog = true, Palette = true })) {
            var panel = driver.Context.QuestLog;
            var source = (QuestLogFixture)driver.Context.QuestSource!;
            double time = 0;
            FrameSnapshot Step(Vector2? mouse = null, bool left = false, bool escape = false, string? text = null,
                float wheel = 0, bool control = false, ImGuiKey[]? keys = null, bool alt = false, bool shift = false) =>
                driver.Step(new(time += .04, Mouse: mouse ?? new(-1000), Left: left, Escape: escape,
                    Text: text, Wheel: wheel, Control: control, Keys: keys, Alt: alt, Shift: shift));
            void Settle() { for (var i = 0; i < 10; i++) Step(); }
            void Click(Vector4 b) {
                Require(Valid(b), "Quest control has usable bounds");
                var mouse = Center(b);
                Step(mouse); Step(mouse, true); Step(mouse); Step(mouse);
            }
            void Search(string text) {
                if (!panel.SearchOpen) {
                    Click(panel.SearchToggleBounds);
                    Settle();
                }
                Click(panel.SearchBounds);
                Step(Center(panel.SearchBounds), control: true, keys: [ImGuiKey.A]);
                Step(Center(panel.SearchBounds), text: text.Length > 0 ? text : null,
                    keys: text.Length == 0 ? [ImGuiKey.Backspace] : null);
                Step();
            }
            Settle();
            Require(panel.IsOpen && panel.Results.Count == 8 && source.Snapshot.Progress?.Percentage == 89,
                "Open policy shows fixture accepted quests and 89% progress");
            Require(panel.Results.Select(q => q.Bucket).Distinct().Count() == 6,
                "All six palette quest buckets are represented");
            Require(!ChromeLayout.Overlap(panel.Bounds, MapToolbarDrawing.LastBounds), "Panel never covers toolbar");
            RowLayout(panel, source.Snapshot);
            var viewport = driver.Renderer.ViewportSize;
            var offset = driver.Renderer.DrawOffset;
            var scale = driver.Config.MapScale;
            var blank = new Vector2(panel.Bounds.X + 6, panel.Bounds.Y + 12);
            Step(blank); Step(blank, true); Step(blank + new Vector2(-24, 12), true, wheel: -1, alt: true, shift: true);
            Step(blank);
            Require(!driver.Window.Dragging && !driver.Window.MoveHandle.Dragging
                && driver.Renderer.DrawOffset == offset && driver.Config.MapScale == scale,
                "Header drag, wheel, Alt and Shift cannot reach the map or window move gesture");
            Click(panel.Sections[QuestBucket.Scenario]);
            Require(!panel.Rows.Any(r => r.Id == 70964), "Section collapse hides its quest row");
            Search("Beyond");
            Require(panel.Query == "Beyond" && panel.Results.Count == 1 && panel.Rows.Any(r => r.Id == 70964),
                "Search temporarily reveals matching quests in collapsed sections");
            Search("");
            Require(!panel.Rows.Any(r => r.Id == 70964), "Clearing search restores section disclosure");
            Search("dramaturge");
            Require(panel.Results.Count == 1 && panel.Results[0].Bucket == QuestBucket.Chronicles,
                "Search includes current objective text");
            Search("Beyond");
            Click(panel.Rows.Single(r => r.Id == 70964).Bounds);
            Settle();
            Require(panel.DetailOpen && panel.DetailQuestId == 70964 && source.Shown == null,
                "Left click opens a quest summary without starting Spotlight");
            Click(panel.DetailMapBounds);
            Require(source.Shown == 70964 && !panel.IsOpen && driver.Renderer.Spotlight.Active
                && driver.Renderer.Spotlight.Request?.Source == SpotlightSource.Quest,
                "Quest selection closes the panel and begins Quest Spotlight");
            Require(driver.Renderer.ViewportSize == viewport, "Panel never changes map viewport dimensions");
            Click(panel.Layout.Toggle);
            Step(escape: true);
            Require(!panel.IsOpen && panel.CapturesKeyboard && driver.Renderer.Spotlight.Active,
                "Escape closes the log without also ending Spotlight");
            Step(escape: true);
            Require(panel.CapturesKeyboard, "Held Escape remains consumed until release");
            Step();
            driver.Config.QuestLogOpenWithMap = false;
            panel.OnClose(); panel.OnOpen(driver.Config); Step();
            Require(!panel.IsOpen, "Reopening obeys the closed default rather than remembering open state");
            driver.Config.QuestLogOpenWithMap = true;
            panel.OnOpen(driver.Config); Settle();
            source.Snapshot = new(2, QuestLogStatus.Loading, [], Character: 1);
            Step();
            Require(panel.Results.Count == 0, "Loading snapshot clears stale quest rows");
            source.Snapshot = new(3, QuestLogStatus.Ready, [], Character: 1);
            Step();
            Require(panel.IsOpen && panel.Results.Count == 0, "Empty accepted list keeps a usable panel");
            driver.Palette!.Open(); Step();
            Require(panel.Bounds == Vector4.Zero && !panel.PointerOver, "Place search suspends the panel");
            driver.Palette.Close(false); Step();
            Require(panel.IsOpen && Valid(panel.Bounds), "Closing place search restores logical panel state");
            source.Snapshot = source.Snapshot with { Revision = 4, Character = 2 };
            Step();
            Require(!panel.IsOpen && !panel.CapturesKeyboard, "Character changes discard input and panel state");
        }
        foreach (var mode in Enum.GetValues<InterfaceAnimationMode>())
            foreach (var circle in new[] { false, true }) {
                var config = Program.QuestLogConfig(540, 1030);
                config.InterfaceAnimations = mode;
                config.MapCircle = circle;
                using var driver = new ScriptedFrameDriver(config,
                    new FrameScript { Width = 540, Height = 1030, QuestLog = true });
                for (var i = 0; i < 12; i++) driver.Step(new(i * .04));
                var panel = driver.Context.QuestLog;
                Require(panel.IsOpen && panel.Opacity > .99f && panel.Layout.Compact && Valid(panel.Bounds)
                    && !ChromeLayout.Overlap(panel.Bounds, MapToolbarDrawing.LastBounds),
                    "Narrow and circular logs settle in every motion mode below the toolbar");
                RowLayout(panel, driver.Context.QuestSource!.Snapshot);
                if (circle) {
                    var center = config.WindowPosition + config.WindowSize / 2;
                    var radius = MathF.Min(config.WindowSize.X, config.WindowSize.Y) / 2;
                    Require(Vector2.Distance(center, new(panel.Bounds.X, panel.Bounds.Y)) < radius
                        && Vector2.Distance(center, new(panel.Bounds.Z, panel.Bounds.W)) < radius,
                        "Circle panel stays in the inscribed safe region");
                }
                var time = .48;
                foreach (var anchor in Enum.GetValues<ChromeAnchor>()) {
                    driver.Config.ToolbarAnchor = anchor;
                    driver.Step(new(time += .04));
                    Require(Valid(MapToolbarDrawing.LastBounds)
                        && !ChromeLayout.Overlap(panel.Bounds, MapToolbarDrawing.LastBounds),
                        "Every toolbar anchor remains accessible above a compact panel");
                }
                driver.Config.VisualStyle = 1;
                driver.Step(new(time += .04));
                Require(!panel.IsOpen && !panel.InputActive && !panel.CapturesKeyboard && panel.Rows.Count == 0,
                    "Classic has no quest widgets or capture even when open-with-map is enabled");
            }
        DetailFrames();
        SummaryFrames();
        ClassicPixels();
        Console.WriteLine("Quest Log: accepted fixtures, progress, collapse/search, input, Spotlight, lifecycle, "
            + "six anchors, circle/narrow, Full/Reduced/Off and Classic pixels passed.");
    }

    private static void ClassicPixels() {
        var directory = Path.GetFullPath("_artifacts/work/questlog-detail/classic");
        Directory.CreateDirectory(directory);
        string Render(bool source) {
            var config = Program.QuestLogConfig(800, 600);
            config.VisualStyle = 1;
            config.QuestLogOpenWithMap = source;
            using var driver = new ScriptedFrameDriver(config,
                new FrameScript { Width = 800, Height = 600, QuestLog = source });
            for (var i = 0; i < 12; i++) driver.Step(new(i * .04));
            var path = Path.Combine(directory, source ? "classic-with-quest-settings.png" : "classic-control.png");
            driver.Save(path);
            return Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
        }
        Require(Render(false) == Render(true), "Classic PNG bytes match with quest source and settings enabled");
    }

    private static void RowLayout(QuestLogPanel panel, QuestLogSnapshot snapshot) {
        var s = GratisTheme.ChromeScale;
        var gap = GratisTheme.Gap * s;
        Require(panel.Bounds.Z - panel.ListBounds.Z >= 16 * s - .5f,
            "The scrolling viewport leaves a right inset inside the panel");
        foreach (var section in panel.Sections) {
            var ids = snapshot.Quests.Where(q => q.Bucket == section.Key).Select(q => q.RowId).ToArray();
            var first = panel.Rows.FirstOrDefault(r => r.Id == ids[0]);
            var expectedTop = section.Value.W + gap;
            if (expectedTop + 52 * s <= panel.ListBounds.W) {
                Require(first.Id != 0 && MathF.Abs(first.Bounds.Y - expectedTop) < .5f
                    && MathF.Abs(first.Bounds.W - first.Bounds.Y - 52 * s) < .5f,
                    "The first row clears its header and retains its full height");
            }
            var visible = panel.Rows.Where(r => ids.Contains(r.Id)).ToArray();
            foreach (var row in visible)
                Require(row.Bounds.Z <= panel.ListBounds.Z - 6 * s,
                    "Rows and headers leave space beside the scrollbar");
            for (var i = 1; i < visible.Length; i++)
                Require(MathF.Abs(visible[i].Bounds.Y - visible[i - 1].Bounds.W - gap) < .5f,
                    "Consecutive quest rows have a consistent eight-unit gap");
        }
    }
}
