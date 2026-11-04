using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Classes;
using Gratis.Controllers;
using Gratis.Rendering;

namespace Gratis.MapRenderer;

public partial class MapRenderer {
    private readonly AetherMarkerCache aetherMarkers = new();

    // Off by default. Draws the uncollected field currents of the viewed map from the level data, so ones outside
    // object-table range show too. Collected ones are not drawn.
    private unsafe void DrawAetherCurrentMarkers() {
        var config = Rendering.MapDrawContext.Current.Config;
        if (!config.IsGratis || !config.Markers.ShowAetherCurrents || Service.ClientState.IsPvP)
            return;
        var points = AetherCurrentsGame.Uncollected();
        aetherMarkers.Update(AgentMap.Instance()->SelectedMapId, points);
        for (var i = 0; i < points.Length; i++) {
            var point = points[i];
            if (!aetherMarkers.ShouldDraw(point.EObjId)) continue;
            var marker = aetherMarkers.Markers[i];
            marker.Position = (new Vector2(point.X, point.Z) * DrawHelpers.GetMapScaleFactor()
                - DrawHelpers.GetMapOffsetVector() + DrawHelpers.GetMapCenterOffsetVector()) * Scale;
            marker.Offset = DrawPosition;
            marker.Scale = Scale;
            DrawHelpers.DrawMapMarker(marker);
        }
    }
}
