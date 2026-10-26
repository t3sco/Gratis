using System.Linq;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
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
}