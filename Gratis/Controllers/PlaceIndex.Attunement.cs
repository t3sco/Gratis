using System.Collections.Generic;
using Gratis.Rendering;

namespace Gratis.Controllers;

public sealed partial class PlaceIndexGame
{
    private readonly Dictionary<(MarkerIconFamily Family, uint Key, uint Map), PlaceEntry?> attunementMarkers = [];
    private int attunementRevision = -1;
    private int unresolvedAttunement;
    public bool? MarkerAttunement(uint dataKey, MarkerIconFamily family)
    {
        if (dataKey == 0 || family is not (MarkerIconFamily.Aetheryte or MarkerIconFamily.Aethernet) || !Service.ClientState.IsLoggedIn) return null;
        Refresh();
        if (attunementRevision != index.Revision) {
            attunementMarkers.Clear(); attunementRevision = index.Revision; unresolvedAttunement = 0;
        }
        var key = (family, dataKey, Context.MapId);
        if (!attunementMarkers.TryGetValue(key, out var entry)) {
            entry = AetheryteAttunement.Find(index.Entries, family, dataKey, Context.MapId);
            if (attunementMarkers.Count >= 512) attunementMarkers.Clear();
            attunementMarkers[key] = entry;
            if (entry is null) unresolvedAttunement++;
        }
        return entry is not null && access.TryGetValue(entry.AetheryteId, out var state) ? state.Attuned : null;
    }
    public string AttunementDiagnostics() => $"style={System.SystemConfig.AetheryteAttunementStyle} map={Context.MapId} " +
        $"indexBuilding={index.Building} revision={attunementRevision} markerMappings={attunementMarkers.Count} unresolved={unresolvedAttunement}; unknown markers retain normal styling";
}
