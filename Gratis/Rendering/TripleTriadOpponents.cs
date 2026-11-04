using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Gratis.Controllers;

namespace Gratis.Rendering;

public readonly record struct TripleTriadOpponent(uint Id, uint NpcId, string Name, uint MapId,
    uint TerritoryId, Vector2 World, uint[]? PreviousQuests = null);

public static class TripleTriadOpponents {
    // Real sheets have identical TripleTriad/TripleTriadResident ID sets, also used by ENpcData.
    // Resident.Order is not the native API key; preserve the complete event row ID.
    public static uint ResidentIdFromTripleTriadRowId(uint rowId) => rowId;
    public static bool IsBeaten(uint rowId, Func<uint, bool> read) =>
        read(ResidentIdFromTripleTriadRowId(rowId));

    public static TripleTriadOpponent[] Filter(IEnumerable<TripleTriadOpponent> rows, bool enabled,
        uint map, Func<uint, bool> beaten, Func<uint, bool> questComplete) => !enabled || map == 0 ? []
        : rows.Where(r => r.Id != 0 && r.NpcId != 0 && r.MapId == map && r.TerritoryId != 0
            && !string.IsNullOrWhiteSpace(r.Name) && float.IsFinite(r.World.X) && float.IsFinite(r.World.Y)
            && !beaten(r.Id) && (r.PreviousQuests == null || r.PreviousQuests.All(q => q == 0 || questComplete(q))))
        .OrderBy(r => r.Name, StringComparer.Ordinal).ThenBy(r => r.Id).ThenBy(r => r.NpcId).ToArray();

    public static PlaceEntry Entry(TripleTriadOpponent row) => new($"triad:{row.Id}:{row.NpcId}",
        PlaceKind.Marker, "Triple Triad: " + row.Name, "Not yet beaten", row.MapId, row.TerritoryId,
        row.World, Aliases: "triple triad cards opponent") { Icon = 71103 };
}
