using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class SegmentedChipVerification {
    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException("Segmented chips: " + message);
    }

    public static void Run() {
        var regular = new ChipProfile {
            Height = 36, WidthPadding = 44, IconInset = 14, IconSize = 13, TextInset = 28,
            Fill = new(1, 1, 1, .035f), SelectedAlpha = .25f,
            Ink = new(237 / 255f, 237 / 255f, 237 / 255f, 1),
        };
        Require(ChipFaces.Regular == regular, "Regular literals changed.");
        Require(ChipFaces.Compact == regular with {
            Height = 30, WidthPadding = 20, TextInset = 0, FitInset = 12,
        }, "Compact literals changed.");
        var cache = new ChipLabelCache();
        Require(cache.Label("All", 0) == "All" && cache.Label("All", 12) == "All 12"
            && cache.Label("Quests", 12) == "Quests 12" && cache.Label("Quests", 0) == "Quests",
            "Counted label invalidation changed.");
        Verify(InterfaceAnimationMode.Off, 1);
        Verify(InterfaceAnimationMode.Full, 1);
        Verify(InterfaceAnimationMode.Off, 1.25f);
    }

    private static void Verify(InterfaceAnimationMode animations, float scale) {
        var config = new SystemConfig { AcceptedSpoilerWarning = true, VisualStyle = 0,
            InterfaceAnimations = animations, ChromeScale = scale, WindowSize = new(600, 700) };
        using var driver = new ScriptedFrameDriver(config, new FrameScript { Width = 800, Height = 900 });
        driver.BenchmarkMapVisible = false;
        string[] labels = ["All", "Aetherytes", "All", "", "All", "Allied societies", "Other", "Side quests"];
        string[] ids = ["##scope-0", "##scope-1", "##scope-2", "##scope-3",
            "##quest-bucket-0", "##quest-bucket-1", "##quest-bucket-2", "##quest-bucket-3"];
        var options = new ChipOptions[labels.Length];
        for (var i = 0; i < options.Length; i++)
            options[i] = new(i < 4 ? ChipFaces.Regular : ChipFaces.Compact, GratisTheme.Crystal,
                new(i == 5 ? 60 : 160, (i < 4 ? 36 : 30) * scale), scale,
                Icon: i == 0 || i == 2 || i >= 4 ? '\0' : GratisIcons.Sparkles,
                Centered: i is 2 or 3, Tooltip: i == 3 ? "Quests" : null, LabelCache: new());
        var vertices = new ImDrawVert[labels.Length][];
        var indices = new ushort[labels.Length][];
        var bounds = new Vector4[labels.Length];
        var allocated = new long[labels.Length];
        var original = true;
        var capture = false;
        var compare = false;
        var measure = false;
        var focusNext = false;
        var focused = false;
        var clicked = false;
        long bytes = 0;
        driver.DrawPreviewOverlay = () => {
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(new(600, 700));
            if (focusNext) ImGui.SetNextWindowFocus();
            using var font = driver.Context.GetChromeFont(14 * scale, ChromeFontRole.Chip)?.Push();
            ImGui.Begin("Segmented chip verification", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDecoration);
            for (var i = 0; i < options.Length; i++) {
                ImGui.SetCursorScreenPos(new(24, 24 + i * 64));
                if (i == 0 && focusNext) ImGui.SetKeyboardFocusHere();
                ImGui.BeginDisabled(i == 3);
                var alpha = ImGui.GetStyle().Alpha;
                var list = ImGui.GetWindowDrawList();
                var firstVertex = list.VtxBuffer.Size;
                var firstIndex = list.IdxBuffer.Size;
                var before = GC.GetAllocatedBytesForCurrentThread();
                bool activated;
                if (original) {
                    var id = (i < 4 ? "##scope-" : "##quest-bucket-") + (i < 4 ? i : i - 4);
                    var label = i < 4 ? labels[i] : labels[i] + $" {12}";
                    activated = Original(id, label, i % 2 == 0, options[i]);
                }
                else {
                    var label = i < 4 ? labels[i] : options[i].LabelCache!.Label(labels[i], 12);
                    activated = SegmentedChip.Draw(ids[i], label, i % 2 == 0, options[i]);
                }
                if (measure) {
                    var delta = GC.GetAllocatedBytesForCurrentThread() - before;
                    bytes += delta;
                    if (!original) allocated[i] += delta;
                }
                Require(ImGui.GetStyle().Alpha == alpha, "Inherited disabled alpha changed.");
                Require(i != 3 || !activated, "Disabled chip activated.");
                if (i == 0) { focused = ImGui.IsItemFocused(); clicked |= activated; }
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                var expectedId = ImGui.GetID(ids[i]);
                Require(UiMotion.Item == expectedId, "Native ID changed.");
                Require(UiMotion.Store.Contains(new(expectedId, 3)) == !UiMotion.Off, "Press motion changed.");
                Require(UiMotion.Store.Contains(new(expectedId, 7)) == !UiMotion.Off, "Focus feedback changed.");
                if (capture) {
                    bounds[i] = new(min.X, min.Y, max.X, max.Y);
                    vertices[i] = new ImDrawVert[list.VtxBuffer.Size - firstVertex];
                    indices[i] = new ushort[list.IdxBuffer.Size - firstIndex];
                    for (var v = 0; v < vertices[i].Length; v++) {
                        var vertex = list.VtxBuffer[firstVertex + v];
                        vertices[i][v] = new() { pos = vertex.pos, uv = vertex.uv, col = vertex.col };
                    }
                    for (var n = 0; n < indices[i].Length; n++) indices[i][n] = list.IdxBuffer[firstIndex + n];
                }
                if (compare) {
                    Require(bounds[i] == new Vector4(min.X, min.Y, max.X, max.Y), "Native bounds changed.");
                    Require(vertices[i].Length == list.VtxBuffer.Size - firstVertex
                        && indices[i].Length == list.IdxBuffer.Size - firstIndex, "Draw geometry changed.");
                    for (var v = 0; v < vertices[i].Length; v++) {
                        var expected = vertices[i][v];
                        var actual = list.VtxBuffer[firstVertex + v];
                        Require(expected.pos == actual.pos && expected.uv == actual.uv && expected.col == actual.col,
                            "Original vertex changed.");
                    }
                    for (var n = 0; n < indices[i].Length; n++)
                        Require(indices[i][n] == list.IdxBuffer[firstIndex + n], "Original index changed.");
                }
                ImGui.EndDisabled();
            }
            ImGui.End();
        };
        var time = 0d;
        void Step() => driver.Step(new(time += .02), false);
        for (var frame = 0; frame < 64; frame++) Step();
        capture = true;
        Step();
        capture = false;
        measure = true;
        for (var frame = 0; frame < 256; frame++) Step();
        var originalBytes = bytes;
        measure = false;
        original = false;
        for (var frame = 0; frame < 64; frame++) Step();
        compare = animations == InterfaceAnimationMode.Off;
        Step();
        compare = false;
        measure = true;
        bytes = 0;
        for (var frame = 0; frame < 256; frame++) Step();
        Require(bytes == 0, $"steady Draw path allocated {bytes} B ({string.Join(", ", allocated)} by chip).");
        measure = false;
        ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        focusNext = true;
        Step();
        focusNext = false;
        Step();
        Require(focused, "Native keyboard focus changed.");
        var hover = new Vector2(40, 35);
        driver.Step(new(time += .02, Mouse: hover), false);
        driver.Step(new(time += .5, Mouse: hover), false);
        Require(ChromeControls.LastTooltipAnchor == bounds[0],
            "Tooltip anchor changed.");
        driver.Step(new(time += .02, Mouse: hover, Left: true), false);
        driver.Step(new(time += .02, Mouse: hover, Left: false), false);
        Require(clicked, "Native activation changed.");
        Console.WriteLine($"Segmented chips: {animations}, scale {scale}: original {originalBytes} B "
            + $"-> {bytes} B / 256 frames; native IDs/bounds, geometry, focus, disabled state and tooltip passed.");
    }

    private static bool Original(string id, string label, bool selected, in ChipOptions options) {
        var s = options.Scale;
        var chosen = UiMotion.Value(ImGui.GetID(id), 6, selected ? 1 : 0);
        ImGui.PushStyleColor(ImGuiCol.Button,
            Vector4.Lerp(new Vector4(1, 1, 1, .035f), options.Accent with { W = .25f }, chosen));
        var clicked = ChromeControls.TextButton(id, options.Size);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var list = ImGui.GetWindowDrawList();
        if (options.Icon != '\0')
            GratisIcons.Draw(list, new(options.Centered ? (min.X + max.X) / 2 : min.X + 14 * s,
                (min.Y + max.Y) / 2), options.Icon, GratisTheme.Primary, 13 * s);
        var fitted = options.Profile == ChipFaces.Compact;
        var text = fitted ? ChromeControls.FitLabel(label, MathF.Max(1, options.Size.X - 12 * s)) : label;
        var textSize = TextDrawing.Measure(text);
        TextDrawing.Draw(list, fitted ? (min + max - textSize) / 2
            : new(options.Centered ? (min.X + max.X - textSize.X) / 2 : min.X + 28 * s,
                (min.Y + max.Y - textSize.Y) / 2), ImGui.GetColorU32(GratisTheme.Primary), text);
        ChromeControls.Tooltip(options.Tooltip ?? label, windowBounds: MapToolbarDrawing.WindowBounds);
        ImGui.PopStyleColor();
        return clicked;
    }
}
