using System;
using Gratis.Classes;

namespace Gratis.Rendering;

public static class MarkerVisual
{
    public static float Scale(MarkerInfo marker)
    {
        var config = MapDrawContext.Current.Config;
        var settings = config.Markers;
        if (config.IsGratis && settings.ScaleWithZoom)
            return Math.Clamp(marker.Scale, Math.Clamp(settings.MinimumScale, .1f, 3),
                Math.Clamp(settings.MaximumScale, Math.Clamp(settings.MinimumScale, .1f, 3), 3));
        return config.ScaleWithZoom ? marker.Scale : 1;
    }

    public static float Alpha(MarkerInfo marker) => MapDrawContext.Current.Config is { IsGratis: true } config &&
        config.Markers.DimIrrelevant && !marker.IsRelevant ? Math.Clamp(config.Markers.IrrelevantAlpha, .1f, 1) : 1;
}
