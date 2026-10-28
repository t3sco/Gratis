using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class SubZoneBannerDrawing {
    private static readonly ActionLabelCache placeLabel = new(), zoneLabel = new();
    public static void Draw(SubZoneBanner banner) {
        if (banner.Opacity <= 0)
            return;
        var context = MapDrawContext.Current;
        var s = GratisTheme.ChromeScale;
        using var alpha = new ChromeVisibility.AlphaScope(true, banner.Opacity);
        using var font = context.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push();
        var (min, max) = ChromeLayout.SafeBounds();
        var width = MathF.Max(1, max.X - min.X - 48 * s);
        var text = placeLabel.Fit(banner.Text, width);
        var textWidth = ImGui.CalcTextSize(text).X;
        string zone;
        using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push()) {
            zone = zoneLabel.Fit(banner.Zone, width);
            textWidth = MathF.Max(textWidth, ImGui.CalcTextSize(zone).X);
        }
        var stacked = zone.Length > 0;
        var size = new Vector2(textWidth + 48 * s, (stacked ? 52 : GratisTheme.ControlHeight) * s);
        var bounds = ChromeLayout.Place("zone-banner", size, ChromeAnchor.TopCenter);
        if (bounds == Vector4.Zero)
            return;
        var pos = ChromeLayout.Position(bounds);
        var list = ImGui.GetWindowDrawList();
        using var motion = new UiMotion.VisualScope(list, pos, 1,
            UiMotion.Full ? new Vector2(0, (1 - banner.Opacity) * 6 * s) : Vector2.Zero);
        MapWindowDrawing.DrawSurface(list, pos, pos + size, GratisTheme.Radius.Card * s);
        GratisIcons.Draw(list, pos + new Vector2(18 * s, size.Y / 2), GratisIcons.Compass, GratisTheme.WarmAccent,
            GratisTheme.IconSize * s);
        if (stacked)
            using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push())
                list.AddText(pos + new Vector2(36, 7) * s, ImGui.GetColorU32(GratisTheme.Secondary), zone);
        list.AddText(pos + new Vector2(36 * s, stacked ? 28 * s : (size.Y - ImGui.GetFontSize()) / 2),
            ImGui.GetColorU32(GratisTheme.Primary), text);
    }
}