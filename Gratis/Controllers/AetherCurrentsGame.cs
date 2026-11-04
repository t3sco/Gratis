using System;
using System.Linq;
using System.Threading;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Rendering;
using Lumina.Excel.Sheets;

namespace Gratis.Controllers;

// Game lookups for aether currents. Sheet joins run once on a worker thread into immutable records; PlayerState
// (a native pointer) is only read on the UI thread, and results are cached for a second.
public static class AetherCurrentsGame {
    private static readonly SheetIndexWorker<AetherIndex> loader = new();
    public static void Dispose() => loader.Dispose();
    private static long lineSecond = long.MinValue, pointSecond = long.MinValue;
    private static uint lineTerritory, pointMap;
    private static string line = "";
    private static AetherCurrentPoint[] points = [];

    public static AetherIndex Index => loader.Value ?? AetherIndex.Empty;
    public static bool Ready => loader.Value != null;
    public static void Ensure() => loader.Ensure(Build);

    private static AetherIndex Build(CancellationToken token) {
        var sets = Service.DataManager.GetExcelSheet<AetherCurrentCompFlgSet>()
            .Where(_ => { token.ThrowIfCancellationRequested(); return true; })
            .Select(s => new AetherSetRow(s.RowId, s.Territory.RowId,
                s.AetherCurrents.Where(c => c.RowId != 0 && c.ValueNullable != null)
                    .Select(c => (c.RowId, c.Value.Quest.RowId != 0)).ToArray())).ToArray();
        var wanted = sets.SelectMany(s => s.Currents).Select(c => c.CurrentId).ToHashSet();
        var eobjs = Service.DataManager.GetExcelSheet<EObj>()
            .Where(e => { token.ThrowIfCancellationRequested(); return wanted.Contains(e.Data.RowId); })
            .Select(e => new AetherEObjRow(e.RowId, e.Data.RowId)).ToArray();
        var held = eobjs.Select(e => e.EObjId).ToHashSet();
        var levels = Service.DataManager.GetExcelSheet<Level>()
            .Where(l => { token.ThrowIfCancellationRequested(); return held.Contains(l.Object.RowId); })
            .Select(l => new AetherLevelRow(l.RowId, l.Object.RowId, l.Map.RowId, l.Territory.RowId, l.X, l.Z)).ToArray();
        return AetherProgress.Build(sets, eobjs, levels);
    }

    private static bool Available => System.SystemConfig.IsGratis && Service.ClientState.IsLoggedIn
        && !Service.ClientState.IsPvP;

    // Native call takes the AetherCurrent row id. Verified for indexing in game with `/gratis aether`.
    private static unsafe bool Unlocked(uint currentId) =>
        PlayerState.Instance() is var state && state != null && state->IsAetherCurrentUnlocked(currentId);

    private static unsafe AetherZoneProgress Progress(AetherZoneSet set) =>
        AetherProgress.Evaluate(set.FieldCurrentIds, Unlocked,
            PlayerState.Instance() is var state && state != null && state->IsAetherCurrentZoneComplete(set.SetId));

    // "Aether currents 3/4" for the selected map's zone, or "" when there is nothing to report.
    public static unsafe string ZoneLine() {
        if (!Available)
            return "";
        Ensure();
        var territory = AgentMap.Instance()->SelectedTerritoryId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now == lineSecond && territory == lineTerritory)
            return line;
        lineSecond = now;
        lineTerritory = territory;
        var sets = Index.Sets;
        line = "";
        foreach (var set in sets)
            if (set.TerritoryId == territory) {
                line = AetherProgress.Line(Progress(set));
                break;
            }
        return line;
    }

    // Uncollected field currents on the selected map; refreshed once a second, never per marker.
    public static unsafe AetherCurrentPoint[] Uncollected() {
        if (!Available)
            return [];
        Ensure();
        var map = AgentMap.Instance()->SelectedMapId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now == pointSecond && map == pointMap)
            return points;
        pointSecond = now;
        pointMap = map;
        return points = AetherProgress.Uncollected(Index.Points, map, Unlocked);
    }

    // /gratis aether: whether the sheet join found a position for every field current, and whether the native
    // unlock calls agree with the raw unlock bits for the current zone.
    public static unsafe string Diagnostics() {
        Ensure();
        if (loader.Error is { } error)
            return "aether index failed: " + error;
        if (!Ready)
            return "aether index still building; run again in a moment";
        var all = Index;
        var agent = AgentMap.Instance();
        var map = agent->SelectedMapId;
        var territory = agent->SelectedTerritoryId;
        var state = PlayerState.Instance();
        var text = $"{AetherProgress.Summary(all)} map={map} territory={territory}";
        if (all.Unmatched.Length > 0)
            text += " unmatchedIds=" + string.Join(",", all.Unmatched.Take(8));
        var set = all.Sets.FirstOrDefault(s => s.TerritoryId == territory);
        if (set.FieldCurrentIds is not { Length: > 0 })
            return text + "; no aether current set for this territory";
        var apiCollected = set.FieldCurrentIds.Count(Unlocked);
        var raw = 0;
        var rawTotal = 0;
        if (state != null) {
            var bits = state->UnlockedAetherCurrentsBitArray;
            // Raw bit index guess: position of the row in the AetherCurrent sheet (first row id 2818048).
            rawTotal = (int)state->UnlockedAetherCurrentsBitArray.PopCount;
            var first = Service.DataManager.GetExcelSheet<AetherCurrent>().First().RowId;
            foreach (var id in set.FieldCurrentIds) {
                var position = (int)(id - first);
                if (position >= 0 && position < bits.BitCount && bits[position])
                    raw++;
            }
        }
        var zonePoints = all.Points.Where(p => p.TerritoryId == territory).ToArray();
        var here = all.Points.Count(p => p.MapId == map);
        var wrongTerritory = all.Points.Count(p => all.Sets.Any(s => s.SetId == p.SetId && s.TerritoryId != p.TerritoryId));
        text += $"; set={set.SetId} field={set.FieldCurrentIds.Length} positioned={zonePoints.Length} onThisMap={here} "
            + $"uncollectedHere={Uncollected().Length} nativeCollected={apiCollected} "
            + $"zoneComplete={(state != null && state->IsAetherCurrentZoneComplete(set.SetId))} "
            + $"rawBitCollected={raw} (should equal nativeCollected) rawBitsSetAllZones={rawTotal} territoryMismatch={wrongTerritory}";
        var player = Service.ObjectTable.LocalPlayer;
        if (player != null && Uncollected() is { Length: > 0 } list) {
            var scale = (float)agent->SelectedMapSizeFactorFloat;
            var nearest = list.OrderBy(p => (p.X - player.Position.X) * (p.X - player.Position.X)
                + (p.Z - player.Position.Z) * (p.Z - player.Position.Z)).First();
            var distance = MathF.Sqrt((nearest.X - player.Position.X) * (nearest.X - player.Position.X)
                + (nearest.Z - player.Position.Z) * (nearest.Z - player.Position.Z));
            text += $"; nearest uncollected id={nearest.CurrentId} at world {nearest.X:0},{nearest.Z:0} "
                + $"({distance:0}y away, size factor {scale:0.##})";
        }
        return text;
    }
}
