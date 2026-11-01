using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Classes;
using Gratis.Controllers;
using Gratis.Extensions;
using Gratis.Rendering;
using Lumina.Excel.Sheets;
using BattleNpcSubKind = Dalamud.Game.ClientState.Objects.Enums.BattleNpcSubKind;
using GameMap = FFXIVClientStructs.FFXIV.Client.Game.UI.Map;

namespace Gratis.MapRenderer;

public partial class MapRenderer {
    private readonly EnemyMarkers enemies = new();
    private readonly Dictionary<uint, string> enemyQuests = new();
    private readonly Dictionary<uint, string> enemyQuestTargets = new();
    private readonly Dictionary<ushort, string> enemyFates = new();
    private readonly HuntingLogNeeds huntingLog = new();
    private readonly HashSet<uint> enemyHunting = new();
    // Object table slots, never the wrapper objects: an enemy that despawns between scans must not be read through a
    // wrapper whose memory is gone, so every frame re-reads the slot and checks it still holds the same entity.
    private readonly List<(int Index, uint EntityId)> enemyCandidates = new();
    private double nextEnemyNeeds, nextEnemyScan;
    private uint enemyNeedsMap;
    private int enemyFrame = -1;
    private IBattleNpc? hoveredEnemy;
    private string hoveredQuest = "", hoveredFate = "";
    private bool hoveredHunting;

    public string EnemyDiagnostics() => enemies.Diagnostics() + $" huntingNeeds={enemyHunting.Count}";

    private unsafe void DrawEnemies() {
        var context = MapDrawContext.Current;
        enemyFrame = ImGui.GetFrameCount();
        hoveredEnemy = null;
        enemies.Begin(System.SystemConfig.ShowEnemies, Service.ClientState.IsPvP);
        var agent = AgentMap.Instance();
        if (!enemies.Enabled || agent == null || agent->SelectedMapId != agent->CurrentMapId
            || Service.ObjectTable.LocalPlayer is not { } player)
            return;
        // Names/needs refresh once a second, never a sheet walk per object or per frame.
        if (ImGui.GetTime() >= nextEnemyNeeds || enemyNeedsMap != agent->CurrentMapId) {
            nextEnemyNeeds = ImGui.GetTime() + 1;
            enemyNeedsMap = agent->CurrentMapId;
            enemyQuests.Clear();
            if (context.QuestLogSource?.Snapshot is { Status: QuestLogStatus.Ready } snapshot)
                foreach (var quest in snapshot.Quests)
                    if (!quest.Completed) enemyQuests[quest.RowId] = quest.Name;
            enemyQuestTargets.Clear();
            var map = GameMap.Instance();
            if (map != null) {
                var levels = Service.DataManager.GetExcelSheet<Level>();
                foreach (var quest in map->QuestMarkers) {
                    if (quest.ObjectiveId == 0) continue;
                    var id = 65536u + quest.ObjectiveId;
                    if (!enemyQuests.TryGetValue(id, out var name)) {
                        name = quest.Label.ToString();
                        if (name.Length == 0) continue;
                        enemyQuests[id] = name;
                    }
                    foreach (var marker in quest.MarkerData) {
                        if (marker.MapId != agent->CurrentMapId || marker.LevelId == 0 || (marker.Flags & 1) != 0) continue;
                        if (levels.GetRowOrDefault(marker.LevelId) is { } level && level.Object.Is<BNpcBase>())
                            enemyQuestTargets[level.Object.RowId] = name;
                    }
                }
            }
            huntingLog.Refresh(enemyHunting, player.ClassJob.RowId);
            enemyFates.Clear();
            foreach (var fate in Service.FateTable)
                enemyFates[(ushort)fate.FateId] = fate.Name.TextValue;
        }
        // Walking the whole object table builds a wrapper per slot, so it runs a few times a second; the frames in
        // between draw (and re-check) only the hostile NPCs the last walk found.
        if (ImGui.GetTime() >= nextEnemyScan) {
            nextEnemyScan = ImGui.GetTime() + .25;
            enemyCandidates.Clear();
            var scanned = 0;
            foreach (var obj in Service.ObjectTable) {
                if (++scanned > EnemyMarkers.ScanLimit || enemyCandidates.Count >= EnemyMarkers.MarkerLimit * 2) break;
                if (obj is IBattleNpc found && IsEnemy(found, player.Position))
                    enemyCandidates.Add((found.ObjectIndex, found.EntityId));
            }
        }
        var nearest = 36f * context.GlobalScale * context.GlobalScale;
        foreach (var (slot, entityId) in enemyCandidates) {
            if (enemies.Shown >= EnemyMarkers.MarkerLimit) break;
            if (slot < 0 || slot >= Service.ObjectTable.Length
                || Service.ObjectTable[slot] is not IBattleNpc npc || npc.EntityId != entityId
                || !IsEnemy(npc, player.Position)) continue;
            var position = (npc.GetMapPosition() - DrawHelpers.GetMapOffsetVector()
                + DrawHelpers.GetMapCenterOffsetVector()) * Scale;
            if (position.X < 0 || position.Y < 0 || position.X > 2048 * Scale || position.Y > 2048 * Scale) continue;
            var center = ImGui.GetWindowPos() + DrawPosition + position;
            var extent = new Vector2(5 * context.GlobalScale);
            if (!ImGui.IsRectVisible(center - extent, center + extent) || !MapShape.Contains(center)
                || !MapShape.ContainsSymbol(center, extent)) continue;
            var native = (GameObject*)npc.Address;
            enemyQuestTargets.TryGetValue(npc.BaseId, out var questName);
            // Only attribute an explicit quest event handler belonging to an accepted quest.
            // Never infer a kill objective from proximity to a quest's map circle.
            if (native->EventHandler != null && native->EventHandler->Info.EventId.ContentId == EventHandlerContent.Quest)
                if (enemyQuests.TryGetValue(65536u + ((QuestEventHandler*)native->EventHandler)->QuestId, out var attachedQuest))
                    questName = attachedQuest;
            enemyFates.TryGetValue(native->FateId, out var fateName);
            questName ??= "";
            fateName ??= "";
            var hunting = enemyHunting.Contains(npc.NameId);
            enemies.Draw(center, context.GlobalScale, context.Config.AccentColor, questName.Length > 0 || fateName.Length > 0 || hunting);
            var distance = Vector2.DistanceSquared(center, ImGui.GetMousePos());
            if (distance < nearest) {
                nearest = distance;
                hoveredEnemy = npc;
                hoveredQuest = questName;
                hoveredFate = fateName;
                hoveredHunting = hunting;
            }
        }
    }

