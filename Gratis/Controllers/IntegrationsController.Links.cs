using System;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Compat.Extensions;
using Lumina.Excel.Sheets;
using Gratis.Classes;
using Gratis.Extensions;

namespace Gratis.Controllers;

public unsafe partial class IntegrationsController {
    private void ProcessQuestLink(AgentMap* agent, OpenMapInfo* mapInfo) {
        Service.Log.Debug("[OpenMap] Processing QuestLog Event");

        var targetMapId = mapInfo->MapId;

        if (GetMapIdForQuest(mapInfo) is { } foundMapId) {
            Service.Log.Debug($"[OpenMap] GetMapIdForQuest identified Quest Target Map as MapId: {foundMapId}");

            if (targetMapId is 0) {
                Service.Log.Debug($"[OpenMap] targetMapId was {targetMapId} using foundMapId: {foundMapId}");
                targetMapId = foundMapId;
            }
        }

        if (targetMapId == 0)
            targetMapId = agent->SelectedMapId;
        var spotlightTarget = MapRenderer.MapRenderer.QuestSpotlightTarget(agent, targetMapId);

        if (agent->SelectedMapId != targetMapId) {
            Service.Log.Debug($"[OpenMap] Opening MapId: {targetMapId}");
            OpenMap(targetMapId);
        }
        else {
            Service.Log.Debug($"[OpenMap] Already in MapId: {targetMapId}, aborting.");
        }

        var spotlightStarted = System.MapRenderer.Frame.BeginQuestSpotlight(spotlightTarget.Target, targetMapId,
            targetMapId == agent->CurrentMapId, spotlightTarget.Icon);
        if (!spotlightStarted && System.SystemConfig.CenterOnQuest && agent->TempMapMarkerCount > 0) {
            ref var targetMarker = ref agent->TempMapMarkers[0].MapMarker;
            CenterOnMarker(targetMarker);
            Service.Log.Debug($"[OpenMap] Centering Map on X = {targetMarker.X}, Y = {targetMarker.Y}");
        }

        System.MapWindow.ProcessingCommand = true;
    }

    private static void ProcessGatheringLink(AgentMap* agent) {
        Service.Log.Debug("[OpenMap] Processing GatheringLog Event");

        if (System.SystemConfig.CenterOnGathering) {
            ref var targetMarker = ref agent->TempMapMarkers[0].MapMarker;

            CenterOnMarker(targetMarker);
            Service.Log.Debug($"[OpenMap] Centering Map on X = {targetMarker.X}, Y = {targetMarker.Y}");
        }

        System.MapWindow.ProcessingCommand = true;
    }

    private static void ProcessFlagLink(AgentMap* agent) {
        Service.Log.Debug("[OpenMap] Processing FlagMarker Event");

        if (System.SystemConfig.CenterOnFlag) {
            ref var targetMarker = ref agent->FlagMapMarkers[0].MapMarker;

            CenterOnMarker(targetMarker);
            Service.Log.Debug($"[OpenMap] Centering Map on X = {targetMarker.X}, Y = {targetMarker.Y}");
        }

        System.MapWindow.ProcessingCommand = true;
    }

    private static void ProcessForayLink(AgentMap* agent, OpenMapInfo* mapInfo) {
        Service.Log.Debug("[OpenMap] Processing Bozja Event");

        var eventMarker = agent->EventMarkers.FirstOrNull(marker => marker.DataId == mapInfo->FateId
            && marker.Flags == 0x40);
        if (eventMarker is not null) {
            CenterOnMarker(eventMarker.Value);
        }

        System.MapWindow.ProcessingCommand = true;
    }

    private void ProcessTeleportLink(AgentMap* agent, OpenMapInfo* mapInfo) {
        Service.Log.Debug("[OpenMap] Processing Teleport Event");

        var targetMapId = mapInfo->MapId;

        if (agent->CurrentMapId != targetMapId) {
            Service.Log.Debug($"[OpenMap] Opening MapId: {targetMapId}");

            OpenMap(targetMapId);

            System.MapWindow.ProcessingCommand = true;

            return;
        }

        Service.Log.Debug($"[OpenMap] Already in MapId: {targetMapId}, aborting.");
        System.MapWindow.ProcessingCommand = true;
    }

    private static void CenterOnMarker(MapMarkerBase marker) {
        var coordinates = new Vector2(marker.X, marker.Y) / 16.0f * DrawHelpers.GetMapScaleFactor()
            - DrawHelpers.GetMapOffsetVector();

        System.SystemConfig.FollowPlayer = false;
        System.MapRenderer.DrawOffset = -coordinates;
    }

    private static void CenterOnMarker(MapMarkerData marker) {
        var coordinates = marker.Position.AsMapVector() * DrawHelpers.GetMapScaleFactor()
            - DrawHelpers.GetMapOffsetVector();

        System.SystemConfig.FollowPlayer = false;
        System.MapRenderer.DrawOffset = -coordinates;
    }

    private uint? GetMapIdForQuest(OpenMapInfo* mapInfo) {
        foreach (var leveQuest in QuestManager.Instance()->LeveQuests) {
            if (leveQuest.LeveId is 0)
                continue;

            var leveData = Service.DataManager.GetExcelSheet<Leve>().GetRow(leveQuest.LeveId);
            if (!IsNameMatch(leveData.Name.ExtractText(), mapInfo))
                continue;

            return leveData.LevelStart.Value.Map.RowId;
        }

        foreach (var quest in QuestManager.Instance()->NormalQuests) {
            if (quest.QuestId is 0)
                continue;

            var questData = Service.DataManager.GetExcelSheet<Quest>().GetRow(quest.QuestId + 65536u);
            if (!IsNameMatch(questData.Name.ExtractText(), mapInfo))
                continue;

            return questData.TodoParams.FirstOrDefault(param =>
                param.ToDoCompleteSeq == quest.Sequence).ToDoLocation.FirstOrDefault(location =>
                location is not { RowId: 0, ValueNullable: null }).Value.Map.RowId;
        }

        var possibleQuests = Service.DataManager.GetExcelSheet<Quest>().Where(quest =>
            quest is { IssuerLocation: { IsValid: true, RowId: not 0 } }).FirstOrNull(quest =>
            IsNameMatch(quest.Name.ExtractText(), mapInfo));

        return possibleQuests?.IssuerLocation.Value.Map.RowId;
    }

    private static bool IsNameMatch(string name, OpenMapInfo* mapInfo) =>
        string.Equals(name, mapInfo->TitleString.ToString(), StringComparison.OrdinalIgnoreCase);
}
