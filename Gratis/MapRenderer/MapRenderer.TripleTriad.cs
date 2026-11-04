using Gratis.Classes;
using Gratis.Controllers;

namespace Gratis.MapRenderer;

public partial class MapRenderer {
    private void DrawTripleTriadMarkers() {
        foreach (var point in TripleTriadGame.Visible()) {
            // Reuse both the marker and its text delegates; no per-frame allocations.
            var marker = point.Marker;
            marker.Position = (point.Row.World * DrawHelpers.GetMapScaleFactor()
                - DrawHelpers.GetMapOffsetVector() + DrawHelpers.GetMapCenterOffsetVector()) * Scale;
            marker.Offset = DrawPosition;
            marker.Scale = Scale;
            DrawHelpers.DrawMapMarker(marker);
        }
    }
}
