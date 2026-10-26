using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Classes;
using MarkerInfo = Gratis.Classes.MarkerInfo;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Compat.CommandManager;
using Dalamud.Game;
using Lumina.Excel.Sheets;
using SheetMap = Lumina.Excel.Sheets.Map;
using GameMap = FFXIVClientStructs.FFXIV.Client.Game.UI.Map;

namespace Gratis.Controllers;

public sealed partial class PlaceIndexGame : IPaletteSource, IDisposable {
    [PluginService] private IChatGui Chat { get; set; } = null!;
    private readonly PlaceIndex index = new();
    private readonly Dictionary<uint, PlaceAccess> access = new();
    private readonly Dictionary<uint, QuestBucket> questBuckets = new();
    private PlaceEntry[] dynamic = [];
    private int revision;
    private double refreshed = double.NegativeInfinity;
    private uint lastMap;
    private ulong lastCharacter;
    private readonly GameTeleportService teleportService = new();
    public Func<IEnumerable<PlaceEntry>>? Pins { get; set; }
    private readonly PaletteGameImages images = new();
    public PaletteImage Image(PlaceEntry place) => images.Get(place);
    public PaletteOverlay Overlay { get; }
    public IEnumerable<PlaceEntry> Entries => index.Entries.Concat(dynamic);
    public int Revision => index.Revision + revision;
    public bool Building => index.Building;
    public PlaceSearchContext Context { get; private set; }

