using System;
using System.Numerics;
using Gratis.Classes;

namespace Gratis.Rendering;

public static class MarkerVisual
{
    public static bool UseFlatIcons => MapDrawContext.Current.Config is { IsGratis: true, MarkerStyle: 1 } config &&
        config.Markers.IconStyle != MarkerIconStyle.Game;

    public static bool UseFlatMarker(MarkerInfo marker) => UseFlatIcons ||
        MapDrawContext.Current.Config.IsGratis && PlayerDrawing.IsPlayerMarker(marker);

    public static float Scale(MarkerInfo marker)
    {
        var context = MapDrawContext.Current;
        var config = context.Config;
        var settings = config.Markers;
        if (!config.IsGratis) return config.ScaleWithZoom ? marker.Scale : 1;
        var spotlight = context.Spotlight;
        var lensScale = spotlight?.Matches(marker) == true ? 1 : context.Visibility.Visual(marker).Y;
        var zoomScale = settings.ScaleWithZoom ? Math.Clamp(marker.Scale, Math.Clamp(settings.MinimumScale, .1f, 3),
            Math.Clamp(settings.MaximumScale, Math.Clamp(settings.MinimumScale, .1f, 3), 3)) : 1;
        return lensScale * zoomScale * (spotlight?.MarkerBreath(marker) ?? 1);
    }

    public static float Alpha(MarkerInfo marker)
    {
        var context = MapDrawContext.Current;
        var config = context.Config;
        if (!config.IsGratis || context.Spotlight?.Matches(marker) == true) return 1;
        return context.Visibility.Visual(marker).X * (config.Markers.DimIrrelevant && !marker.IsRelevant && !MarkerRelevance.IsProtected(marker)
            ? Math.Clamp(config.Markers.IrrelevantAlpha, .1f, 1) : 1);
    }

    public static Vector2 HalfSize(MarkerInfo marker)
    {
        var context = MapDrawContext.Current;
        var config = context.Config;
        var scale = Scale(marker);
        var setting = context.GetIconSetting(marker.IconId);
        if (UseFlatMarker(marker)) {
            var size = config.FlatMarkerSize * (config.IconScale / .5f) * setting.Scale * context.GlobalScale * scale;
            return new Vector2(MathF.Max(size * .6f + 3 * context.GlobalScale, 8 * context.GlobalScale));
        }
        var iconScale = config.IconScale;
        if (marker.IconId is 60401 or 60402) scale *= 2;
        if (DrawHelpers.IsRegionIcon(marker.IconId)) {
            if (!config.IsGratis || !config.Markers.ScaleWithZoom) scale = marker.Scale;
            iconScale = .42f;
        }
        return context.GetIcon(marker.IconId).Size * scale * iconScale * setting.Scale / 2 + new Vector2(2 * context.GlobalScale);
    }
}
