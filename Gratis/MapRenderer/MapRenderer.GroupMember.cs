using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Classes;
using Lumina.Excel.Sheets;

namespace Gratis.MapRenderer;

public unsafe partial class MapRenderer {
    private void DrawGroupMembers() {
        // Looked up once: each lookup per member used to allocate, and empty alliance slots were walked in full.
        var localId = Service.ObjectTable.LocalPlayer?.EntityId;
        var selectedTerritory = AgentMap.Instance()->SelectedTerritoryId;
        foreach (var partyMember in GroupManager.Instance(
            )->MainGroup.PartyMembers[..GroupManager.Instance()->MainGroup.MemberCount]) {
            if (partyMember.EntityId is 0xE0000000 || Service.ClientState.IsPvP && partyMember.EntityId == 0)
                continue;
            if (System.SystemConfig.IsGratis && partyMember.EntityId == localId)
                continue;
            if (partyMember.TerritoryType != selectedTerritory)
                continue;

            DrawHelpers.DrawMapMarker(new MarkerInfo {
                Source = Rendering.MapMarkerSource.Party,
                Position = (new Vector2(partyMember.Position.X, partyMember.Position.Z)
                    * DrawHelpers.GetMapScaleFactor() - DrawHelpers.GetMapOffsetVector()
                    + DrawHelpers.GetMapCenterOffsetVector())
                    * Scale,
                Offset = DrawPosition,
                Scale = Scale,
                IconId = 60421,
                PrimaryText = () => System.SystemConfig.IsGratis ? partyMember.NameString
                    : $"Lv. {partyMember.Level} {partyMember.NameString}",
                SecondaryText = () => System.SystemConfig.IsGratis
                    ? $"{Service.DataManager.GetExcelSheet<ClassJob>().GetRow(partyMember.ClassJob).Abbreviation.ExtractText()}\t"
                        + $"Lv. {partyMember.Level}"
                    : null,
            });
        }

        // Alliance slots hold nothing outside an alliance, so they are not walked at all.
        if (!GroupManager.Instance()->MainGroup.IsAlliance)
            return;
        foreach (var allianceMember in GroupManager.Instance()->MainGroup.AllianceMembers) {
            if (allianceMember.EntityId is 0xE0000000 or 0)
                continue;
            if (!Rendering.GroupTerritory.Matches(allianceMember.TerritoryType, selectedTerritory))
                continue;
            if (Service.ClientState.IsPvP
                && ((allianceMember.Flags & 1) == 0
                    || GroupManager.Instance()->MainGroup.IsEntityIdInParty(allianceMember.EntityId)))
                continue;
            if (System.SystemConfig.IsGratis
                && (allianceMember.EntityId == localId
                    || GroupManager.Instance()->MainGroup.IsEntityIdInParty(allianceMember.EntityId)))
                continue;
            if (AgentMap.Instance()->SelectedMapId != AgentMap.Instance()->CurrentMapId)
                continue;

            DrawHelpers.DrawMapMarker(new MarkerInfo {
                Source = Rendering.MapMarkerSource.Alliance,
                Position = (new Vector2(allianceMember.Position.X, allianceMember.Position.Z)
                    * DrawHelpers.GetMapScaleFactor() - DrawHelpers.GetMapOffsetVector()
                    + DrawHelpers.GetMapCenterOffsetVector())
                    * Scale,
                Offset = DrawPosition,
                Scale = Scale,
                IconId = 60403,
                PrimaryText = () => System.SystemConfig.IsGratis ? allianceMember.NameString
                    : $"Lv. {allianceMember.Level} {allianceMember.NameString}",
                SecondaryText = () => System.SystemConfig.IsGratis
                    ? $"{Service.DataManager.GetExcelSheet<ClassJob>().GetRow(allianceMember.ClassJob).Abbreviation.ExtractText()}\t"
                        + $"Lv. {allianceMember.Level}"
                    : null,
            });
        }
    }
}