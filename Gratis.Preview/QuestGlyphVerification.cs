using System.Numerics;
using Gratis.Controllers;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class QuestGlyphVerification {
    private static void Require(bool value, string message) => MarkerVisibilityVerification.Require(value, message);

    public static unsafe void Run() {
        var directory = Path.GetFullPath("_artifacts/work/glyphs");
        Directory.CreateDirectory(directory);
        using var driver = new ScriptedFrameDriver(Program.QuestLogConfig(1300, 800),
            new FrameScript { Width = 1300, Height = 800, QuestLog = true, Palette = true });
        var fixture = (QuestLogFixture)driver.Context.QuestSource!;
        var quests = QuestGlyphFixture.Completed();
        fixture.Snapshot = fixture.Snapshot with { Revision = 2, Completed = quests };
        var chars = string.Concat(quests.Select(q => q.Name)).Where(c => !char.IsWhiteSpace(c)).Distinct().ToArray();
        double time = 0;
        void Step(Vector2? mouse = null, bool down = false, ImGuiKey[]? keys = null, string? text = null) =>
            driver.Step(new(time += .04, Mouse: mouse ?? new(-1000), Left: down, Keys: keys, Text: text));
        void Settle() { for (var i = 0; i < 16; i++) Step(); }
        void Click(Vector4 bounds) {
            Require(bounds.Z > bounds.X && bounds.W > bounds.Y, "Glyph fixture control must be visible");
            var mouse = new Vector2((bounds.X + bounds.Z) / 2, (bounds.Y + bounds.W) / 2);
            Step(mouse); Step(mouse, true); Step(mouse); Settle();
        }
        Settle();
        ImGui.NewFrame();
        ImGui.Begin("Glyph verification");
        foreach (var size in TextDrawing.Sizes)
            foreach (var role in Enum.GetValues<ChromeFontRole>())
                using (driver.Context.GetChromeFont(size, role)!.Push()) {
                    var font = ImGui.GetFont();
                    foreach (var c in chars) {
                        var glyph = font.FindGlyphNoFallback(c);
                        Require(glyph.NativePtr != null, $"U+{(int)c:X4} must exist at {size}px ({role})");
                        Require(glyph.NativePtr != font.FindGlyph('?').NativePtr,
                            $"U+{(int)c:X4} must not resolve to '?' at {size}px ({role})");
                        Require(DrawBinding.TryGlyphBounds(font, c, out var ink) && ink.Z > ink.X && ink.W > ink.Y,
                            $"U+{(int)c:X4} must have visible ink at {size}px ({role})");
                    }
                    Require(font.FindGlyphNoFallback('§').NativePtr != font.FindGlyphNoFallback('S').NativePtr,
                        "The literal section sign must retain its own glyph");
                }
        ImGui.End();
        ImGui.Render();
        var count = driver.Context.TextBudget.Count;
        Require(count == 7 && driver.Context.TextBudget.Denied == 0, "Fallbacks must share seven font handles");
        Require(!QuestLogText.MoreConditions(1, 1, false) && !QuestLogText.MoreConditions(2, 1, false)
            && QuestLogText.MoreConditions(2, 2, false) && QuestLogText.MoreConditions(0, 2, true)
            && !QuestLogText.MoreConditions(0, 2, false),
            "Only branch joins or populated locks need the additional-conditions note");

        var panel = driver.Context.QuestLog;
        Click(panel.TabBounds[1]);
        Require(panel.Results.Count == quests.Length, "Completed fixtures retain every literal quest name");
        driver.Save(Path.Combine(directory, "completed.png"));
        foreach (var id in new uint[] { 69996, 69995, 69989 }) {
            Click(panel.Rows.Single(r => r.Id == id).Bounds);
            Require(panel.DetailQuestId == id, "Real cypher title must open its summary");
            driver.Save(Path.Combine(directory, $"detail-{id}.png"));
            Click(panel.DetailCloseBounds);
        }
        fixture.Snapshot = fixture.Snapshot with { Revision = 3, Completed = [quests[3] with {
            Prerequisites = QuestGlyphFixture.Victory + "\n" + QuestGlyphFixture.Knowledge, MoreConditions = true,
        }] };
        Settle();
        Click(panel.Rows.Single().Bounds);
        driver.Save(Path.Combine(directory, "requires-branch.png"));
        Click(panel.DetailCloseBounds);
        driver.PlaceSource!.AdditionalPlaces.AddRange(quests.Select(q =>
            new PlaceEntry("glyph:" + q.RowId, PlaceKind.Quest, q.Name, "Quest glyph fixture", 1, 1, new(40)) {
                QuestRowId = q.RowId, QuestBucket = QuestBucket.Scenario,
            }));
        driver.Palette!.Open("No");
        Settle();
        driver.Save(Path.Combine(directory, "palette.png"));
        Require(driver.Context.TextBudget.Count == count && driver.Context.TextBudget.Denied == 0,
            "Opening names, prerequisites and palette must not allocate or rebuild fonts");
        File.WriteAllLines(Path.Combine(directory, "fixture-codepoints.txt"), quests.Select(q =>
            $"{q.RowId}: {q.Name}\n" + string.Join(" ", q.Name.EnumerateRunes().Select(r => $"U+{r.Value:X4}"))));
        Console.WriteLine($"Quest glyphs: {chars.Length} literal glyphs in all seven sizes and roles, "
            + "no fallback '?', shared handles, Requires/branch rules; PNGs: " + directory);
    }
}
