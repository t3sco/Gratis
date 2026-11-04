using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Classes;
using Gratis.Rendering;
using Lumina.Excel.Sheets;
using MarkerInfo = Gratis.Classes.MarkerInfo;
using TripleTriad = Lumina.Excel.Sheets.TripleTriad;

namespace Gratis.Controllers;

internal static class TripleTriadGame {
    internal sealed record Point(TripleTriadOpponent Row, PlaceEntry Entry, MarkerInfo Marker);
    private sealed record Index(TripleTriadOpponent[] Rows, int Total);
    private static readonly SheetIndexWorker<Index> loader = new();
    internal static void Dispose() => loader.Dispose();
    private static Point[] points = [];
    private static long refreshed = long.MinValue;
    private static uint lastMap;
    private static ulong lastCharacter;

    private static Index Build(global::System.Threading.CancellationToken token) {
        var data = Service.DataManager;
        var triad = data.GetExcelSheet<TripleTriad>().Where(r => r.RowId != 0).ToArray();
        var ids = triad.Select(r => r.RowId).ToHashSet();
        // Exact event IDs only: never match a truncated ID against unrelated NPC event handlers.
        var npcs = data.GetExcelSheet<ENpcBase>()
            .Where(_ => { token.ThrowIfCancellationRequested(); return true; })
            .SelectMany(n => n.ENpcData.Select(e => e.RowId).Where(ids.Contains).Distinct()
                .Select(id => (Id: id, Npc: n.RowId))).ToLookup(n => n.Id, n => n.Npc);
        var levels = data.GetExcelSheet<Level>().Where(l => { token.ThrowIfCancellationRequested(); return l.Type == 8 && l.Object.RowId != 0; })
            .ToLookup(l => l.Object.RowId);
        var residents = data.GetExcelSheet<ENpcResident>();
        var found = new List<TripleTriadOpponent>();
        foreach (var row in triad) {
            token.ThrowIfCancellationRequested();
            var candidates = npcs[row.RowId].Distinct().ToArray();
            if (candidates.Length != 1) continue;
            var npc = candidates[0];
            var name = residents.GetRowOrDefault(npc)?.Singular.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var placements = levels[npc].Where(l => l.Map.RowId != 0 && l.Map.ValueNullable is { SizeFactor: > 0 }
                && l.Territory.RowId == l.Map.Value.TerritoryType.RowId
                && float.IsFinite(l.X) && float.IsFinite(l.Z))
                .Select(l => (Map: l.Map.RowId, Territory: l.Territory.RowId, World: new Vector2(l.X, l.Z)))
                .Distinct().ToArray();
            // Multiple scripted placements are not proof of the opponent's static location.
            if (placements.Length != 1) continue;
            var p = placements[0];
            found.Add(new(row.RowId, npc, name, p.Map, p.Territory, p.World,
                row.PreviousQuest.Select(q => q.RowId).Where(id => id != 0).ToArray()));
        }
        return new(found.ToArray(), triad.Length);
    }

    private static unsafe bool Beaten(uint id) => TripleTriadOpponents.IsBeaten(id,
        static index => UIState.Instance()->IsTripleTriadNpcBeaten(index));
    private static bool QuestComplete(uint id) => Service.DataManager.GetExcelSheet<Quest>().GetRowOrDefault(id)
        is { } quest && Service.UnlockState.IsQuestCompleted(quest);

    internal static unsafe Point[] Visible() {
        if (!System.SystemConfig.IsGratis || !System.SystemConfig.TripleTriadOpponents
            || !Service.ClientState.IsLoggedIn || Service.ClientState.IsPvP || UIState.Instance() == null) {
            points = [];
            refreshed = long.MinValue;
            return points;
        }
        var agent = AgentMap.Instance();
        if (agent == null) return [];
        var map = agent->SelectedMapId;
        var character = Service.ObjectTable.LocalPlayer?.GameObjectId ?? 0;
        var now = Environment.TickCount64 / 1000;
        if (now == refreshed && map == lastMap && character == lastCharacter) return points;
        loader.Ensure(Build);
        if (loader.Value is not { } index) return [];
        var rows = index.Rows;
        refreshed = now;
        lastMap = map;
        lastCharacter = character;
        points = TripleTriadOpponents.Filter(rows!, true, map, Beaten, QuestComplete).Select(row => {
            var entry = TripleTriadOpponents.Entry(row);
            return new Point(row, entry, new MarkerInfo {
                Source = MapMarkerSource.NativeEvent, Position = default, Offset = default, Scale = 1,
                IconId = entry.Icon, IconFamily = MarkerIconFamily.Cards,
                PrimaryText = () => entry.Name, SecondaryText = () => entry.Subtitle,
            });
        }).ToArray();
        return points;
    }

    internal static unsafe string Diagnostics() {
        if (!Service.ClientState.IsLoggedIn || UIState.Instance() == null) return "Log in first.";
        loader.Ensure(Build);
        if (loader.Error is { } error) return "triad index failed: " + error;
        if (loader.Value is not { } index) return "triad index still building; run again in a moment";
        var rows = index.Rows;
        var map = AgentMap.Instance()->SelectedMapId;
        var sample = rows!.OrderBy(r => r.MapId == map ? 0 : 1).ThenBy(r => r.Id).Take(8);
        return $"map={map} resolved={rows!.Length}/{index.Total} skipped={index.Total - rows.Length}; full TripleTriadResident row id; "
            + string.Join(" | ", sample.Select(r => $"{r.Name}: row={r.Id} npc={r.NpcId} map={r.MapId} "
                + $"at={r.World} resident={TripleTriadOpponents.ResidentIdFromTripleTriadRowId(r.Id)} beaten={Beaten(r.Id)}"));
    }
}
