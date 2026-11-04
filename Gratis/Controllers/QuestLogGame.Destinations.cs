using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Rendering;
using Lumina.Excel.Sheets;
using SheetMap = Lumina.Excel.Sheets.Map;

namespace Gratis.Controllers;

public sealed partial class QuestLogGame {
    private const uint DungeonIcon = 60414;
    private readonly Dictionary<uint, QuestMapAccess> mapAccess = [];
    private Dictionary<uint, List<(uint Range, Vector2 Texture, uint Icon)>>? entranceMarkers;
    private Dictionary<uint, SheetMap[]>? mapsByRange;

    private unsafe QuestDestination ResolveDestination(uint id, Metadata? data, PlaceEntry[] raw) {
        if (raw.Length == 0)
            return new(raw, QuestDestinationSource.None);
        var agent = AgentMap.Instance();
        var current = agent == null ? 0 : agent->CurrentMapId;
        // Standing inside the instance, its map is exactly where the objective is.
        QuestMapAccess Access(PlaceEntry t) => t.MapId == current ? QuestMapAccess.Reachable : AccessOf(t.MapId);
        return QuestTargetSelection.Resolve(raw, Access, t => Entrance(id, t), () => Giver(id, data, raw[0]),
            () => TrackedObjective(id, raw[0]),
            () => raw.Select(t => Door(id, t)).FirstOrDefault(t => t != null) ?? ScriptEntryNpc(id, data, raw[0]));
    }

    private QuestMapAccess AccessOf(uint mapId) {
        if (mapAccess.TryGetValue(mapId, out var access))
            return access;
        var use = Service.DataManager.GetExcelSheet<SheetMap>().GetRowOrDefault(mapId)?.TerritoryType.ValueNullable
            ?.TerritoryIntendedUse.RowId;
        return mapAccess[mapId] = use is { } value ? QuestTargetSelection.Classify(value) : QuestMapAccess.Reachable;
    }

    // Duty and instance entrances are drawn on the overworld map as an icon labelled with the duty's place name.
    private PlaceEntry? Entrance(uint id, PlaceEntry target) {
        var maps = Service.DataManager.GetExcelSheet<SheetMap>();
        if (maps.GetRowOrDefault(target.MapId) is not { PlaceName.RowId: not 0 } duty)
            return null;
        entranceMarkers ??= EntranceMarkers();
        mapsByRange ??= MapsByRange();
        if (!entranceMarkers.TryGetValue(duty.PlaceName.RowId, out var markers))
            return null;
        foreach (var marker in markers.OrderBy(m => m.Icon == DungeonIcon ? 0 : 1)) {
            if (!mapsByRange.TryGetValue(marker.Range, out var candidates))
                continue;
            foreach (var map in candidates) {
                if (map.RowId == target.MapId || AccessOf(map.RowId) != QuestMapAccess.Reachable)
                    continue;
                return new($"quest-log:{id}:entrance", PlaceKind.Quest, target.Name,
                    duty.PlaceName.ValueNullable?.Name.ExtractText() ?? "", map.RowId, map.TerritoryType.RowId,
                    PlaceIndexBuilder.MarkerWorld(marker.Texture, map.SizeFactor / 100f,
                        new Vector2(map.OffsetX, map.OffsetY))) { QuestRowId = id, Icon = target.Icon };
            }
        }
        return null;
    }

    // The sheet scans below are built once, on a background task, before the quest log first reports Ready.
    private static Dictionary<uint, List<(uint Range, Vector2 Texture, uint Icon)>> EntranceMarkers() =>
        Service.DataManager.GetSubrowExcelSheet<MapMarker>().SelectMany(row => row)
            .Where(m => m.Icon != 0 && m.PlaceNameSubtext.RowId != 0).GroupBy(m => m.PlaceNameSubtext.RowId)
            .ToDictionary(g => g.Key, g => g.Select(m => (m.RowId, new Vector2(m.X, m.Y), (uint)m.Icon)).ToList());

    private static Dictionary<uint, SheetMap[]> MapsByRange() => Service.DataManager.GetExcelSheet<SheetMap>()
        .Where(m => m.MapMarkerRange != 0 && m.SizeFactor > 0)
        .GroupBy(m => (uint)m.MapMarkerRange).ToDictionary(g => g.Key, g => g.ToArray());

