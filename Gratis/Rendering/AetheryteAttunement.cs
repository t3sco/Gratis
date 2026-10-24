using System.Collections.Generic;
using Gratis.Classes;
using Gratis.Controllers;

namespace Gratis.Rendering;

public static class AetheryteAttunement
{
    public static PlaceEntry? Find(IEnumerable<PlaceEntry> places, MarkerIconFamily family, uint dataKey, uint map)
    {
        if (dataKey == 0 || family is not (MarkerIconFamily.Aetheryte or MarkerIconFamily.Aethernet)) return null;
        PlaceEntry? candidate = null; var ambiguous = false;
        foreach (var place in places) {
            if (place.Kind != PlaceKind.Aetheryte || (family == MarkerIconFamily.Aetheryte
                ? place.AetheryteId != dataKey : place.AethernetNameId != dataKey)) continue;
            if (place.MapId == map) return place;
            if (candidate is not null && candidate.AetheryteId != place.AetheryteId) ambiguous = true;
            candidate = place;
        }
        return ambiguous ? null : candidate;
    }
    public static bool? Status(MapDrawingConfig config, MarkerInfo? marker) =>
        config.IsGratis && config.AetheryteAttunementStyle && marker is not null &&
        MarkerIcon.Family(marker) is MarkerIconFamily.Aetheryte or MarkerIconFamily.Aethernet ? marker.AetheryteAttuned : null;
    public static float Opacity(MapDrawingConfig config, MarkerInfo marker, bool loud) =>
        Status(config, marker) == false && !marker.IsSpotlighted ? (loud ? .72f : .55f) : 1;
}
