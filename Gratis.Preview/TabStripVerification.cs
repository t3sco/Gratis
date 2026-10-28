using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class TabStripVerification {
    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException("Tab strips: " + message);
    }

    public static void Run() {
        var secondary = new Vector4(143 / 255f, 143 / 255f, 143 / 255f, 1);
        var accent = new Vector4(.85f, .72f, .48f, 1);
        var primary = new Vector4(237 / 255f, 237 / 255f, 237 / 255f, 1);
        Require(TabFaces.QuestLog == new TabProfile {
            Height = 28, WidthInset = 8, Columns = 3, Gap = 4, Ink = secondary, SelectedInk = primary,
        }, "Quest literals changed.");
        var settings = new TabProfile {
            Height = 36, WidthInset = 4, Columns = 1, TextInset = 34,
            IconCenter = new(18, 18), IconSize = 16, Ink = secondary,
            SelectedInk = new(237 / 255f, 237 / 255f, 237 / 255f, 1), Accent = accent,
            SelectionFill = new(1, 1, 1, .04f), SelectionRadius = 6,
            IndicatorMin = new(0, 8), IndicatorMax = new(2, 28), SelectionKey = "##settings-indicator",
        };
        Require(TabFaces.Settings == settings, "Settings literals changed.");
        Require(TabFaces.SettingsCompact == settings with { Compact = true }, "Compact literals changed.");
        foreach (var style in new[] { 0, 1 })
            foreach (var scale in new[] { 1f, 1.25f })
                foreach (var motion in new[] { InterfaceAnimationMode.Off, InterfaceAnimationMode.Full })
                    Verify(style, scale, motion);
    }

    private static void Verify(int style, float scale, InterfaceAnimationMode motion) {
        var config = new SystemConfig { AcceptedSpoilerWarning = true, VisualStyle = style,
            InterfaceAnimations = motion, ChromeScale = scale, WindowSize = new(600, 700) };
        using var driver = new ScriptedFrameDriver(config, new FrameScript { Width = 800, Height = 900 });
        driver.BenchmarkMapVisible = false;
        string[] ids = ["##quest-tab-0", "##quest-tab-1", "##settings-0", "##settings-1", "##settings-2", "##settings-3"];
        string[] labels = ["Accepted", "Unaccepted", "Appearance", "Map", "Markers", "Filters"];
        TabProfile[] profiles = [TabFaces.QuestLog, TabFaces.QuestLog, TabFaces.Settings, TabFaces.Settings,
            TabFaces.SettingsCompact, TabFaces.SettingsCompact];
        var bounds = new Vector4[ids.Length];
        var vertices = new ImDrawVert[ids.Length][];
        var indices = new ushort[ids.Length][];
        var original = true;
        var capture = false;
        var compare = false;
        var measure = false;
        var focusNext = false;
        var focused = false;
        var clicked = false;
        var target = 0;
        long bytes = 0;
        driver.DrawPreviewOverlay = () => {
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(new(600, 700));
            if (focusNext) ImGui.SetNextWindowFocus();
            using var font = driver.Context.GetChromeFont(14 * scale, ChromeFontRole.Chip)?.Push();
            ImGui.Begin("Tab strip verification", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDecoration);
            for (var i = 0; i < ids.Length; i++) {
                ImGui.PushID(i);
                ImGui.SetCursorScreenPos(new(24, 24 + i * 64));
                if (focusNext && i == target) ImGui.SetKeyboardFocusHere();
                var list = ImGui.GetWindowDrawList();
                var firstVertex = list.VtxBuffer.Size;
                var firstIndex = list.IdxBuffer.Size;
                var width = (i < 2 ? 360 : i < 4 ? 168 : 44) * scale;
                var selected = i % 2 == 0;
                var before = GC.GetAllocatedBytesForCurrentThread();
                bool activated;
                if (original) activated = Original(ids[i], labels[i], i, width, scale, selected);
                else {
                    var tab = new TabStrip(ids[i], profiles[i], width, scale, selected);
                    activated = tab.Clicked;
                    tab.Draw(labels[i], selected || activated, GratisIcons.Flag);
                }
                if (measure) bytes += GC.GetAllocatedBytesForCurrentThread() - before;
                Require(UiMotion.Item == ImGui.GetID(ids[i]), "Native ID changed.");
                if (i == target) { focused = ImGui.IsItemFocused(); clicked |= activated; }
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
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
                            "Ordered vertex changed.");
                    }
                    for (var n = 0; n < indices[i].Length; n++)
                        Require(indices[i][n] == list.IdxBuffer[firstIndex + n], "Ordered index changed.");
                }
                ImGui.PopID();
            }
            ImGui.End();
        };
        var time = 0d;
        void Step() => driver.Step(new(time += .02), false);
        for (var frame = 0; frame < 64; frame++) Step();
        capture = true;
        Step();
        capture = false;
        original = false;
        for (var frame = 0; frame < 64; frame++) Step();
        compare = motion == InterfaceAnimationMode.Off;
        Step();
        compare = false;
        measure = true;
        for (var frame = 0; frame < 256; frame++) Step();
        measure = false;
        Require(bytes == 0, $"steady submit/draw allocated {bytes} B / 256 frames.");
        ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        foreach (var index in new[] { 0, 2, 4 }) {
            target = index;
            clicked = false;
            focusNext = true;
            Step();
            focusNext = false;
            Step();
            Require(focused, "Keyboard focus changed.");
            var hover = new Vector2(40, 35 + index * 64);
            driver.Step(new(time += .02, Mouse: hover), false);
            driver.Step(new(time += .5, Mouse: hover), false);
            if (index == 4) Require(ChromeControls.LastTooltipAnchor == bounds[index], "Compact tooltip anchor changed.");
            driver.Step(new(time += .02, Mouse: hover, Left: true), false);
            driver.Step(new(time += .02, Mouse: hover, Left: false), false);
            Require(clicked, "Native activation changed.");
        }
        Console.WriteLine($"Tab strips: style {style}, scale {scale}, {motion}: 0 B / 256 frames; IDs/bounds, geometry, focus, activation and tooltip passed.");
    }

    private static bool Original(string id, string label, int index, float width, float s, bool selected) {
        var start = ImGui.GetCursorScreenPos();
        if (index < 2) {
            var size = new Vector2((width - 8 * s) / 3, 28 * s);
            var clicked = ChromeControls.Button(id, size);
            selected |= clicked;
            ChromeControls.Feedback(ImGui.GetWindowDrawList(), start, start + size, selected);
            var text = TextDrawing.Measure(label);
            TextDrawing.Draw(ImGui.GetWindowDrawList(), start + (size - text) / 2,
                ImGui.GetColorU32(selected ? GratisTheme.Primary : GratisTheme.Secondary), label);
            return clicked;
        }
        else {
            var size = new Vector2(width - 4 * s, 36 * s);
            ImGui.PushStyleColor(ImGuiCol.Header, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, Vector4.Zero);
            var clicked = ImGui.Selectable(id, selected, ImGuiSelectableFlags.None, size);
            selected |= clicked;
            ImGui.PopStyleColor(3);
            UiMotion.ItemId(id);
            var nav = ImGui.GetWindowDrawList();
            if (selected) {
                var indicator = UiMotion.Selection("##settings-indicator", start);
                nav.AddRectFilled(indicator, indicator + size, ImGui.GetColorU32(new Vector4(1, 1, 1, .04f)),
                    GratisTheme.Radius.Control * s);
                nav.AddRectFilled(indicator + new Vector2(0, 8) * s, indicator + new Vector2(2, 28) * s,
                    ImGui.GetColorU32(GratisTheme.WarmAccent), GratisTheme.Radius.Pill(new Vector2(2, 20) * s));
            }
            ChromeControls.Feedback(nav, start, start + size, fill: true, scale: s);
            GratisIcons.Draw(nav, start + new Vector2(18, 18) * s, GratisIcons.Flag,
                selected ? GratisTheme.WarmAccent : GratisTheme.Secondary, 16 * s);
            if (index >= 4) ChromeControls.Tooltip(label, scale: s);
            else nav.AddText(start + new Vector2(34 * s, (size.Y - ImGui.GetFontSize()) / 2),
                ImGui.GetColorU32(selected ? GratisTheme.Primary : GratisTheme.Secondary), label);
            return clicked;
        }
    }
}