    private PlaceEntry? Giver(uint id, Metadata? data, PlaceEntry sample) {
        if (data?.Row.IssuerLocation.ValueNullable is not { } level || level.Map.ValueNullable is not { } map
            || AccessOf(map.RowId) != QuestMapAccess.Reachable || !float.IsFinite(level.X) || !float.IsFinite(level.Z))
            return null;
        return new($"quest-log:{id}:giver", PlaceKind.Quest, sample.Name, "", map.RowId, map.TerritoryType.RowId,
            new Vector2(level.X, level.Z)) { QuestRowId = id, Icon = sample.Icon };
    }

    private ILookup<uint, Level>? levelsByObject;
    private ILookup<string, uint>? entranceObjects;
    private readonly Dictionary<uint, Level?> namedDoors = [];

    // What the game's own quest tracker holds (FFXIVClientStructs TrackedQuest.Objectives): each objective carries
    // the map and level positions it points at, so a walkable one is the place the game itself sends the player.
    private unsafe PlaceEntry? TrackedObjective(uint id, PlaceEntry sample) {
        var ui = UIState.Instance();
        if (ui == null)
            return null;
        ref var tracked = ref ui->QuestTodoList.Todo.TrackedQuests;
        if (tracked.LongCount < 0 || tracked.LongCount > 64)
            return null;
        foreach (ref var quest in tracked) {
            if (quest.QuestId == 0 || 65536u + quest.QuestId != id)
                continue;
            // Only the current objective: other steps would send the player somewhere the quest is not asking for.
            var index = quest.CurrentObjective;
            if (index < 0 || index >= Math.Clamp(quest.ObjectivesCount, 0, 24))
                continue;
            ref var objective = ref quest.Objectives[index];
            if (objective.MapId == 0 || AccessOf(objective.MapId) != QuestMapAccess.Reachable)
                continue;
            foreach (var entry in objective.LevelEntries) {
                if (entry.LevelId == 0 || !float.IsFinite(entry.X) || !float.IsFinite(entry.Z))
                    continue;
                var map = Service.DataManager.GetExcelSheet<SheetMap>().GetRowOrDefault(objective.MapId);
                return new($"quest-log:{id}:tracked", PlaceKind.Quest, sample.Name,
                    EntryName(Service.DataManager.GetExcelSheet<Level>().GetRowOrDefault(entry.LevelId)?.Object.RowId ?? 0, "instance"), objective.MapId,
                    map?.TerritoryType.RowId ?? objective.TerritoryTypeId, new Vector2(entry.X, entry.Z)) {
                    QuestRowId = id, Icon = sample.Icon,
                };
            }
        }
        return null;
    }

    private readonly Dictionary<(uint Quest, uint Instance), (uint Territory, Vector2 World, uint Map, string Name)?> scriptEntries = [];

    // The script is static, so it is walked once per quest and instance rather than on every refresh.
    private PlaceEntry? ScriptEntryNpc(uint id, Metadata? data, PlaceEntry sample) {
        var key = (id, sample.TerritoryId);
        if (!scriptEntries.TryGetValue(key, out var found))
            scriptEntries[key] = found = data?.Row is { } quest ? FindScriptEntry(quest, sample.TerritoryId) : null;
        return found is not { } entry ? null
            : new($"quest-log:{id}:entry", PlaceKind.Quest, sample.Name, entry.Name, entry.Map, entry.Territory, entry.World) {
                QuestRowId = id, Icon = sample.Icon,
            };
    }

