using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
        return QuestTargetSelection.Resolve(raw, Access, t => Entrance(id, t), () => Giver(id, data, raw[0]));
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
        entranceMarkers ??= Service.DataManager.GetSubrowExcelSheet<MapMarker>().SelectMany(row => row)
            .Where(m => m.Icon != 0 && m.PlaceNameSubtext.RowId != 0).GroupBy(m => m.PlaceNameSubtext.RowId)
            .ToDictionary(g => g.Key, g => g.Select(m => (m.RowId, new Vector2(m.X, m.Y), (uint)m.Icon)).ToList());
        mapsByRange ??= maps.Where(m => m.MapMarkerRange != 0 && m.SizeFactor > 0)
            .GroupBy(m => (uint)m.MapMarkerRange).ToDictionary(g => g.Key, g => g.ToArray());
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

    private PlaceEntry? Giver(uint id, Metadata? data, PlaceEntry sample) {
        if (data?.Row.IssuerLocation.ValueNullable is not { } level || level.Map.ValueNullable is not { } map
            || AccessOf(map.RowId) != QuestMapAccess.Reachable || !float.IsFinite(level.X) || !float.IsFinite(level.Z))
            return null;
        return new($"quest-log:{id}:giver", PlaceKind.Quest, sample.Name, "", map.RowId, map.TerritoryType.RowId,
            new Vector2(level.X, level.Z)) { QuestRowId = id, Icon = sample.Icon };
    }
}