    public PlaceIndexGame() {
        Service.PluginInterface.Inject(this);
        Overlay = new PaletteOverlay(this);
        index.Start(BuildSheets,
            (count, ms) => Service.Log.Information($"[PlaceIndex] Built {count} places in {ms:0} ms: "
                + $"maps={index.Entries.Count(p => p.Kind == PlaceKind.Zone)}, "
                + $"aetherytes={index.Entries.Count(p => p.Kind == PlaceKind.Aetheryte && !p.IsShard)}, "
                + $"shards={index.Entries.Count(p => p.IsShard)}, "
                + $"markers={index.Entries.Count(p => p.Kind == PlaceKind.Marker)}"),
            error => Service.Log.Error(error, "[PlaceIndex] Sheet build failed"));
        System.CommandManager.RegisterCommand(new CommandHandler { ActivationPath = "/find", Delegate = Find });
        RegisterStoryAction();
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/nearest", Delegate = _ => {
                Refresh();
                Chat.Print($"[Gratis] nearest map={Context.MapId} "
                    + $"player={Context.Player?.ToString() ?? "unavailable on this map"} indexed={Entries.Count()} "
                    + $"frame-markers={MapDrawContext.Current.MarkerTargets.Targets.Count}; use /gratis "
                    + "find nearest <thing>");
            }
        });
    }

    private static IEnumerable<PlaceEntry> BuildSheets(CancellationToken cancellation) {
        var data = Service.DataManager;
        var english = data.GetExcelSheet<PlaceName>(ClientLanguage.English);
        var japanese = data.GetExcelSheet<PlaceName>(ClientLanguage.Japanese);
        string Alias(uint id) => id == 0 ? ""
            : (english.GetRowOrDefault(id)?.Name.ExtractText() ?? "") + " "
                + (japanese.GetRowOrDefault(id)?.Name.ExtractText() ?? "");
        var maps = data.GetExcelSheet<SheetMap>().Where(m => m.PlaceName.RowId != 0 && m.SizeFactor != 0
            && !PlaceIndexBuilder.IsUnnamed(m.PlaceName.Value.Name.ExtractText())).ToArray();
        var mapRows = PlaceIndexBuilder.WithFallbackArt(maps.Select(m => {
            var territory = m.TerritoryType.ValueNullable;
            return new PlaceMapRow(m.RowId, m.TerritoryType.RowId, m.PlaceName.Value.Name.ExtractText(),
                m.PlaceNameRegion.ValueNullable?.Name.ExtractText() ?? "",
                m.PlaceNameSub.ValueNullable?.Name.ExtractText() ?? "", Alias(m.PlaceName.RowId),
                territory?.ExVersion.RowId ?? 0, territory?.ExVersion.ValueNullable?.Name.ExtractText() ?? "",
                territory?.LoadingImage.ValueNullable is { } loading && loading.FileName.ToString().Length > 0
                    ? $"ui/loadingimage/{loading.FileName}_hr1.tex" : "",
                (uint)(territory?.ExVersion.ValueNullable?.Icon ?? 0));
        }));
        var mapMetadata = mapRows.ToDictionary(m => m.Id);
        var markers = data.GetSubrowExcelSheet<MapMarker>().SelectMany(row => row).ToArray();
        var markerGroups = markers.GroupBy(m => m.RowId).ToDictionary(g => g.Key, g => g.ToArray());
        var aetherytes = new List<PlaceAetheryteRow>();
        foreach (var a in data.GetExcelSheet<Aetheryte>()) {
            cancellation.ThrowIfCancellationRequested();
            var nameId = a.IsAetheryte ? a.PlaceName.RowId : a.AethernetName.RowId;
            var name = a.IsAetheryte ? a.PlaceName.ValueNullable?.Name.ExtractText()
                : a.AethernetName.ValueNullable?.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name))
                continue;
            var mapId = a.Map.RowId;
            Vector2? world = null;
            foreach (var levelRef in a.Level) {
                if (levelRef.ValueNullable is not { RowId: not 0 } level)
                    continue;
                if (mapId != 0 && level.Map.RowId != 0 && level.Map.RowId != mapId)
                    continue;
                mapId = mapId != 0 ? mapId : level.Map.RowId;
                world = new Vector2(level.X, level.Z);
                break;
            }
            if (world == null) {
                var candidates = maps.Where(m => mapId == 0 ? m.TerritoryType.RowId == a.Territory.RowId
                    : m.RowId == mapId);
                foreach (var map in candidates) {
                    if (!markerGroups.TryGetValue(map.MapMarkerRange, out var mapMarkers))
                        continue;
                    var marker = mapMarkers.FirstOrDefault(m => a.IsAetheryte
                        ? m.DataType == 3 && m.DataKey.RowId == a.RowId
                        : m.DataType == 4 && m.DataKey.RowId == a.AethernetName.RowId);
                    if (marker.RowId == 0)
                        continue;
                    mapId = map.RowId;
                    world = MarkerWorld(map, marker);
                    break;
                }
            }
            aetherytes.Add(new PlaceAetheryteRow(a.RowId, mapId, a.Territory.RowId, name!, a.IsAetheryte,
                a.AethernetGroup, world, Alias(nameId) + " aetheryte aethernet teleport", a.AethernetName.RowId));
        }
        foreach (var entry in PlaceIndexBuilder.Build(mapRows, aetherytes)) {
            cancellation.ThrowIfCancellationRequested();
            yield return entry;
        }
        foreach (var map in maps) {
            cancellation.ThrowIfCancellationRequested();
            if (!markerGroups.TryGetValue(map.MapMarkerRange, out var mapMarkers))
                continue;
            var metadata = mapMetadata[map.RowId];
            foreach (var marker in mapMarkers) {
                if (marker.Icon == 0)
                    continue;
                var family = MarkerIcon.Family(new MarkerInfo {
                    Position = default,
                    Offset = default,
                    Scale = 1,
                    IconId = marker.Icon
                });
                var name = marker.PlaceNameSubtext.ValueNullable?.Name.ExtractText() ?? "";
                if (name.Length == 0)
                    name = family is not (MarkerIconFamily.Auto or MarkerIconFamily.Neutral) ? family.ToString() : "";
                if (name.Length == 0)
                    continue;
                yield return new PlaceEntry($"m:{map.RowId}:{marker.SubrowId}", PlaceKind.Marker, name,
                    (map.PlaceNameRegion.ValueNullable?.Name.ExtractText() ?? "") + " > "
                        + map.PlaceName.Value.Name.ExtractText(),
                    map.RowId, map.TerritoryType.RowId, MarkerWorld(map, marker),
                    Aliases: Alias(marker.PlaceNameSubtext.RowId), Expansion: metadata.Expansion) {
                    Icon = marker.Icon,
                    ExpansionName = metadata.ExpansionName,
                    ArtPath = metadata.ArtPath,
                    ExpansionIcon = metadata.ExpansionIcon
                };
            }
        }
    }

    private static Vector2 MarkerWorld(SheetMap map, MapMarker marker) =>
        PlaceIndexBuilder.MarkerWorld(new Vector2(marker.X, marker.Y), map.SizeFactor / 100f,
            new Vector2(map.OffsetX, map.OffsetY));

    public unsafe void Opening() {
        if (Service.ClientState.IsLoggedIn)
            Telepo.Instance()->UpdateAetheryteList();
        refreshed = double.NegativeInfinity;
        teleportService.ClearConfirmation();
        Refresh();
    }

    public unsafe void Refresh() {
        if (Service.ClientState.IsPvP) {
            ResetPvpState();
            return;
        }
        var now = ImGui.GetTime();
        var agent = AgentMap.Instance();
        var character = Service.ObjectTable.LocalPlayer?.GameObjectId ?? 0;
        if (now - refreshed < .75 && lastMap == agent->SelectedMapId && lastCharacter == character)
            return;
        refreshed = now;
        lastMap = agent->SelectedMapId;
        lastCharacter = character;
        Context = new PlaceSearchContext(lastMap, agent->SelectedTerritoryId,
            lastMap == agent->CurrentMapId && Service.ObjectTable.LocalPlayer is { } player
                ? new Vector2(player.Position.X, player.Position.Z) : null);
        var live = new List<PlaceEntry>();
        access.Clear();
        if (Service.ClientState.IsLoggedIn) {
            foreach (var place in index.Entries) {
                if (place.Kind != PlaceKind.Aetheryte)
                    continue;
                access[place.AetheryteId] = new PlaceAccess(UIState.Instance()->IsAetheryteUnlocked(place.AetheryteId));
            }
            foreach (var destination in Service.AetheryteList)
                if (destination.SubIndex == 0)
                    access[destination.AetheryteId] = new PlaceAccess(true, destination.GilCost);
            for (var i = 0; i < agent->MapMarkerCount; i++) {
                ref var marker = ref agent->MapMarkers[i];
                var name = marker.MapMarker.Subtext.AsDalamudSeString().TextValue;
                if (marker.MapMarker.IconId == 0)
                    continue;
                if (string.IsNullOrWhiteSpace(name))
                    name = MarkerIcon.Family(new MarkerInfo {
                        Position = default, Offset = default, Scale = 1,
                        IconId = marker.MapMarker.IconId
                    }).ToString();
                var world = new Vector2(marker.MapMarker.X, marker.MapMarker.Y) / 16 / DrawHelpers.GetMapScaleFactor()
                    + DrawHelpers.GetRawMapOffsetVector();
                live.Add(new PlaceEntry($"live:{lastMap}:{i}", PlaceKind.Marker, name, "Current map", lastMap,
                    agent->SelectedTerritoryId, world) { Icon = marker.MapMarker.IconId });
            }
            foreach (var obj in Service.ObjectTable) {
                if (agent->SelectedMapId != agent->CurrentMapId || obj.Name.TextValue.Length == 0)
                    continue;
                if (obj.ObjectKind is not (
                    Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventNpc or Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Aetheryte))
                    continue;
                live.Add(new PlaceEntry($"npc:{obj.GameObjectId}", PlaceKind.Marker, obj.Name.TextValue,
                    "Current map > NPC / service", lastMap, agent->CurrentTerritoryId,
                    new Vector2(obj.Position.X, obj.Position.Z)));
            }
            foreach (var fate in Service.FateTable) {
                if (agent->SelectedMapId != agent->CurrentMapId)
                    break;
                live.Add(new PlaceEntry($"fate:{fate.FateId}", PlaceKind.Marker, fate.Name.TextValue,
                    "Current map > FATE", lastMap, agent->CurrentTerritoryId,
                    new Vector2(fate.Position.X, fate.Position.Z)));
            }
            foreach (var quest in GameMap.Instance()->QuestMarkers) {
                if (quest.ObjectiveId == 0)
                    continue;
                var rowId = quest.ObjectiveId + 65536u;
                var bucket = QuestCategory(rowId);
                var i = 0;
                foreach (var marker in quest.MarkerData) {
                    if (marker.MapId == 0)
                        continue;
                    live.Add(new PlaceEntry($"q:{quest.ObjectiveId}:{i++}", PlaceKind.Quest, quest.Label.ToString(),
                        "Active / tracked quest", marker.MapId, marker.TerritoryTypeId,
                        new Vector2(marker.Position.X, marker.Position.Z)) {
                        Icon = marker.IconId, QuestRowId = rowId, QuestBucket = bucket
                    });
                }
            }
        }
        foreach (var flag in System.FlagConfig.FlagHistory) {
            var name = flag.GetMap().PlaceName.ValueNullable?.Name.ExtractText() ?? "Flag";
            live.Add(new PlaceEntry("flag:" + flag.GetIdString(), PlaceKind.Flag, name + " · flag",
                "Flag history > " + name, flag.Map, flag.Territory, flag.GetCoordinate()));
        }
        if (Pins != null)
            live.AddRange(Pins());
        if (Service.ObjectTable.LocalPlayer != null && agent->CurrentMapId != 0)
            live.Add(new PlaceEntry("action:copy-location", PlaceKind.Action, "Copy my location",
                "Copy coordinates with map name", agent->CurrentMapId, agent->CurrentTerritoryId));
        static (uint, string, int, int) Key(PlaceEntry entry) =>
            (entry.MapId, entry.SearchName, (int)MathF.Round(entry.World?.X ?? 0),
                (int)MathF.Round(entry.World?.Y ?? 0));
        var known = index.Entries.Where(p => p.Kind == PlaceKind.Marker && p.MapId == lastMap).Select(Key).ToHashSet();
        var zones = index.Entries.Where(p => p.Kind == PlaceKind.Zone).GroupBy(p => p.MapId).ToDictionary(g => g.Key,
            g => g.First());
        dynamic = live.Where(p => p.Kind != PlaceKind.Marker || known.Add(Key(p))).Select(p => {
            if (!zones.TryGetValue(p.MapId, out var zone))
                return p;
            return p with {
                Subtitle = p.Subtitle.StartsWith("Current map", StringComparison.Ordinal) ? zone.Subtitle : p.Subtitle,
                Expansion = zone.Expansion,
                ExpansionName = zone.ExpansionName,
                ArtPath = zone.ArtPath,
                ExpansionIcon = zone.ExpansionIcon,
                SearchContext = PlaceSearch.Normalize(zone.Subtitle + " " + p.Subtitle)
            };
        }).ToArray();
        revision++;
    }

    private QuestBucket QuestCategory(uint rowId) {
        if (questBuckets.TryGetValue(rowId, out var bucket))
            return bucket;
        var quest = Service.DataManager.GetExcelSheet<Quest>().GetRowOrDefault(rowId);
        var section = quest?.JournalGenre.ValueNullable?.JournalCategory.ValueNullable?.JournalSection.ValueNullable;
        return questBuckets[rowId] = QuestBuckets.FromSection(section?.RowId);
    }

    public PlaceAccess Access(PlaceEntry place) => access.GetValueOrDefault(place.AetheryteId);
    public void Invalidate() => refreshed = double.NegativeInfinity;
    public void ResetPvpState() {
        dynamic = [];
        Context = default;
        Overlay.Close(false);
        Invalidate();
        revision++;
    }

    public unsafe void Show(PlaceEntry place) {
        if (!System.SystemConfig.IsGratis || place.MapId == 0)
            return;
        if (place.Id == "action:copy-location") {
            CoordinateShareGame.CopyPlayer();
            return;
        }
        System.ZoneSwitchController?.NotifyUserAction();
        var sameMap = place.MapId == AgentMap.Instance()->CurrentMapId;
        if (AgentMap.Instance()->SelectedMapId != place.MapId)
            System.IntegrationsController.OpenMap(place.MapId);
        System.MapWindow.ProcessingCommand = true;
        if (place.World is { } world) {
            var request = new SpotlightRequest(world, place.MapId,
                place.Kind == PlaceKind.Pin ? SpotlightSource.Pin : SpotlightSource.Search, IncludePlayer: sameMap);
            if (!System.MapRenderer.Frame.BeginSpotlight(request)) {
                System.SystemConfig.FollowPlayer = false;
                System.MapRenderer.CenterOnCoordinate(world);
            }
        }
        else {
            System.SystemConfig.FollowPlayer = false;
            System.MapRenderer.DrawOffset = Vector2.Zero;
        }
    }

    public string Teleport(PlaceEntry place, bool confirmed) =>
        teleportService.Request(place, confirmed, System.SystemConfig.PaletteTeleportButton);

    public void Used() {
        if (System.SystemConfig.PaletteHintSeen)
            return;
        System.SystemConfig.PaletteHintSeen = true;
        SystemConfig.Save();
    }

    public void CaptureGameKeys() {
        if (!Overlay.CapturesInput && !LocationMenuDrawing.CapturesKeyboard
            && !MapDrawContext.Current.QuestLog.CapturesKeyboard && !MapDrawContext.Current.QuestLog.PointerOver)
            return;
        foreach (var key in Service.KeyState.GetValidVirtualKeys())
            Service.KeyState[key] = false;
    }

    private void Find(params string[] args) {
        Refresh();
        var found = PlaceSearch.Find(Entries, string.Join(' ', args), PlaceKind.All, Context, new Dictionary<string,
            long>(), Access, System.SystemConfig.PaletteShowUnattuned);
        Chat.Print($"[Gratis] {found.Length} places{(Building ? " (index building)" : "")}");
        foreach (var result in found.Take(8))
            Chat.Print($"[Gratis] {result.Place.Name} — {result.Place.Subtitle} (map {result.Place.MapId})"
                + (result.Place.Kind == PlaceKind.Aetheryte ? result.Access.Attuned ? " [Attuned]" : " [Not attuned]"
                    : ""));
    }

    public void Dispose() {
        images.Dispose();
        index.Dispose();
        System.CommandManager.RemoveCommand("/find");
        System.CommandManager.RemoveCommand("/nearest");
        System.CommandManager.RemoveCommand("/msq");
    }
}
