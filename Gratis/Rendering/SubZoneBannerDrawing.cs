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
        var context = MapDrawContext.Current; var s = context.GlobalScale;
        using var alpha = new ChromeVisibility.AlphaScope(true, banner.Opacity);
        using var font = context.GetChromeFont(12 * s, ChromeFontRole.Chip)?.Push();
        var (min, max) = MapShape.Bounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        var width = MathF.Max(1, max.X - min.X - 48 * s);
        var text = banner.Text;
        while (text.Length > 1 && ImGui.CalcTextSize(text).X > width) text = text[..^1];
        var size = ImGui.CalcTextSize(text) + new Vector2(20, 12) * s;
        var pos = new Vector2((min.X + max.X - size.X) / 2, min.Y + 48 * s);
        if (context.Config.MapCircle) {
            var r = (max.X - min.X) / 2;
            pos.Y = MathF.Max(pos.Y, min.Y + r - MathF.Sqrt(MathF.Max(0, r * r - size.X * size.X / 4)) + 8 * s);
        }
        var list = ImGui.GetWindowDrawList();
        MapWindowDrawing.DrawSurface(list, pos, pos + size, 6 * s);
        list.AddText(pos + new Vector2(10, 6) * s, ImGui.GetColorU32(GratisTheme.Primary), text);
    }
}
