using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Rendering;

namespace Gratis.MapRenderer;

public unsafe partial class MapRenderer {
    public bool BeginSpotlight(SpotlightRequest request) => frame.BeginSpotlight(request);

    public static (Vector2? Target, uint? Icon) QuestSpotlightTarget(AgentMap* agent, uint mapId) {
        if (agent->TempMapMarkerCount <= 0 || agent->SelectedMapSub != mapId)
            return (null, null);
        var first = agent->TempMapMarkers[0].MapMarker;
        for (var index = 0; index < agent->TempMapMarkerCount; index++) {
            var marker = agent->TempMapMarkers[index].MapMarker;
            if (marker.X != first.X || marker.Y != first.Y || marker.IconId is 0 or 60493)
                continue;
            return (new Vector2(marker.X, marker.Y) / 16f, marker.IconId);
        }
        return (null, null);
    }
}