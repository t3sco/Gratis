using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gratis.Rendering;

// One record of the game's territory-wide event marker list (AgentMap.EventMarkers), reduced to what decides
// whether it belongs on the map being viewed. Height is the world Y the game stores; it is read, never used to hide.
public readonly record struct EventMarkerRow(uint ObjectiveId, uint MapId, uint TerritoryId, uint IconId, float Height,
    byte Flags);

// Quest objective markers (ObjectiveId != 0) come from a list the game builds for the whole territory, and every
// record names the Map row it was authored for. A territory with several floors has one Map row per floor, so a
// marker for another floor names another Map row. The viewed map is the one Gratis was asked to draw.
//
// What this does NOT do: tell floors apart inside a single Map row. Nothing in the marker, the Map sheet or
// TerritoryType carries a vertical range, so a Y threshold would be a guess. Height is only reported (Summary).
public static class EventMarkerScope {
    // An id of 0 means the game did not say, so the marker is drawn as before rather than guessed away.
    public static bool Shows(uint objectiveId, uint markerMapId, uint viewedMapId) =>
        objectiveId == 0 || markerMapId == 0 || viewedMapId == 0 || markerMapId == viewedMapId;

    public static string Summary(IEnumerable<EventMarkerRow> rows, uint viewedMapId, uint viewedTerritoryId,
        int maxRows = 8) {
        var list = rows.ToList();
        var quests = list.Where(r => r.ObjectiveId != 0).ToList();
        var hidden = quests.Count(r => !Shows(r.ObjectiveId, r.MapId, viewedMapId));
        var text = new StringBuilder($"events viewedMap={viewedMapId} territory={viewedTerritoryId} total={list.Count} "
            + $"quest={quests.Count} hiddenOtherMap={hidden}");
        var shown = quests.Where(r => Shows(r.ObjectiveId, r.MapId, viewedMapId)).Select(r => r.Height)
            .Distinct().Count();
        text.Append($" shownQuestHeights={shown}");
        foreach (var row in quests.Take(maxRows))
            text.Append($" | obj={row.ObjectiveId} map={row.MapId} terr={row.TerritoryId} icon={row.IconId} "
                + $"y={row.Height:0.#} flags={row.Flags} "
                + (Shows(row.ObjectiveId, row.MapId, viewedMapId) ? "drawn" : "other-map"));
        if (quests.Count > maxRows)
            text.Append($" | +{quests.Count - maxRows} more");
        return text.ToString();
    }
}
