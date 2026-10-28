using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class PaletteRowDrawing {
    internal static void TextPair(ImDrawListPtr list, Vector2 min, float width, string primary, string trailing,
        float scale, PaletteRowNames labels, bool secondary = false, bool keepTrailing = false) {
        var context = MapDrawContext.Current;
        width = MathF.Max(0, width);
        float trailingWidth;
        using (context.GetChromeFont(GratisTheme.SmallTextSize * scale, ChromeFontRole.Body)?.Push()) {
            trailingWidth = ImGui.CalcTextSize(trailing).X;
            if (trailingWidth + (secondary && !keepTrailing ? MathF.Max(64 * scale, ImGui.CalcTextSize(primary).X) : 64 * scale)
                + 12 * scale > width) trailingWidth = 0;
            if (trailingWidth > 0)
                TextDrawing.Draw(list, min + new Vector2(width - trailingWidth, secondary ? 0 : 2 * scale),
                    ImGui.GetColorU32(GratisTheme.Secondary), trailing);
        }
        using (context.GetChromeFont((secondary ? GratisTheme.SmallTextSize : GratisTheme.TextSize) * scale,
            ChromeFontRole.Body)?.Push())
            TextDrawing.Draw(list, min, ImGui.GetColorU32(secondary ? GratisTheme.Secondary : GratisTheme.Primary),
                labels.Fit(primary, MathF.Max(0, width - (trailingWidth > 0 ? trailingWidth + 12 * scale : 0)), secondary));
    }

    public static void Background(ImDrawListPtr list, Vector2 min, Vector2 size, Vector4 accent,
        bool active, bool hovered, float scale) => list.AddRectFilled(min, min + size,
            ImGui.GetColorU32(active || hovered
                ? accent with { W = active ? .17f : .08f } : new Vector4(1, 1, 1, .025f)),
            GratisTheme.Radius.Control * scale);

    public static void Pill(ImDrawListPtr list, Vector2 min, string label, Vector4 ink, float scale) {
        global::Gratis.Rendering.Pill.Draw(list, min, label, PillFaces.PaletteRow, scale, ink);
    }
}

internal sealed class PlaceTooltip {
    private readonly Action draw;
    private string title = "", detail = "", context = "";
    private float scale, width;
    public PlaceTooltip() => draw = Draw;

    public void Show(string name, string secondary, string extra = "") {
        title = name;
        detail = secondary;
        context = extra;
        scale = GratisTheme.ChromeScale;
        var host = MapDrawContext.Current;
        float naturalWidth;
        using (host.GetChromeFont(GratisTheme.TextSize * scale, ChromeFontRole.Body)?.Push())
            naturalWidth = ImGui.CalcTextSize(title).X;
        using (host.GetChromeFont(GratisTheme.SmallTextSize * scale, ChromeFontRole.Body)?.Push())
            naturalWidth = MathF.Max(naturalWidth, MathF.Max(ImGui.CalcTextSize(detail).X, ImGui.CalcTextSize(context).X));
        width = MathF.Max(1, MathF.Min(naturalWidth, MathF.Min(320 * scale, ImGui.GetWindowViewport().Size.X - 40 * scale)));
        float height;
        using (host.GetChromeFont(GratisTheme.TextSize * scale, ChromeFontRole.Body)?.Push())
            height = ImGui.CalcTextSize(title, false, width).Y;
        using (host.GetChromeFont(GratisTheme.SmallTextSize * scale, ChromeFontRole.Body)?.Push()) {
            if (detail.Length > 0) height += ImGui.CalcTextSize(detail, false, width).Y + 4 * scale;
            if (context.Length > 0) height += ImGui.CalcTextSize(context, false, width).Y + 4 * scale;
        }
        UiMotion.TooltipContent(draw, new(width + 24 * scale, height + 24 * scale), scale);
    }

    private void Draw() {
        var host = MapDrawContext.Current;
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
        using (host.GetChromeFont(GratisTheme.TextSize * scale, ChromeFontRole.Body)?.Push())
            ImGui.TextColored(GratisTheme.Primary, title);
        using (host.GetChromeFont(GratisTheme.SmallTextSize * scale, ChromeFontRole.Body)?.Push()) {
            if (detail.Length > 0) ImGui.TextColored(GratisTheme.Secondary, detail);
            if (context.Length > 0) ImGui.TextColored(GratisTheme.Secondary, context);
        }
        ImGui.PopTextWrapPos();
    }
}
