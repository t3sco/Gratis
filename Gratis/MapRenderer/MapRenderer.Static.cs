using System.Linq;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.Utility;
using Gratis.Extensions;

namespace Gratis.MapRenderer;

public partial class MapRenderer {
    private unsafe void DrawStaticMapMarkers() {
        foreach (var index in Enumerable.Range(0, AgentMap.Instance()->MapMarkerCount)) {
            ref var marker = ref AgentMap.Instance()->MapMarkers[index];

            if (marker.MapMarker.IconId is 0)
                continue;

            marker.Draw(DrawPosition, Scale);
        }
    }

    private unsafe void DrawStaticTextMarkers() {
        var gratis = Rendering.MapDrawContext.Current.Config.IsGratis;
        for (var pass = gratis ? 0 : 1; pass < 2; pass++) {
            foreach (var index in Enumerable.Range(0, AgentMap.Instance()->MapMarkerCount)) {
                ref var marker = ref AgentMap.Instance()->MapMarkers[index];

                if (marker.MapMarker.IconId is not 0)
                    continue;
                if (marker.MapMarker.Index is 0)
                    continue;
                if (marker.MapMarker.SubtextOrientation is 0)
                    continue;
                if (gratis && (marker.MapMarker.SubtextStyle is 1) != (pass == 0))
                    continue;

                marker.Draw(DrawPosition, Scale);
            }
        }
    }

    // Link markers (door, map and instance links) with the ids the game stores, to see what ties a door to its interior.
    public unsafe string LinkDiagnostics() {
        var agent = AgentMap.Instance();
        var rows = new global::System.Collections.Generic.List<string>();
        for (var index = 0; index < agent->MapMarkerCount && rows.Count < 12; index++) {
            ref var marker = ref agent->MapMarkers[index];
            if (marker.MapMarker.IconId is 0 || marker.DataType is not (1 or 2))
                continue;
            rows.Add($"#{index} type={marker.DataType} key={marker.DataKey} icon={marker.MapMarker.IconId} "
                + $"\"{marker.MapMarker.Subtext.AsDalamudSeString().TextValue}\" at {marker.MapMarker.X / 16f:0},{marker.MapMarker.Y / 16f:0}");
        }
        return $"links map={agent->SelectedMapId} markers={agent->MapMarkerCount} " + string.Join(" | ", rows);
    }
}
