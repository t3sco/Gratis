using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class MenuRowVerification {
    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException("Menu rows: " + message);
    }

    public static void Run() {
        Profiles();
        Verify(InterfaceAnimationMode.Off, 1);
        Verify(InterfaceAnimationMode.Full, 1);
        Verify(InterfaceAnimationMode.Reduced, 1);
        Verify(InterfaceAnimationMode.Off, 1.25f);
        Verify(InterfaceAnimationMode.Full, 1.25f);
    }

    private static void Profiles() {
        var primary = new Vector4(237 / 255f, 237 / 255f, 237 / 255f, 1);
        var secondary = new Vector4(143 / 255f, 143 / 255f, 143 / 255f, 1);
        var location = new RowProfile {
            Height = 36, MinimumWidth = 1, TextInset = 8, TrailingInset = 8, Ink = primary, Fit = true,
        };
        Require(RowFaces.Location == location, "Location literals changed.");
        Require(RowFaces.Navigation == new RowProfile {
            Height = 36, MinimumWidth = 200, TextInset = 36, IconInset = 16, IconSize = 18,
            Ink = primary, SecondaryInk = secondary,
        }, "Navigation literals changed.");
        Require(RowFaces.Reward == new RowProfile {
            Height = 36, TextInset = 8, Ink = primary, TextDrawing = true,
        }, "Reward literals changed.");
        Require(RowFaces.Context == location with {
            TextInset = 40, IconInset = 18, IconSize = 18, ChildReserve = 24, ChildInset = 16,
            ChildHalfSize = new(2, 4), SecondaryInk = secondary,
            SelectionFill = new(.85f, .72f, .48f, .08f), SelectionKey = "##context-highlight",
            ClampFit = true, Control = true, DisabledScope = true, TruncatedTooltip = true,
        }, "Context literals changed.");
    }

    private static void Verify(InterfaceAnimationMode animations, float scale) {
        var config = new SystemConfig { AcceptedSpoilerWarning = true, VisualStyle = 0,
            InterfaceAnimations = animations, ChromeScale = scale, WindowSize = new(600, 700) };
        using var driver = new ScriptedFrameDriver(config, new FrameScript { Width = 800, Height = 900 });
        driver.BenchmarkMapVisible = false;
        string[] labels = ["A context action with a very long name", "A long submenu name",
            "Unavailable action", "Unavailable submenu", "A long location name", "Narrow location",
            "Locate player", "Copy name"];
        string[] ids = ["##context-row-0", "##context-row-1", "##context-row-2", "##context-row-3",
            "##location-zone", "##location-narrow", "##navigation-Locate player", "Copy name"];
        var caches = ids.Select(id => new MenuRowCache(id)).ToArray();
        var invalidated = new ActionLabelCache();
        RowOptions[] options = [
            new(RowFaces.Context, scale, 150 * scale, GratisIcons.Flag),
            new(RowFaces.Context, scale, 150 * scale, GratisIcons.Layers, Children: true),
            new(RowFaces.Context, scale, 200 * scale, GratisIcons.Info, Enabled: false),
            new(RowFaces.Context, scale, 60 * scale, GratisIcons.Info, Enabled: false, Children: true),
            new(RowFaces.Location, scale, 100 * scale), new(RowFaces.Location, scale, 10 * scale),
            new(RowFaces.Navigation, scale, Icon: GratisIcons.LocateFixed),
            new(RowFaces.Reward, scale, 184 * scale),
        ];
        var vertices = new ImDrawVert[ids.Length][];
        var indices = new ushort[ids.Length][];
        var bounds = new Vector4[ids.Length];
        var original = true;
        var capture = false;
        var compare = false;
        var measure = false;
        var focusNext = false;
        var focused = false;
        var clicked = false;
        var hoverIndex = -1;
        long bytes = 0;
        driver.DrawPreviewOverlay = () => {
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(new(600, 700));
            if (focusNext) ImGui.SetNextWindowFocus();
            using var font = driver.Context.GetChromeFont(14 * scale, ChromeFontRole.Body)?.Push();
            ImGui.Begin("Menu row verification", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDecoration);
            for (var i = 0; i < options.Length; i++) {
                var pos = new Vector2(i == 6 ? 520 : 24, 24 + i * 64);
                ImGui.SetCursorScreenPos(pos);
                if (i == 0 && focusNext) ImGui.SetKeyboardFocusHere();
                ImGui.BeginDisabled(i == 7);
                var alpha = ImGui.GetStyle().Alpha;
                var list = ImGui.GetWindowDrawList();
                var firstVertex = list.VtxBuffer.Size;
                var firstIndex = list.IdxBuffer.Size;
                var expectedSize = new Vector2(options[i].Width != 0 ? options[i].Width
                    : MathF.Max(200 * scale, ImGui.GetContentRegionAvail().X), 36 * scale);
                var before = GC.GetAllocatedBytesForCurrentThread();
                bool activated;
                if (original) activated = Original(i, labels[i], options[i]);
                else {
                    var row = new MenuRow(caches[i], options[i]);
                    Require(row.Min == pos && row.Size == expectedSize, "Submitted bounds changed.");
                    row.Draw(labels[i], i is 0 or 2 or 3);
                    activated = row.Clicked;
                    Require(row.Hovered == ImGui.IsItemHovered(), "Hover state changed.");
                }
                if (measure) bytes += GC.GetAllocatedBytesForCurrentThread() - before;
                Require(ImGui.GetStyle().Alpha == alpha, "Disabled alpha leaked.");
                Require(i is not (2 or 3 or 7) || !activated, "Disabled row activated.");
                Require(UiMotion.Item == ImGui.GetID(ids[i]), "Native ID changed.");
                Require(ImGui.GetItemRectMin() == pos && ImGui.GetItemRectSize() == expectedSize,
                    "Native hit rectangle changed.");
                if (i == 0) { focused = ImGui.IsItemFocused(); clicked |= activated; }
                Require(UiMotion.Store.Contains(new(ImGui.GetID(ids[i]), 3)) == (i < 4 && !UiMotion.Off),
                    "Press transform channel changed.");
                if (i == hoverIndex) {
                    Require(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled), "Disabled hit rectangle changed.");
                    Require(ImGui.IsItemHovered() == (i < 2), "Disabled hover changed.");
                }
                if (capture) {
                    bounds[i] = new(pos.X, pos.Y, pos.X + expectedSize.X, pos.Y + expectedSize.Y);
                    vertices[i] = new ImDrawVert[list.VtxBuffer.Size - firstVertex];
                    indices[i] = new ushort[list.IdxBuffer.Size - firstIndex];
                    for (var v = 0; v < vertices[i].Length; v++) {
                        var vertex = list.VtxBuffer[firstVertex + v];
                        vertices[i][v] = new() { pos = vertex.pos, uv = vertex.uv, col = vertex.col };
                    }
                    for (var n = 0; n < indices[i].Length; n++) indices[i][n] = list.IdxBuffer[firstIndex + n];
                }
                if (compare) {
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
            if (!original) {
                var fitted = invalidated;
                Require(fitted.Fit(labels[0], 35) == ChromeControls.FitLabel(labels[0], 35), "Width invalidation changed.");
                Require(fitted.Fit("Changed label", 35) == ChromeControls.FitLabel("Changed label", 35),
                    "Text invalidation changed.");
                using (driver.Context.GetChromeFont(20 * scale, ChromeFontRole.Title)?.Push())
                    Require(fitted.Fit("Changed label", 35) == ChromeControls.FitLabel("Changed label", 35),
                        "Font invalidation changed.");
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
        Require(bytes == 0, $"steady submit/draw path allocated {bytes} B.");
        measure = false;
        ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        focusNext = true;
        Step();
        focusNext = false;
        Step();
        Require(focused, "Keyboard focus changed.");
        var hover = new Vector2(40, 35);
        driver.Step(new(time += .02, Mouse: hover), false);
        driver.Step(new(time += .5, Mouse: hover), false);
        Require(ChromeControls.LastTooltipAnchor == bounds[0], "Truncation tooltip anchor changed.");
        driver.Step(new(time += .02, Mouse: hover, Left: true), false);
        driver.Step(new(time += .02, Mouse: hover, Left: false), false);
        Require(clicked, "Native activation changed.");
        foreach (var i in new[] { 2, 3, 7 }) {
            hoverIndex = i;
            driver.Step(new(time += .02, Mouse: new Vector2(40, 35 + i * 64)), false);
        }
        Console.WriteLine($"Menu rows: {animations}, scale {scale}: original {originalBytes} B -> {bytes} B / 256 frames; "
            + "IDs/bounds, geometry, focus, disabled state and truncation tooltip passed.");
    }

    private static bool Original(int index, string label, in RowOptions options) {
        var s = options.Scale;
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(options.Width != 0 ? options.Width
            : MathF.Max(200 * s, ImGui.GetContentRegionAvail().X), 36 * s);
        var id = index < 4 ? "##context-row-" + index : index < 6
            ? "##location-" + (index == 4 ? "zone" : "narrow") : index == 6 ? "##navigation-" + label : label;
        if (index < 4) ImGui.BeginDisabled(!options.Enabled);
        var clicked = ChromeControls.Button(id, size);
        if (index < 4) ImGui.EndDisabled();
        var list = ImGui.GetWindowDrawList();
        using var control = index < 4 ? UiMotion.Control(list, pos, pos + size) : (UiMotion.VisualScope?)null;
        var selected = index == 0;
        if (selected) {
            var highlight = UiMotion.Selection("##context-highlight", pos);
            list.AddRectFilled(highlight, highlight + size,
                ImGui.GetColorU32(GratisTheme.WarmAccent with { W = .08f }), GratisTheme.Radius.Control * s);
        }
        ChromeControls.Feedback(list, pos, pos + size, selected);
        if (index < 4 || index == 6)
            GratisIcons.Draw(list, pos + new Vector2((index < 4 ? 18 : 16) * s, size.Y / 2), options.Icon,
                GratisTheme.Secondary, 18 * s);
        var inset = index < 4 ? 40 : index == 6 ? 36 : 8;
        var displayed = index < 4
            ? ChromeControls.FitLabel(label, MathF.Max(0, size.X - 40 * s - (options.Children ? 24 * s : 8 * s)))
            : index < 6 ? ChromeControls.FitLabel(label, size.X - 16 * s) : label;
        var textPos = pos + new Vector2(inset * s,
            (size.Y - (index == 7 ? ImGui.GetTextLineHeight() : ImGui.GetFontSize())) / 2);
        var ink = ImGui.GetColorU32(options.Enabled ? GratisTheme.Primary : GratisTheme.Secondary);
        if (index == 7) TextDrawing.Draw(list, textPos, ink, displayed);
        else list.AddText(textPos, ink, displayed);
        if (options.Children) {
            var center = pos + new Vector2(size.X - 16 * s, size.Y / 2);
            list.AddLine(center + new Vector2(-2, -4) * s, center + new Vector2(2, 0) * s,
                ImGui.GetColorU32(GratisTheme.Secondary), s);
            list.AddLine(center + new Vector2(2, 0) * s, center + new Vector2(-2, 4) * s,
                ImGui.GetColorU32(GratisTheme.Secondary), s);
        }
        if (index < 4 && displayed != label) ChromeControls.Tooltip(label);
        return clicked;
    }
}
