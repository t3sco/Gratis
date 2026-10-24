using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiLib.Extensions;
using Gratis.Classes;
using Gratis.Extensions;

namespace Gratis.MapRenderer;

public partial class MapRenderer
{
    private unsafe void DrawTemporaryMarkers()
    {
        if (AgentMap.Instance()->SelectedMapSub != AgentMap.Instance()->SelectedMapId) return;

        var validMarkers = new Span<TempMapMarker>(Unsafe.AsPointer(ref AgentMap.Instance()->TempMapMarkers[0]), AgentMap.Instance()->TempMapMarkerCount);
        var iconGroups = validMarkers.ToArray().GroupBy(markers => new Vector2(markers.MapMarker.X, markers.MapMarker.Y));

        foreach (var group in iconGroups) {
            var markerCopy = group.First();

            var correctIconId = group.FirstOrNull(marker => marker.MapMarker.IconId is not (60493 or 0));
            markerCopy.MapMarker.IconId = correctIconId?.MapMarker.IconId ?? markerCopy.MapMarker.IconId;
            
            if (markerCopy.MapMarker.IconId == 0 && group.Count() == 2)
            {
                if (markerCopy.Type == 4 && group.Last() is { Type: 6, MapMarker.IconId: 0 })
                {
                    markerCopy.MapMarker.IconId = DrawHelpers.QuestionMarkIcon;
                }
            }

            markerCopy.MapMarker.Scale = group.Max(marker => marker.MapMarker.Scale);

            markerCopy.Draw(DrawPosition, Scale);
        }
    }
}