    private static bool IsEnemy(IBattleNpc npc, Vector3 from) => EnemyMarkers.IsVisibleEnemy(npc.IsTargetable,
        npc.BattleNpcKind == BattleNpcSubKind.Combatant, (npc.StatusFlags & StatusFlags.Hostile) != 0,
        npc.CurrentHp, Vector3.DistanceSquared(npc.Position, from));

    private void DrawEnemyTooltip() {
        var context = MapDrawContext.Current;
        if (enemyFrame != ImGui.GetFrameCount() || hoveredEnemy is not { CurrentHp: > 0 } npc
            || !System.SystemConfig.ShowEnemies || context.IsPvP || context.ReportCapture
            || context.QuestLog.InputActive || context.HoverCardFrame == ImGui.GetFrameCount()
            || !ImGui.IsWindowHovered() || ImGui.IsAnyItemHovered()) return;
        if (context.Config.IsGratis && context.Config.HoverCardStyle == 0) {
            HoverCardDrawing.Draw("enemy:" + npc.EntityId, npc.Name.ToString(),
                EnemyMarkers.Details(npc.Level, hoveredQuest, hoveredFate, hoveredHunting), MarkerCategory.Poi);
            return;
        }
        // A tooltip opened without the map's viewport lands on another viewport and never shows over the map.
        ImGui.SetNextWindowViewport(ImGui.GetWindowViewport().ID);
        ImGui.SetTooltip(EnemyMarkers.Tooltip(npc.Name.ToString(), npc.Level, hoveredQuest, hoveredFate, hoveredHunting));
        context.HoverCardFrame = ImGui.GetFrameCount();
    }
}