    // A quest can enter several instances over its steps (In Fate's Footsteps: a lift in Resolution, later a door in
    // Old Sharlayan), so the entry is the NPC declared before the instance the current objective is in. An
    // objective in an instance the script never names (the door's interior has no TERRITORYTYPE) has no entry NPC.
    private (uint Territory, Vector2 World, uint Map, string Name)? FindScriptEntry(Quest quest, uint instance) {
        levelsByObject ??= LevelsByObject();
        var issuer = quest.IssuerLocation.RowId;
        Level? best = null;
        foreach (var param in quest.QuestParams) {
            var instruction = param.ScriptInstruction.ExtractText();
            if (instruction.StartsWith("ACTOR", StringComparison.Ordinal) && char.IsDigit(instruction[^1])) {
                foreach (var level in levelsByObject[param.ScriptArg])
                    if (level.Type == 8 && level.RowId != issuer && level.Map.ValueNullable is { } map && float.IsFinite(level.X)
                        && float.IsFinite(level.Z) && AccessOf(map.RowId) == QuestMapAccess.Reachable) {
                        best = level;
                        break;
                    }
            }
            else if (instruction.StartsWith("TERRITORYTYPE", StringComparison.Ordinal)
                && Service.DataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(param.ScriptArg)
                    is { } territory && QuestTargetSelection.Classify(territory.TerritoryIntendedUse.RowId) != QuestMapAccess.Reachable) {
                // Only an NPC declared since the previous instance is the way in; without one there is no entry.
                if (param.ScriptArg == instance)
                    return best is { } npc && npc.Map.ValueNullable is { } entryMap
                        ? (entryMap.TerritoryType.RowId, new Vector2(npc.X, npc.Z), entryMap.RowId, EntryName(npc.Object.RowId, "instance")) : null;
                best = null;
            }
        }
        return null;
    }

    private static ILookup<uint, Level> LevelsByObject() => Service.DataManager.GetExcelSheet<Level>()
        .Where(l => l.Object.RowId != 0).ToLookup(l => l.Object.RowId);

    // Warp links are authoritative; name matching is only a fallback for interiors without one.
    // Cache static lookup data, never a choice that depends on the player's current position.
    private PlaceEntry? Door(uint id, PlaceEntry target) {
        if (Service.DataManager.GetExcelSheet<SheetMap>().GetRowOrDefault(target.MapId) is not { } interior)
            return null;
        var area = interior.PlaceName.ValueNullable?.Name.ExtractText() ?? "instance";
        warpDoors ??= WarpDoorsByTerritory();
        var candidates = warpDoors[interior.TerritoryType.RowId].OrderBy(l => l.RowId)
            .Where(l => l.Map.ValueNullable is { SizeFactor: > 0 })
            .Select(l => new QuestEntryPoint(interior.TerritoryType.RowId, EntryPlace(id, target, l, area),
                AccessOf(l.Map.RowId))).ToArray();
        var position = Service.ObjectTable.LocalPlayer?.Position;
        var chosen = QuestTargetSelection.ChooseEntry(target with { TerritoryId = interior.TerritoryType.RowId }, candidates,
            Service.ClientState.TerritoryType, position is { } at ? new Vector2(at.X, at.Z) : null);
        if (chosen != null)
            return chosen;
        if (!namedDoors.TryGetValue(target.MapId, out var door))
            namedDoors[target.MapId] = door = FindDoor(area, interior.PlaceNameRegion.RowId);
        return door is { } level ? EntryPlace(id, target, level, area) : null;
    }

    private static string EntryName(uint obj, string fallback) {
        var name = obj is >= 1000000 and < 2000000
            ? Service.DataManager.GetExcelSheet<ENpcResident>().GetRowOrDefault(obj)?.Singular.ExtractText()
            : Service.DataManager.GetExcelSheet<EObjName>().GetRowOrDefault(obj)?.Singular.ExtractText();
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }

    private static PlaceEntry EntryPlace(uint id, PlaceEntry target, Level level, string area) =>
        new($"quest-log:{id}:door:{level.RowId}", PlaceKind.Quest, target.Name, EntryName(level.Object.RowId, area),
            level.Map.RowId, level.Map.Value.TerritoryType.RowId, new Vector2(level.X, level.Z)) {
            QuestRowId = id, Icon = target.Icon,
        };

    private const string EntranceSuffix = " entrance";

    private static ILookup<string, uint> EntranceObjects() => Service.DataManager.GetExcelSheet<EObjName>()
        .Select(e => (Id: e.RowId, Name: e.Singular.ExtractText()))
        .Where(e => e.Name.EndsWith(EntranceSuffix, StringComparison.OrdinalIgnoreCase))
        .ToLookup(e => e.Name[..^EntranceSuffix.Length].Trim(), e => e.Id, StringComparer.OrdinalIgnoreCase);

