using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Classes;

namespace Gratis.Extensions;

public static class TempMapMarkerExtensions {
    public static void Draw(this TempMapMarker marker, Vector2 offset, float scale) {
        DrawHelpers.DrawMapMarker(new MarkerInfo {
            Position = (new Vector2(marker.MapMarker.X, marker.MapMarker.Y) / 16.0f * DrawHelpers.GetMapScaleFactor()
                + DrawHelpers.GetCombinedOffsetVector())
                * scale,
            Offset = offset,
            Source = Rendering.MapMarkerSource.NativeTemporary,
            Scale = scale,
            IconId = marker.MapMarker.IconId,
            IsDynamicMarker = true,
            Radius = marker.MapMarker.Scale,
            PrimaryText = () => marker.TooltipText.ToString(),
        });
    }
}