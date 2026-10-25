using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif
using Math = global::System.Math;

namespace Gratis.Rendering;

public static class HoverCardDrawing
{
    public static void Draw(string key, string title, string? secondary = null, MarkerCategory? category = null, Classes.MarkerInfo? marker = null)
    {
        var context = MapDrawContext.Current;
        context.HoverCardFrame = ImGui.GetFrameCount();
        var attuned = AetheryteAttunement.Status(context.Config, marker);
        if (attuned.HasValue) secondary = (attuned.Value ? "Attuned" : "Not yet attuned") + (string.IsNullOrWhiteSpace(secondary) ? "" : "\n" + secondary);
        if (marker != null && context.Assistance?.HoverTransport(marker, title) == true) return;
        context.Chrome.Reveal(ImGui.GetTime());
        var scale = GratisTheme.ChromeScale;
        var fade = HoverAnimation.Progress("card/" + key, true);
        var viewport = ImGui.GetWindowViewport();
        var margin = new Vector2(GratisTheme.Inset * scale);
        var available = Vector2.Max(new Vector2(1), viewport.Size - margin * 2);
        var padding = new Vector2(12 * scale);
        var swatch = (context.Config.HoverCardSwatch && category.HasValue ? 24 * scale : 0) + (attuned.HasValue ? 18 * scale : 0);
        var titleSize = 14 * scale; var secondarySize = 12 * scale;
        using var titleFont = context.GetChromeFont(titleSize, ChromeFontRole.Title)?.Push();
        var font = ImGui.GetFont();
        var wrap = Math.Max(1, Math.Min(300 * scale, available.X - padding.X * 2 - swatch));
        var primaryBounds = DrawBinding.MeasureText(titleSize, title, wrap);
        Vector2 secondaryBounds;
        using (context.GetChromeFont(secondarySize, ChromeFontRole.Body)?.Push())
            secondaryBounds = string.IsNullOrEmpty(secondary) ? Vector2.Zero : DrawBinding.MeasureText(secondarySize, secondary, wrap);
        var size = new Vector2(Math.Max(primaryBounds.X, secondaryBounds.X) + swatch, primaryBounds.Y + (secondaryBounds.Y > 0 ? 4 * scale + secondaryBounds.Y : 0)) + padding * 2;
        size = Vector2.Min(size, available);
        var position = ImGui.GetMousePos() + new Vector2(24, 24 + 4 * (1 - fade)) * scale;
        if (position.Y + size.Y > viewport.Pos.Y + viewport.Size.Y - margin.Y)
            position.Y = ImGui.GetMousePos().Y - size.Y - 24 * scale;
        position = Vector2.Clamp(position, viewport.Pos + margin, viewport.Pos + viewport.Size - margin - size);
        var list = ImGui.GetForegroundDrawList();
        DrawBinding.PushFontTexture(list);
        MapWindowDrawing.DrawSurface(list, position, position + size, GratisTheme.CardRadius * scale, fade, surfaceOpacity: 1);
        list.PushClipRect(position, position + size, true);
        var text = position + padding + new Vector2(swatch, 0);
        if (attuned.HasValue) GratisIcons.Draw(list, text + new Vector2(-10, 7) * scale,
            attuned.Value ? GratisIcons.Unlock : GratisIcons.Lock, GratisTheme.Secondary with { W = fade }, 12 * scale);
        if (context.Config.HoverCardSwatch && swatch > 0 && category is { } shape) {
            var center = position + padding + new Vector2(7, 7) * scale;
            if (marker != null && MarkerVisual.UseFlatMarker(marker))
                FlatMarkerDrawing.Symbol(list, center, 7 * scale, marker, fade);
            else if (marker != null) {
                var icon = context.GetIcon(marker.IconId);
                list.AddImage(icon.Handle, center - new Vector2(8 * scale), center + new Vector2(8 * scale),
                    Vector2.Zero, Vector2.One, ImGui.GetColorU32(context.GetIconSetting(marker.IconId).Color with { W = fade }));
            }
            else FlatMarkerDrawing.Shape(list, center, 6 * scale, shape, FlatMarkerDrawing.Color(shape), fade);
        }
        list.AddText(font, titleSize, text, ImGui.GetColorU32(GratisTheme.Primary with { W = fade }), title, wrap);
        if (!string.IsNullOrEmpty(secondary)) {
            using var secondaryFont = context.GetChromeFont(secondarySize, ChromeFontRole.Body)?.Push();
            DrawBinding.PushFontTexture(list);
            list.AddText(ImGui.GetFont(), secondarySize, text + new Vector2(0, primaryBounds.Y + 4 * scale),
                ImGui.GetColorU32(GratisTheme.Secondary with { W = fade }), secondary, wrap);
            DrawBinding.PopTexture(list);
        }
        list.PopClipRect();
        DrawBinding.PopTexture(list);
    }
}
