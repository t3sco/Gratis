using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Compat.Extensions;
using Lumina.Excel.Sheets;
using Gratis.Classes;
using Gratis.Extensions;
using BattleNpcSubKind = Dalamud.Game.ClientState.Objects.Enums.BattleNpcSubKind;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace Gratis.MapRenderer;

public partial class MapRenderer {
    private unsafe void DrawGameObjects() {
        if (Service.ClientState.IsPvP)
            return;
        if (AgentMap.Instance()->SelectedMapId != AgentMap.Instance()->CurrentMapId)
            return;

        if (Service.ObjectTable is not { LocalPlayer: { } player })
            return;

        if (System.SystemConfig.ShowRadar) {
            if ((Service.Condition.IsBoundByDuty() && System.SystemConfig.ShowRadarInDuties)
                || !Service.Condition.IsBoundByDuty()) {
                DrawRadar(player);
            }
        }

        var targetId = player.TargetObject?.EntityId;
        // Same snapshot-then-reverse order as Enumerable.Reverse, into a buffer that is reused between frames.
        gameObjectBuffer.Clear();
        foreach (var tableObject in Service.ObjectTable)
            gameObjectBuffer.Add(tableObject);
        for (var index = gameObjectBuffer.Count - 1; index >= 0; index--) {
            var obj = gameObjectBuffer[index];
            if (!obj.IsTargetable)
                continue;
            if (System.SystemConfig.IsGratis && obj.EntityId == player.EntityId)
                continue;
            if (obj.ObjectKind == ObjectKind.BattleNpc && (System.SystemConfig.IsGratis
                || System.SystemConfig.ShowEnemies && obj is IBattleNpc enemy
                    && enemy.BattleNpcKind == BattleNpcSubKind.Combatant
                    && (enemy.StatusFlags & Dalamud.Game.ClientState.Objects.Enums.StatusFlags.Hostile) != 0))
                continue;
            if (GroupManager.Instance()->MainGroup.IsEntityIdInParty(obj.EntityId))
                continue;
            if (GroupManager.Instance()->MainGroup.IsEntityIdInAlliance(obj.EntityId))
                continue;
            if (Vector3.Distance(obj.Position, player.Position) >= 150.0f)
                continue;

            // DrawMapMarker ignores icon 0, so such objects (most NPCs) no longer get a marker and closures built.
            var iconId = GetGameObjectIconId(obj);
            if (iconId is 0)
                continue;

            DrawGameObjectMarker(obj, iconId, targetId, player);
        }
        gameObjectBuffer.Clear();
    }

    private readonly List<IGameObject> gameObjectBuffer = [];

    private unsafe uint GetGameObjectIconId(IGameObject obj) => obj.ObjectKind switch {
        ObjectKind.Pc when GroupManager.Instance()->MainGroup.MemberCount is 0
            && System.SystemConfig.ShowPlayers => 60421,
        ObjectKind.Pc when System.SystemConfig.ShowPlayers => 60444,
        ObjectKind.BattleNpc when IsBoss(obj) && obj.TargetObject is null => 60402,
        ObjectKind.BattleNpc when IsBoss(obj) && obj.TargetObject is not null => 60401,
        ObjectKind.BattleNpc when obj is {
            SubKind: (int)BattleNpcSubKind.Combatant,
            TargetObject: not null
        } =>
            60422,
        ObjectKind.BattleNpc when obj is { SubKind: (int)BattleNpcSubKind.Combatant, TargetObject: null } =>
            60424,
        ObjectKind.BattleNpc when obj.SubKind == (int)BattleNpcSubKind.Pet => 60961,
        ObjectKind.Treasure => 60003,
        ObjectKind.GatheringPoint => System.GatheringPointIconCache.GetValue(obj.BaseId),
        ObjectKind.EventObj when IsAetherCurrent(obj) => 60653,
        _ => 0
    };

    // Separate from the loop so the closure over obj is only allocated for objects that are drawn, not for every
    // entry in the object table.
    private void DrawGameObjectMarker(IGameObject obj, uint iconId, uint? targetId, IPlayerCharacter player) {
        DrawHelpers.DrawMapMarker(new MarkerInfo {
            Source = Rendering.MapMarkerSource.ObjectTable,
            Position = (obj.GetMapPosition() - DrawHelpers.GetMapOffsetVector()
                + DrawHelpers.GetMapCenterOffsetVector())
                * Scale,
            Offset = DrawPosition,
            Scale = Scale,
            IconFamily = obj.ObjectKind switch {
                ObjectKind.Pc => Rendering.MarkerIconFamily.Player,
                ObjectKind.BattleNpc when obj.SubKind == (int)BattleNpcSubKind.Pet =>
                    Rendering.MarkerIconFamily.Pet,
                ObjectKind.BattleNpc or ObjectKind.EventNpc => Rendering.MarkerIconFamily.Neutral,
                ObjectKind.Treasure => Rendering.MarkerIconFamily.Treasure,
                _ => Rendering.MarkerIconFamily.Auto,
            },
            IsTarget = targetId == obj.EntityId,
            IsRelevant = targetId == obj.EntityId
                || Vector3.DistanceSquared(obj.Position, player.Position) < 10000,
            IconId = iconId,
            PrimaryText = () => System.SystemConfig.IsGratis && obj is IPlayerCharacter ? obj.Name.ToString()
                : GetTooltipForGameObject(obj),
            SecondaryText = () => System.SystemConfig.IsGratis && obj is IPlayerCharacter character
                ? $"{character.ClassJob.Value.Abbreviation.ExtractText()}\tLv. {character.Level}" : null,
        });
    }

    private void DrawRadar(IPlayerCharacter gameObjectCenter) {
        var position = ImGui.GetWindowPos() + DrawPosition
            + (gameObjectCenter.GetMapPosition() - DrawHelpers.GetMapOffsetVector()
                + DrawHelpers.GetMapCenterOffsetVector())
            * Scale;

        ImGui.GetWindowDrawList().AddCircleFilled(position, 150.0f * Scale,
            ImGui.GetColorU32(System.SystemConfig.RadarColor));
        ImGui.GetWindowDrawList().AddCircle(position, 150.0f * Scale,
            ImGui.GetColorU32(System.SystemConfig.RadarOutlineColor));
    }

    private string GetTooltipForGameObject(IGameObject obj) {
        return obj switch {
            IBattleNpc { Level: > 0 } battleNpc => $"Lv. {battleNpc.Level} {battleNpc.Name}",
            IPlayerCharacter { Level: > 0 } playerCharacter => $"Lv. {playerCharacter.Level} {playerCharacter.Name}",
            _ => obj.ObjectKind switch {
                ObjectKind.GatheringPoint => System.GatheringPointNameCache.GetValue((obj.BaseId, obj.Name.ToString()))
                    ?? string.Empty,
                ObjectKind.Treasure => obj.Name.ToString(),
                ObjectKind.EventObj when IsAetherCurrent(obj) => obj.Name.ToString(),
                _ => string.Empty
            },
        };
    }

    private unsafe bool IsAetherCurrent(IGameObject gameObject) {
        if (gameObject.ObjectKind is not ObjectKind.EventObj)
            return false;

        var csEventObject = (GameObject*)gameObject.Address;
        if (csEventObject is null)
            return false;

        if (csEventObject->EventHandler is null)
            return false;
        return csEventObject->EventHandler->Info.EventId.ContentId == EventHandlerContent.AetherCurrent;
    }

    private bool IsBoss(IGameObject chara) =>
        Service.DataManager.GetExcelSheet<BNpcBase>().GetRow(chara.BaseId).Rank is 2 or 6;
}