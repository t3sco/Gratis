using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static partial class ChromeControls {
#if GRATIS_PREVIEW
    internal static Action<string>? PreviewBeforeButton, PreviewAfterButton;
#endif
    private static MapDrawContext? hoverContext;
    private static Vector4 hoverBounds;
    private static double hoverBegan;
    private static int hoverFrame = -2;
    public static Vector4 LastTooltipBounds { get; private set; }
    public static Vector4 LastTooltipAnchor { get; private set; }
    public static double LastTooltipTime { get; private set; }

    public static bool Button(string id, Vector2 size) {
#if GRATIS_PREVIEW
        PreviewBeforeButton?.Invoke(id);
#endif
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0);
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
        var clicked = ImGui.Button(id, size);
#if GRATIS_PREVIEW
        PreviewAfterButton?.Invoke(id);
#endif
        UiMotion.ItemId(id);
        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar();
        return clicked;
    }

    public static void Feedback(ImDrawListPtr list, Vector2 min, Vector2 max, bool selected = false, bool fill = true,
        float? scale = null, float? radius = null) {
        if (!ImGui.IsItemVisible())
            return;
        var s = scale ?? GratisTheme.ChromeScale;
        var r = radius ?? GratisTheme.Radius.Control * s;
        var hovered = UiMotion.Value(UiMotion.Item, 1, ImGui.IsItemHovered() ? 1 : 0, initial: 0);
        var pressed = UiMotion.Value(UiMotion.Item, 2, ImGui.IsItemActive() ? 1 : 0, initial: 0);
        var active = UiMotion.Value(UiMotion.Item, 6, selected ? 1 : 0);
        var focus = UiMotion.Value(UiMotion.Item, 7, ImGui.IsItemFocused() && ImGui.GetIO().NavVisible ? 1 : 0,
            initial: 0);
        if (fill && (hovered > 0 || pressed > 0 || active > 0))
            list.AddRectFilled(min + new Vector2(s), max - new Vector2(s),
                ImGui.GetColorU32(Vector4.Lerp(new Vector4(1, 1, 1, .06f * hovered + .06f * pressed),
                    GratisTheme.WarmAccent with { W = .12f }, active)),
                UiMotion.IsGratis ? GratisTheme.Radius.Inner(r, s) : GratisTheme.ButtonRadius * s);
        if (focus > 0)
            list.AddRect(min + new Vector2(2 * s), max - new Vector2(2 * s),
                ImGui.GetColorU32(GratisTheme.Crystal with { W = focus }),
                UiMotion.IsGratis ? GratisTheme.Radius.Inner(r, 2 * s) : GratisTheme.ButtonRadius * s,
                ImDrawFlags.None, s);
    }

    public static bool IconButton(string id, char glyph, string label, Vector2 size, float? scale = null,
        Vector4? windowBounds = null, float? radius = null) {
        var pos = ImGui.GetCursorScreenPos();
        var clicked = Button(id, size);
        var list = ImGui.GetWindowDrawList();
        var s = scale ?? GratisTheme.ChromeScale;
        using var motion = UiMotion.Control(list, pos, pos + size);
        Feedback(list, pos, pos + size, scale: s, radius: radius);
        GratisIcons.Draw(list, pos + size / 2, glyph, GratisTheme.Secondary, GratisTheme.IconSize * s);
        Tooltip(label, scale: s, windowBounds: windowBounds);
        return clicked;
    }

    public static void Tooltip(string label, string? shortcut = null, float? scale = null,
        Vector4? windowBounds = null, string? meta = null, HintRow[]? rows = null) {
        var focused = ImGui.IsItemFocused() && ImGui.GetIO().NavVisible;
        if (!ImGui.IsItemHovered() && !focused)
            return;
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var bounds = new Vector4(min.X, min.Y, max.X, max.Y);
        var now = ImGui.GetTime();
        var frame = ImGui.GetFrameCount();
        if (!ReferenceEquals(hoverContext, MapDrawContext.Current) || hoverBounds != bounds || hoverFrame < frame - 1) {
            hoverContext = MapDrawContext.Current;
            hoverBounds = bounds;
            hoverBegan = now;
        }
        hoverFrame = frame;
        if (now - hoverBegan < .4)
            return;
        var s = scale ?? GratisTheme.ChromeScale;
        using var style = PopupSurface.Push(PopupFaces.Chrome, scale: s);
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s,
            ChromeFontRole.Body)?.Push();
        var viewport = ImGui.GetWindowViewport();
        var region = windowBounds
            ?? new Vector4(viewport.Pos.X, viewport.Pos.Y, viewport.Pos.X + viewport.Size.X,
                viewport.Pos.Y + viewport.Size.Y);
        var regionMin = new Vector2(region.X, region.Y);
        var regionMax = new Vector2(region.Z, region.W);
        if (meta == null && rows == null && string.IsNullOrEmpty(shortcut) && label.IndexOf('\n') is var split and > 0) {
            // Draw splits at the first newline into a title card, so measure it the same way.
            meta = label[(split + 1)..];
            label = label[..split];
        }
        var text = string.IsNullOrEmpty(shortcut) ? label : label + "  " + shortcut;
        var padding = ImGui.GetStyle().WindowPadding;
        var wrap = MathF.Max(1, MathF.Min(360 * s, regionMax.X - regionMin.X - padding.X * 2));
        var card = meta != null || rows != null;
        var size = (card ? HintCard.Measure(label, meta, rows, wrap, s) : ImGui.CalcTextSize(text, false, wrap))
            + padding * 2;
        LastTooltipBounds = ChromePopoverMath.Place(bounds, size, regionMin, regionMax, 4 * s);
        LastTooltipAnchor = bounds;
        LastTooltipTime = now;
        if (card)
            UiMotion.TooltipCard(label, meta, rows, LastTooltipBounds, wrap, s);
        else
            UiMotion.TooltipText(text, LastTooltipBounds, wrap, s);
    }

    public static string FitLabel(string text, float width) {
        // Half a pixel of tolerance: callers size boxes to the measured text, so equality is the common case.
        if (ImGui.CalcTextSize(text).X <= width + .5f)
            return text;
        const string ellipsis = "\u2026";
        if (ImGui.CalcTextSize(ellipsis).X > width)
            return "";
        var length = text.Length;
        while (length > 0) {
            length--;
            if (length > 0 && char.IsLowSurrogate(text[length]))
                length--;
            if (ImGui.CalcTextSize(text[..length] + ellipsis).X <= width)
                return text[..length] + ellipsis;
        }
        return ellipsis;
    }
}
