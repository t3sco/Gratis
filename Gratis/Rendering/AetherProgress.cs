using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gratis.Rendering;

// One field aether current: the EObj that holds it and where the level data places it (world X/Z).
public readonly record struct AetherCurrentPoint(uint CurrentId, uint EObjId, uint MapId, uint TerritoryId, uint SetId,
    float X, float Z);

// A zone's set (AetherCurrentCompFlgSet) and the currents found in the world. Quest-reward currents are excluded.
public readonly record struct AetherZoneSet(uint SetId, uint TerritoryId, uint[] FieldCurrentIds);

public readonly record struct AetherSetRow(uint SetId, uint TerritoryId, (uint CurrentId, bool IsQuest)[] Currents);
public readonly record struct AetherEObjRow(uint EObjId, uint DataId);
public readonly record struct AetherLevelRow(uint LevelId, uint ObjectId, uint MapId, uint TerritoryId, float X, float Z);

public sealed record AetherIndex(AetherZoneSet[] Sets, AetherCurrentPoint[] Points, uint[] Unmatched) {
    public static readonly AetherIndex Empty = new([], [], []);
}

public readonly record struct AetherZoneProgress(int Collected, int Total, bool ZoneComplete) {
    // Hidden when the zone has no field currents, is flagged complete, or every field current is already collected.
    public bool Visible => Total > 0 && !ZoneComplete && Collected < Total;
}

public static class AetherProgress {
    // Joins the sheets: AetherCurrentCompFlgSet -> AetherCurrent (no quest = a field current) -> EObj.Data == current
    // row id -> Level.Object == EObj row id -> world position. Pure so a stub-row test can pin the rules.
    public static AetherIndex Build(IEnumerable<AetherSetRow> sets, IEnumerable<AetherEObjRow> eobjs,
        IEnumerable<AetherLevelRow> levels) {
        var zoneSets = new List<AetherZoneSet>();
        var field = new Dictionary<uint, uint>(); // current id -> set id
        var setTerritory = new Dictionary<uint, uint>();
        foreach (var set in sets) {
            var ids = set.Currents.Where(c => c.CurrentId != 0 && !c.IsQuest).Select(c => c.CurrentId).Distinct().ToArray();
            if (ids.Length == 0)
                continue;
            zoneSets.Add(new(set.SetId, set.TerritoryId, ids));
            setTerritory[set.SetId] = set.TerritoryId;
            foreach (var id in ids)
                field.TryAdd(id, set.SetId);
        }
        // EObj row id -> current id, only for EObj rows whose Data is one of the field currents.
        var holder = new Dictionary<uint, uint>();
        foreach (var eobj in eobjs)
            if (field.ContainsKey(eobj.DataId))
                holder.TryAdd(eobj.EObjId, eobj.DataId);
        var points = new List<AetherCurrentPoint>();
        var placed = new HashSet<uint>();
        foreach (var level in levels) {
            if (!holder.TryGetValue(level.ObjectId, out var current) || !placed.Add(current))
                continue;
            var set = field[current];
            points.Add(new(current, level.ObjectId, level.MapId, level.TerritoryId != 0 ? level.TerritoryId : setTerritory[set],
                set, level.X, level.Z));
        }
        var unmatched = field.Keys.Where(id => !placed.Contains(id)).OrderBy(id => id).ToArray();
        return new(zoneSets.ToArray(), points.ToArray(), unmatched);
    }

    // Progress for one zone set; `unlocked` takes the AetherCurrent row id, exactly as the native call does.
    public static AetherZoneProgress Evaluate(IReadOnlyList<uint> fieldCurrentIds, Func<uint, bool> unlocked,
        bool zoneComplete) {
        var collected = 0;
        foreach (var id in fieldCurrentIds)
            if (unlocked(id))
                collected++;
        return new(collected, fieldCurrentIds.Count, zoneComplete);
    }

    public static string Line(AetherZoneProgress progress) => progress.Visible
        ? "Aether currents " + progress.Collected.ToString(CultureInfo.InvariantCulture) + "/"
            + progress.Total.ToString(CultureInfo.InvariantCulture)
        : "";

    // The location card hover line; one call site keeps the card free of aether rules.
    public static string Hint(ILocationMenuSource source) => source.AetherHint ?? "";

    // The uncollected field currents on one map, in a stable order.
    public static AetherCurrentPoint[] Uncollected(IEnumerable<AetherCurrentPoint> points, uint mapId,
        Func<uint, bool> unlocked) =>
        points.Where(p => p.MapId == mapId && !unlocked(p.CurrentId)).OrderBy(p => p.CurrentId).ToArray();

    public static string Summary(AetherIndex index) =>
        $"sets={index.Sets.Length} fieldCurrents={index.Sets.Sum(s => s.FieldCurrentIds.Length)} "
        + $"positioned={index.Points.Length} unmatched={index.Unmatched.Length}";
}
