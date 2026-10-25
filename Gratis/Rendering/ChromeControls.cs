using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class ChromeControls
{
    private static MapDrawContext? hoverContext;
    private static Vector4 hoverBounds;
    private static double hoverBegan;
    private static int hoverFrame = -2;

    public static bool Button(string id, Vector2 size)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0);
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
        var clicked = ImGui.Button(id, size);
        ImGui.PopStyleColor(4); ImGui.PopStyleVar();
        return clicked;
    }

    public static void Feedback(ImDrawListPtr list, Vector2 min, Vector2 max, bool selected = false, bool fill = true)
    {
        var s = GratisTheme.ChromeScale;
        var hovered = ImGui.IsItemHovered();
        var pressed = ImGui.IsItemActive();
        if (fill && (hovered || pressed || selected))
            list.AddRectFilled(min + new Vector2(s), max - new Vector2(s), ImGui.GetColorU32(
                selected ? GratisTheme.WarmAccent with { W = .12f } : new Vector4(1, 1, 1, pressed ? .12f : .06f)), GratisTheme.ButtonRadius * s);
        if (ImGui.IsItemFocused() && ImGui.GetIO().NavVisible)
            list.AddRect(min + new Vector2(2 * s), max - new Vector2(2 * s), ImGui.GetColorU32(GratisTheme.Crystal), GratisTheme.ButtonRadius * s, ImDrawFlags.None, s);
    }

    public static bool IconButton(string id, char glyph, string label, Vector2 size)
    {
        var pos = ImGui.GetCursorScreenPos();
        var clicked = Button(id, size);
        var list = ImGui.GetWindowDrawList();
        Feedback(list, pos, pos + size);
        GratisIcons.Draw(list, pos + size / 2, glyph, GratisTheme.Secondary, GratisTheme.IconSize * GratisTheme.ChromeScale);
        Tooltip(label);
        return clicked;
    }

    public static void Tooltip(string label, string? shortcut = null)
    {
        var focused = ImGui.IsItemFocused() && ImGui.GetIO().NavVisible;
        if (!ImGui.IsItemHovered() && !focused) return;
        var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax();
        var bounds = new Vector4(min.X, min.Y, max.X, max.Y);
        var now = ImGui.GetTime(); var frame = ImGui.GetFrameCount();
        if (!ReferenceEquals(hoverContext, MapDrawContext.Current) || hoverBounds != bounds || hoverFrame < frame - 1) {
            hoverContext = MapDrawContext.Current; hoverBounds = bounds; hoverBegan = now;
        }
        hoverFrame = frame;
        if (!focused && now - hoverBegan < .4) return;
        using var style = new ChromePopupStyle();
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * GratisTheme.ChromeScale, ChromeFontRole.Body)?.Push();
        ImGui.BeginTooltip();
        ImGui.TextUnformatted(label);
        if (!string.IsNullOrEmpty(shortcut)) { ImGui.SameLine(0, GratisTheme.Gap * GratisTheme.ChromeScale); Hints((shortcut, "")); }
        ImGui.EndTooltip();
    }

    public static string FitLabel(string text, float width)
    {
        if (ImGui.CalcTextSize(text).X <= width) return text;
        const string ellipsis = "\u2026";
        if (ImGui.CalcTextSize(ellipsis).X > width) return "";
        var length = text.Length;
        while (length > 0) {
            length--;
            if (length > 0 && char.IsLowSurrogate(text[length])) length--;
            if (ImGui.CalcTextSize(text[..length] + ellipsis).X <= width) return text[..length] + ellipsis;
        }
        return ellipsis;
    }

    public static void Hints(params (string Key, string Label)[] hints)
    {
        var s = GratisTheme.ChromeScale;
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push();
        var first = true;
        foreach (var (key, label) in hints) {
            var keySize = ImGui.CalcTextSize(key); var labelSize = ImGui.CalcTextSize(label);
            var cap = keySize.X + 12 * s;
            var width = cap + (label.Length > 0 ? 6 * s + labelSize.X : 0);
            if (!first) ImGui.SameLine(0, 12 * s);
            if (width > ImGui.GetContentRegionAvail().X) { if (!first) ImGui.NewLine(); break; }
            var pos = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(width, MathF.Max(22 * s, keySize.Y + 4 * s)));
            var list = ImGui.GetWindowDrawList();
            list.AddRectFilled(pos, pos + new Vector2(cap, 22 * s), ImGui.GetColorU32(new Vector4(1, 1, 1, .04f)), 4 * s);
            list.AddRect(pos + new Vector2(.5f * s), pos + new Vector2(cap - .5f * s, 21.5f * s), ImGui.GetColorU32(GratisTheme.Border), 4 * s);
            list.AddText(pos + new Vector2(6 * s, (22 * s - keySize.Y) / 2), ImGui.GetColorU32(GratisTheme.Primary), key);
            if (label.Length > 0) list.AddText(pos + new Vector2(cap + 6 * s, (22 * s - labelSize.Y) / 2), ImGui.GetColorU32(new Vector4(.72f, .72f, .72f, 1)), label);
            first = false;
        }
    }
}
