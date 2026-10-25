using System;
using System.Numerics;
using Gratis.Classes;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class SubZoneBannerDrawing
{
    public static void Draw(SubZoneBanner banner)
    {
        if (banner.Opacity <= 0) return;
        var context = MapDrawContext.Current; var s = GratisTheme.ChromeScale;
        using var alpha = new ChromeVisibility.AlphaScope(true, banner.Opacity);
        using var font = context.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Chip)?.Push();
        var (min, max) = MapShape.Bounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        var width = MathF.Max(1, max.X - min.X - 48 * s);
        var text = banner.Text;
        while (text.Length > 1 && ImGui.CalcTextSize(text).X > width) text = text[..^1];
        var size = new Vector2(ImGui.CalcTextSize(text).X + 24 * s, GratisTheme.ControlHeight * s);
        var bounds = ChromeLayout.Place("zone-banner", size, ChromeAnchor.TopCenter);
        if (bounds == Vector4.Zero) return;
        var pos = ChromeLayout.Position(bounds);
        var list = ImGui.GetWindowDrawList();
        MapWindowDrawing.DrawSurface(list, pos, pos + size, GratisTheme.CardRadius * s);
        list.AddText(pos + new Vector2(12 * s, (size.Y - ImGui.GetFontSize()) / 2), ImGui.GetColorU32(GratisTheme.Primary), text);
    }
}