    // Runs the door lookup for a map without needing its quest, for /gratis door.
    public string DoorDiagnostics(uint mapId) {
        var map = Service.DataManager.GetExcelSheet<SheetMap>().GetRowOrDefault(mapId);
        var door = Door(0, new PlaceEntry("door-check", PlaceKind.Quest, "", "", mapId, 0));
        return $"door map={mapId} area=\"{map?.PlaceName.ValueNullable?.Name.ExtractText()}\" "
            + (door?.World is { } at ? $"found map={door.MapId} at {at.X:0.#},{at.Y:0.#}" : "none");
    }

    private ILookup<uint, Level>? warpDoors;

    // The game's own link: a door (EObj) or NPC (ENpcBase) points at an event id. Type 2 is a Warp row, which names
    // the territory it enters; type 13 is an ArrayEventHandler row listing more event ids (the door's Warp is one).
    private static IEnumerable<uint> WarpsOf(uint eventId, Dictionary<uint, uint[]> arrays,
        ISet<uint> warps, int depth = 0) {
        if (eventId == 0 || depth > 3)
            yield break;
        var type = eventId >> 16;
        if (type == 2) {
            if (warps.Contains(eventId))
                yield return eventId;
        }
        else if (type == 13 && arrays.TryGetValue(eventId, out var children))
            foreach (var child in children)
                foreach (var warp in WarpsOf(child, arrays, warps, depth + 1))
                    yield return warp;
    }

    // Destination territory -> the placed doors and NPCs that warp into it.
    private ILookup<uint, Level> WarpDoorsByTerritory() {
        var arrays = new Dictionary<uint, uint[]>();
        var raw = Service.DataManager.Excel.GetSheet<Lumina.Excel.RawRow>(null, "ArrayEventHandler");
        foreach (var row in raw) {
            var ids = new List<uint>();
            for (var i = 0; i < raw.Columns.Count; i++)
                if (row.ReadColumn(i) is uint value && value != 0)
                    ids.Add(value);
            arrays[row.RowId] = [.. ids];
        }
        var warpSheet = Service.DataManager.GetExcelSheet<Warp>();
        var warps = warpSheet.Select(w => w.RowId).ToHashSet();
        var eobjs = Service.DataManager.GetExcelSheet<EObj>();
        var npcs = Service.DataManager.GetExcelSheet<ENpcBase>();
        levelsByObject ??= LevelsByObject();
        var found = new List<(uint Territory, Level Level)>();
        foreach (var level in levelsByObject.SelectMany(g => g)) {
            var obj = level.Object.RowId;
            IEnumerable<uint> events = obj is >= 2000000 and < 2100000 && eobjs.GetRowOrDefault(obj) is { } eobj
                ? [eobj.Data.RowId]
                : obj is >= 1000000 and < 2000000 && npcs.GetRowOrDefault(obj) is { } npc
                    ? npc.ENpcData.Select(d => d.RowId) : [];
            foreach (var warp in events.SelectMany(e => WarpsOf(e, arrays, warps)).Distinct()) {
                var destination = warpSheet.GetRow(warp).TerritoryType.RowId;
                if (destination != 0 && destination != level.Territory.RowId)
                    found.Add((destination, level));
            }
        }
        return found.ToLookup(f => f.Territory, f => f.Level);
    }

    private Level? FindDoor(string area, uint region) {
        entranceObjects ??= EntranceObjects();
        levelsByObject ??= LevelsByObject();
        Level? found = null;
        foreach (var obj in entranceObjects[area])
            foreach (var level in levelsByObject[obj]) {
                // Another area can share the name, so the door must stand in the interior's own region.
                if (level.Map.ValueNullable is not { } map || map.PlaceNameRegion.RowId != region
                    || !float.IsFinite(level.X) || !float.IsFinite(level.Z) || AccessOf(map.RowId) != QuestMapAccess.Reachable)
                    continue;
                if (found is not null)
                    return null;
                found = level;
            }
        return found;
    }
}
