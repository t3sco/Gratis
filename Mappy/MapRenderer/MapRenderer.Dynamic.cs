using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Extensions;
using Mappy.Extensions;

namespace Mappy.MapRenderer;

public partial class MapRenderer
{
    private unsafe void DrawDynamicMarkers()
    {
        var iconGroups = AgentMap.Instance()->EventMarkers.GroupBy(markers => (markers.DataId, markers.Position));

        foreach (var group in iconGroups) {
            var markerCopy = group.First();

            var correctIconId = group.FirstOrNull(marker => marker.IconId is not 60493);
            markerCopy.IconId = correctIconId?.IconId ?? markerCopy.IconId;

            markerCopy.Radius = group.Max(marker => marker.Radius);

            if (HousingManager.Instance()->CurrentTerritory is not null) {
                markerCopy.Radius = 0.0f;
            }

            markerCopy.Draw(DrawPosition, Scale);
        }
    }
}