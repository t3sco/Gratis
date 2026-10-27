using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public readonly record struct SectionProfile {
    public float FontSize { get; init; }
    public ChromeFontRole FontRole { get; init; }
    public Vector4 Ink { get; init; }
    public Vector4 IconInk { get; init; }
    public Vector4 SecondaryInk { get; init; }
    public Vector2 IconOffset { get; init; }
    public float IconSize { get; init; }
    public Vector2 TextOffset { get; init; }
    public Vector2 Padding { get; init; }
    public float Height { get; init; }
    public float BorderSize { get; init; }
    public float Radius { get; init; }
    public float Before { get; init; }
    public float AfterLine { get; init; }
    public float TrailingInset { get; init; }
    public Vector4 Fill { get; init; }
    public Vector4 Border { get; init; }
    public Vector2 ArrowA { get; init; }
    public Vector2 ArrowB { get; init; }
    public Vector2 ArrowC { get; init; }
    public Vector2 ClosedA { get; init; }
    public Vector2 ClosedB { get; init; }
    public Vector2 ClosedC { get; init; }
    public bool Spacing { get; init; }
    public bool Colored { get; init; }
    public float TitleSize { get; init; }
    public float DetailGap { get; init; }
    public float DetailInset { get; init; }
    public Vector2 Diamond { get; init; }
}

public static class SectionFaces {
    public static readonly SectionProfile SettingsGroup = new() {
        FontSize = 12, FontRole = ChromeFontRole.Body, Ink = GratisTheme.Secondary,
        Padding = new(0, 12), BorderSize = 0, IconOffset = new(28, 18), IconSize = 14,
        TextOffset = new(44, 0), Height = 36,
    };
    public static readonly SectionProfile QuestSticky = new() {
        Height = 32, Radius = GratisTheme.Radius.Control, Fill = GratisTheme.Surface with { W = 1 },
        IconOffset = new(10, 15), IconInk = GratisTheme.WarmAccent, Ink = GratisTheme.Primary,
        TextOffset = new(24, 6), TrailingInset = 8, SecondaryInk = GratisTheme.Secondary,
        ArrowA = new(-4, -2), ArrowB = new(4, -2), ArrowC = new(0, 3),
        ClosedA = new(-2, -4), ClosedB = new(-2, 4), ClosedC = new(3, 0),
    };
    public static readonly SectionProfile Detail = new() {
        Before = 8, FontSize = 12, FontRole = ChromeFontRole.Chip, Ink = GratisTheme.Secondary, Colored = true,
    };
    public static readonly SectionProfile ItemTooltip = new() {
        Before = 6, AfterLine = 10, Border = GratisTheme.Border,
        FontSize = 12, FontRole = ChromeFontRole.Chip, Ink = GratisTheme.Secondary, Colored = true,
    };
    public static readonly SectionProfile Palette = new() {
        Spacing = true, IconOffset = new(8, 9), IconInk = GratisTheme.WarmAccent with { W = .75f },
        IconSize = 14, TextOffset = new(24, 0), FontSize = 14, FontRole = ChromeFontRole.Body,
    };
    public static readonly SectionProfile Travel = new() {
        FontSize = 12, FontRole = ChromeFontRole.Chip, TitleSize = 16, Ink = GratisTheme.Secondary,
        IconOffset = new(5, 7), Diamond = new(4, 6), IconInk = GratisTheme.Crystal,
        TextOffset = new(18, 12), DetailGap = 16, DetailInset = 6, Padding = new(4, 2),
        Fill = GratisTheme.WarmAccent with { W = .10f },
    };
}

public sealed class SectionHeaderCache {
    private string source = "", upper = "", counted = "";
    private int count = -1;
    private global::System.Globalization.CultureInfo? culture;
    internal readonly ActionLabelCache Title = new(), Subtitle = new();
    public string Upper(string label) {
        if (source != label) { source = label; upper = label.ToUpperInvariant(); }
        return upper;
    }
    public string Count(int value) {
        var current = global::System.Globalization.CultureInfo.CurrentCulture;
        if (count != value || !ReferenceEquals(culture, current) || !current.IsReadOnly
            || !current.NumberFormat.IsReadOnly) {
            count = value; culture = current; counted = value.ToString();
        }
        return counted;
    }
}

