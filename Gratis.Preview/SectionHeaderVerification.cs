using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class SectionHeaderVerification {
    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException("Section/empty parity: " + message);
    }

    public static void Run() {

        var SettingsGroup = new SectionProfile() {
            FontSize = 12, FontRole = ChromeFontRole.Body, Ink = new Vector4(143 / 255f, 143 / 255f, 143 / 255f, 1),
            Padding = new(0, 12), BorderSize = 0, IconOffset = new(28, 18), IconSize = 14,
            TextOffset = new(44, 0), Height = 36,
        };
        var QuestSticky = new SectionProfile() {
            Height = 32, Radius = 6, Fill = new Vector4(10 / 255f, 10 / 255f, 11 / 255f, 1),
            IconOffset = new(10, 15), IconInk = new Vector4(.85f, .72f, .48f, 1), Ink = new Vector4(237 / 255f, 237 / 255f, 237 / 255f, 1),
            TextOffset = new(24, 6), TrailingInset = 8, SecondaryInk = new(143 / 255f, 143 / 255f, 143 / 255f, 1),
            ArrowA = new(-4, -2), ArrowB = new(4, -2), ArrowC = new(0, 3),
            ClosedA = new(-2, -4), ClosedB = new(-2, 4), ClosedC = new(3, 0),
        };
        var Detail = new SectionProfile() {
            Before = 8, FontSize = 12, FontRole = ChromeFontRole.Chip, Ink = new Vector4(143 / 255f, 143 / 255f, 143 / 255f, 1), Colored = true,
        };
        var ItemTooltip = new SectionProfile() {
            Before = 6, AfterLine = 10, Border = new Vector4(1, 1, 1, .10f),
            FontSize = 12, FontRole = ChromeFontRole.Chip, Ink = new Vector4(143 / 255f, 143 / 255f, 143 / 255f, 1), Colored = true,
        };
        var Palette = new SectionProfile() {
            Spacing = true, IconOffset = new(8, 9), IconInk = new Vector4(.85f, .72f, .48f, .75f),
            IconSize = 14, TextOffset = new(24, 0), FontSize = 14, FontRole = ChromeFontRole.Body,
        };
        var Travel = new SectionProfile() {
            FontSize = 12, FontRole = ChromeFontRole.Chip, TitleSize = 16, Ink = new Vector4(143 / 255f, 143 / 255f, 143 / 255f, 1),
            IconOffset = new(5, 7), Diamond = new(4, 6), IconInk = new Vector4(.72f, .9f, 1, 1),
            TextOffset = new(18, 12), DetailGap = 16, DetailInset = 6, Padding = new(4, 2),
            Fill = new Vector4(.85f, .72f, .48f, .10f),
        };
        Require(SectionFaces.SettingsGroup == SettingsGroup, "SettingsGroup literals changed.");
        Require(SectionFaces.QuestSticky == QuestSticky, "QuestSticky literals changed.");
        Require(SectionFaces.Detail == Detail, "Detail literals changed.");
        Require(SectionFaces.ItemTooltip == ItemTooltip, "ItemTooltip literals changed.");
        Require(SectionFaces.Palette == Palette, "Palette literals changed.");
        Require(SectionFaces.Travel == Travel, "Travel literals changed.");
        var cache = new SectionHeaderCache();
        Require(cache.Upper("Base Item") == "BASE ITEM" && cache.Upper("Effects") == "EFFECTS",
            "Uppercase cache invalidation changed.");
        Require(cache.Count(12) == "12" && cache.Count(3) == "3", "Count cache invalidation changed.");
        foreach (var style in new[] { 0, 1 })
            foreach (var scale in new[] { 1f, 1.25f }) {
                var travel = new[] { new SectionHeaderCache(), new SectionHeaderCache(), new SectionHeaderCache() };
                VerifyDraw("Section headers", style, scale, 9, (original, i) => {
                    const string label = "A section heading";
                    switch (i) {
                        case 0:
                            ImGui.SetNextItemOpen(true, ImGuiCond.Always);
                            if (original) OriginalSettings(label, scale);
                            else SectionHeader.Settings("##group-test", label, GratisIcons.Info, true, scale);
                            Require(ImGui.GetItemID() == ImGui.GetID("##group-test"), "Settings native ID changed.");
                            break;
                        case 1: case 2:
                            if (original) OriginalSticky(label, ImGui.GetCursorScreenPos(), 320, scale, i == 1);
                            else SectionHeader.Sticky("##section", label, cache.Count(12), ImGui.GetCursorScreenPos(), 320, scale, i == 1);
                            Require(ImGui.GetItemID() == ImGui.GetID("##section"), "Sticky native ID changed.");
                            break;
                        case 3:
                            if (original) OriginalDetail(label, scale);
                            else SectionHeader.Draw(label, SectionFaces.Detail, scale);
                            break;
                        case 4:
                            if (original) OriginalItem(label, 320, scale);
                            else SectionHeader.Draw(cache.Upper(label), SectionFaces.ItemTooltip, scale, width: 320);
                            break;
                        case 5:
                            if (original) OriginalPalette(label, scale, GratisIcons.Repeat);
                            else SectionHeader.Draw(label, SectionFaces.Palette, scale, GratisIcons.Repeat);
                            break;
                        default:
                            var name = i == 7 ? "A very long destination name that is deliberately wider than the available content region" : "Your destination";
                            var subtitle = i == 8 ? "A subtitle that also needs to fit in the current available content region" : "";
                            var detail = i == 6 ? "" : "120 gil";
                            if (original) OriginalTravel("Travelling", name, detail, subtitle);
                            else SectionHeader.Travel(travel[i - 6], "Travelling", name, detail, subtitle, scale);
                            break;
                    }
                });
            }
    }

    internal static void VerifyDraw(string name, int style, float scale, int count, Action<bool, int> draw) {
        var config = new SystemConfig { AcceptedSpoilerWarning = true, VisualStyle = style,
            InterfaceAnimations = InterfaceAnimationMode.Off, ChromeScale = scale, WindowSize = new(600, 900) };
        using var driver = new ScriptedFrameDriver(config, new FrameScript { Width = 800, Height = 1100 });
        driver.BenchmarkMapVisible = false;
        var vertices = new ImDrawVert[count][];
        var indices = new ushort[count][];
        var commands = new (uint Elements, uint Index, uint Vertex, Vector4 Clip, nint Texture)[count][];
        var bounds = new Vector4[count];
        var cursor = new Vector2[count];
        var ids = new uint[count];
        var original = true;
        var capture = false;
        var compare = false;
        var measure = false;
        long bytes = 0;
        driver.DrawPreviewOverlay = () => {
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(new(600, 1000));
            ImGui.Begin("Component parity", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDecoration);
            for (var i = 0; i < count; i++) {
                ImGui.PushID(i);
                ImGui.SetCursorScreenPos(new(i >= 7 ? 320 : 24, 24 + i * 100));
                var list = ImGui.GetWindowDrawList();
                var v0 = list.VtxBuffer.Size;
                var n0 = list.IdxBuffer.Size;
                var before = GC.GetAllocatedBytesForCurrentThread();
                draw(original, i);
                if (measure) bytes += GC.GetAllocatedBytesForCurrentThread() - before;
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                if (capture) {
                    bounds[i] = new(min.X, min.Y, max.X, max.Y);
                    ids[i] = ImGui.GetItemID();
                    cursor[i] = ImGui.GetCursorScreenPos();
                    vertices[i] = new ImDrawVert[list.VtxBuffer.Size - v0];
                    indices[i] = new ushort[list.IdxBuffer.Size - n0];
                    commands[i] = new (uint, uint, uint, Vector4, nint)[list.CmdBuffer.Size];
                    for (var v = 0; v < vertices[i].Length; v++) {
                        var a = list.VtxBuffer[v0 + v];
                        vertices[i][v] = new() { pos = a.pos, uv = a.uv, col = a.col };
                    }
                    for (var n = 0; n < indices[i].Length; n++) indices[i][n] = list.IdxBuffer[n0 + n];
                    for (var c = 0; c < commands[i].Length; c++) {
                        var a = list.CmdBuffer[c];
                        commands[i][c] = (a.ElemCount, a.IdxOffset, a.VtxOffset, a.ClipRect, a.TextureId);
                    }
                }
                if (compare) {
                    Require(bounds[i] == new Vector4(min.X, min.Y, max.X, max.Y), "Native bounds changed.");
                    Require(ids[i] == ImGui.GetItemID(), "Native ID changed.");
                    Require(cursor[i] == ImGui.GetCursorScreenPos(), "Cursor layout changed.");
                    Require(vertices[i].Length == list.VtxBuffer.Size - v0 && indices[i].Length == list.IdxBuffer.Size - n0,
                        "Geometry counts changed.");
                    for (var v = 0; v < vertices[i].Length; v++) {
                        var a = list.VtxBuffer[v0 + v]; var b = vertices[i][v];
                        Require(a.pos == b.pos && a.uv == b.uv && a.col == b.col, "Vertex changed.");
                    }
                    for (var n = 0; n < indices[i].Length; n++)
                        Require(indices[i][n] == list.IdxBuffer[n0 + n], "Index changed.");
                    Require(commands[i].Length == list.CmdBuffer.Size, "Command count changed.");
                    for (var c = 0; c < commands[i].Length; c++) {
                        var a = list.CmdBuffer[c];
                        Require(commands[i][c] == (a.ElemCount, a.IdxOffset, a.VtxOffset, a.ClipRect, a.TextureId),
                            "Draw command order/clip/texture changed.");
                    }
                }
                ImGui.PopID();
            }
            ImGui.End();
        };
        var time = 0d;
        void Step() => driver.Step(new(time += .02), false);
        for (var i = 0; i < 32; i++) Step();
        capture = true; Step(); capture = false;
        original = false;
        for (var i = 0; i < 32; i++) Step();
        compare = true; Step(); compare = false;
        measure = true;
        for (var i = 0; i < 256; i++) Step();
        Require(bytes == 0, $"{name} allocated {bytes} B / 256 steady frames.");
        Console.WriteLine($"{name}: VisualStyle {style}, scale {scale}: {count} cases; IDs/bounds/layout/commands/geometry passed; {bytes} B / 256 frames.");
    }

    private static bool OriginalSettings(string label, float scale) {
        bool open;
        using (MapDrawContext.Current.GetChromeFont(12 * scale, ChromeFontRole.Body)?.Push()) {
            ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Secondary);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 12) * scale);
            var heading = ImGui.GetCursorScreenPos();
            open = ImGui.CollapsingHeader("##group-test",
                true ? ImGuiTreeNodeFlags.DefaultOpen
                    : ImGuiTreeNodeFlags.None);
            GratisIcons.Draw(ImGui.GetWindowDrawList(), heading + new Vector2(28, 18) * scale,
                GratisIcons.Info, GratisTheme.Secondary, 14 * scale);
            ImGui.GetWindowDrawList().AddText(heading
                + new Vector2(44 * scale, (36 * scale - ImGui.GetFontSize()) / 2),
                ImGui.GetColorU32(GratisTheme.Secondary), label);
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor();
        }
        return open;
    }
    private static bool OriginalSticky(string label, Vector2 header, float width, float s, bool expanded) {
        var headerHeight = 32 * s;
        ImGui.SetCursorScreenPos(header);
        var clicked = ChromeControls.Button("##section", new(width, headerHeight));
        var list = ImGui.GetWindowDrawList();
        list.AddRectFilled(header, header + new Vector2(width, headerHeight),
            ImGui.GetColorU32(GratisTheme.Surface with { W = 1 }), GratisTheme.Radius.Control * s);
        ChromeControls.Feedback(list, header, header + new Vector2(width, headerHeight), fill: true);
        var arrow = header + new Vector2(10, 15) * s;
        var ink = ImGui.GetColorU32(GratisTheme.WarmAccent);
        if (expanded)
            list.AddTriangleFilled(arrow + new Vector2(-4, -2) * s, arrow + new Vector2(4, -2) * s,
                arrow + new Vector2(0, 3) * s, ink);
        else
            list.AddTriangleFilled(arrow + new Vector2(-2, -4) * s, arrow + new Vector2(-2, 4) * s,
                arrow + new Vector2(3, 0) * s, ink);
        TextDrawing.Draw(list, header + new Vector2(24, 6) * s, ImGui.GetColorU32(GratisTheme.Primary),
            label);
        var count = "12";
        TextDrawing.Draw(list, header + new Vector2(width - TextDrawing.Measure(count).X - 8 * s, 6 * s),
            ImGui.GetColorU32(GratisTheme.Secondary), count);
        return clicked;
    }
    private static void OriginalDetail(string title, float s) {
        ImGui.SetCursorScreenPos(ImGui.GetCursorScreenPos() + new Vector2(0, 8 * s));
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push();
        ImGui.TextColored(GratisTheme.Secondary, title);
    }

    private static void OriginalItem(string label, float width, float s) {
        var p = ImGui.GetCursorScreenPos() + new Vector2(0, 6 * s);
        ImGui.GetWindowDrawList().AddLine(p, p + new Vector2(width, 0), ImGui.GetColorU32(GratisTheme.Border), s);
        ImGui.SetCursorScreenPos(p + new Vector2(0, 10 * s));
        using (MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push())
            ImGui.TextColored(GratisTheme.Secondary, label.ToUpperInvariant());
    }
    private static void OriginalPalette(string label, float scale, char icon) {
        ImGui.Spacing();
        var position = ImGui.GetCursorScreenPos();
        GratisIcons.Draw(ImGui.GetWindowDrawList(), position + new Vector2(8, 9) * scale, icon,
            GratisTheme.WarmAccent with { W = .75f }, 14 * scale);
        ImGui.SetCursorScreenPos(position + new Vector2(24 * scale, 0));
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * scale,
            ChromeFontRole.Body)?.Push();
        ImGui.TextUnformatted(label);
    }

    private static void OriginalTravel(string label, string name, string detail, string subtitle = "") {
        var context = MapDrawContext.Current;
        var s = GratisTheme.ChromeScale;
        using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push()) {
            var pos = ImGui.GetCursorScreenPos();
            var list = ImGui.GetWindowDrawList();
            var crystal = pos + new Vector2(5, 7) * s;
            list.AddQuad(crystal - new Vector2(0, 6) * s, crystal + new Vector2(4, 0) * s,
                crystal + new Vector2(0, 6) * s, crystal - new Vector2(4, 0) * s,
                ImGui.GetColorU32(GratisTheme.Crystal), s);
            ImGui.Dummy(new Vector2(18, 12) * s);
            ImGui.SameLine(0, 0);
            ImGui.TextColored(GratisTheme.Secondary, label);
            if (detail.Length > 0) {
                var x = ImGui.GetWindowWidth() - ImGui.GetStyle().WindowPadding.X - ImGui.CalcTextSize(detail).X;
                if (x > ImGui.GetCursorPosX() + 16 * s) {
                    ImGui.SameLine(x - 6 * s);
                    var chip = ImGui.GetCursorScreenPos();
                    var measured = ImGui.CalcTextSize(detail);
                    list.AddRectFilled(chip - new Vector2(4, 2) * s, chip + measured + new Vector2(4, 2) * s,
                        ImGui.GetColorU32(GratisTheme.WarmAccent with { W = .10f }),
                        GratisTheme.Radius.Pill(measured + new Vector2(8, 4) * s));
                    ImGui.TextColored(GratisTheme.Secondary, detail);
                }
            }
        }
        using (context.GetChromeFont(GratisTheme.TravelTitleSize * s, ChromeFontRole.Title)?.Push()) {
            var displayed = ChromeControls.FitLabel(name, ImGui.GetContentRegionAvail().X);
            ImGui.TextUnformatted(displayed);
            if (displayed != name)
                ChromeControls.Tooltip(name);
        }
        if (subtitle.Length > 0) {
            using var secondary = context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push();
            ImGui.TextColored(GratisTheme.Secondary,
                ChromeControls.FitLabel(subtitle, ImGui.GetContentRegionAvail().X));
        }
        ImGui.Spacing();
    }
}