public static class SectionHeader {
    public static bool Settings(string id, string label, char icon, bool defaultOpen, float scale) {
        var face = SectionFaces.SettingsGroup;
        using var font = MapDrawContext.Current.GetChromeFont(face.FontSize * scale, face.FontRole)?.Push();
        ImGui.PushStyleColor(ImGuiCol.Text, face.Ink);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, face.BorderSize);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, face.Padding * scale);
        var heading = ImGui.GetCursorScreenPos();
        var open = ImGui.CollapsingHeader(id, defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
        GratisIcons.Draw(ImGui.GetWindowDrawList(), heading + face.IconOffset * scale, icon, face.Ink, face.IconSize * scale);
        ImGui.GetWindowDrawList().AddText(heading + new Vector2(face.TextOffset.X * scale,
            (face.Height * scale - ImGui.GetFontSize()) / 2), ImGui.GetColorU32(face.Ink), label);
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor();
        return open;
    }

    public static bool Sticky(string id, string label, string count, Vector2 header, float width, float s, bool expanded) {
        var face = SectionFaces.QuestSticky;
        var size = new Vector2(width, face.Height * s);
        ImGui.SetCursorScreenPos(header);
        var clicked = ChromeControls.Button(id, size);
        var list = ImGui.GetWindowDrawList();
        list.AddRectFilled(header, header + size, ImGui.GetColorU32(face.Fill), face.Radius * s);
        ChromeControls.Feedback(list, header, header + size, fill: true);
        var arrow = header + face.IconOffset * s;
        var ink = ImGui.GetColorU32(face.IconInk);
        list.AddTriangleFilled(arrow + (expanded ? face.ArrowA : face.ClosedA) * s,
            arrow + (expanded ? face.ArrowB : face.ClosedB) * s,
            arrow + (expanded ? face.ArrowC : face.ClosedC) * s, ink);
        TextDrawing.Draw(list, header + face.TextOffset * s, ImGui.GetColorU32(face.Ink), label);
        TextDrawing.Draw(list, header + new Vector2(width - TextDrawing.Measure(count).X - face.TrailingInset * s,
            face.TextOffset.Y * s), ImGui.GetColorU32(face.SecondaryInk), count);
        return clicked;
    }

    public static void Draw(string label, in SectionProfile face, float scale, char icon = '\0', float width = 0) {
        if (face.Spacing) ImGui.Spacing();
        if (face.Before != 0)
            ImGui.SetCursorScreenPos(ImGui.GetCursorScreenPos() + new Vector2(0, face.Before * scale));
        if (face.AfterLine != 0) {
            var p = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddLine(p, p + new Vector2(width, 0), ImGui.GetColorU32(face.Border), scale);
            ImGui.SetCursorScreenPos(p + new Vector2(0, face.AfterLine * scale));
        }
        if (face.IconSize != 0) {
            var p = ImGui.GetCursorScreenPos();
            GratisIcons.Draw(ImGui.GetWindowDrawList(), p + face.IconOffset * scale, icon, face.IconInk, face.IconSize * scale);
            ImGui.SetCursorScreenPos(p + face.TextOffset * scale);
        }
        using var font = MapDrawContext.Current.GetChromeFont(face.FontSize * scale, face.FontRole)?.Push();
        if (face.Colored) ImGui.TextColored(face.Ink, label);
        else ImGui.TextUnformatted(label);
    }

    public static void Travel(SectionHeaderCache cache, string label, string name, string detail, string subtitle, float s) {
        var face = SectionFaces.Travel;
        var context = MapDrawContext.Current;
        using (context.GetChromeFont(face.FontSize * s, face.FontRole)?.Push()) {
            var pos = ImGui.GetCursorScreenPos();
            var list = ImGui.GetWindowDrawList();
            var crystal = pos + face.IconOffset * s;
            list.AddQuad(crystal - new Vector2(0, face.Diamond.Y) * s, crystal + new Vector2(face.Diamond.X, 0) * s,
                crystal + new Vector2(0, face.Diamond.Y) * s, crystal - new Vector2(face.Diamond.X, 0) * s,
                ImGui.GetColorU32(face.IconInk), s);
            ImGui.Dummy(face.TextOffset * s);
            ImGui.SameLine(0, 0);
            ImGui.TextColored(face.Ink, label);
            if (detail.Length > 0) {
                var x = ImGui.GetWindowWidth() - ImGui.GetStyle().WindowPadding.X - ImGui.CalcTextSize(detail).X;
                if (x > ImGui.GetCursorPosX() + face.DetailGap * s) {
                    ImGui.SameLine(x - face.DetailInset * s);
                    var chip = ImGui.GetCursorScreenPos();
                    Pill.Draw(list, chip, detail, PillFaces.HeadingDetail, s);
                }
            }
        }
        using (context.GetChromeFont(face.TitleSize * s, ChromeFontRole.Title)?.Push()) {
            var displayed = cache.Title.Fit(name, ImGui.GetContentRegionAvail().X);
            ImGui.TextUnformatted(displayed);
            if (displayed != name)
                ChromeControls.Tooltip(name);
        }
        if (subtitle.Length > 0) {
            using var secondary = context.GetChromeFont(face.FontSize * s, ChromeFontRole.Body)?.Push();
            ImGui.TextColored(face.Ink,
                cache.Subtitle.Fit(subtitle, ImGui.GetContentRegionAvail().X));
        }
        ImGui.Spacing();
    }
}
