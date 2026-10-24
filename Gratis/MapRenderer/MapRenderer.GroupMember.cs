using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Classes;
using Lumina.Excel.Sheets;
using Dalamud.Utility;

namespace Gratis.MapRenderer;

public unsafe partial class MapRenderer
{
    private void DrawGroupMembers()
    {
        foreach (var partyMember in GroupManager.Instance()->MainGroup.PartyMembers[..GroupManager.Instance()->MainGroup.MemberCount]) {
            if (partyMember.EntityId is 0xE0000000) continue;
            if (System.SystemConfig.IsGratis && partyMember.EntityId == Service.ObjectTable.LocalPlayer?.EntityId) continue;
            if (partyMember.TerritoryType != AgentMap.Instance()->SelectedTerritoryId) continue;

            DrawHelpers.DrawMapMarker(new MarkerInfo
            {
                Position = (new Vector2(partyMember.Position.X, partyMember.Position.Z) * DrawHelpers.GetMapScaleFactor() -
                            DrawHelpers.GetMapOffsetVector() +
                            DrawHelpers.GetMapCenterOffsetVector()) * Scale,
                Offset = DrawPosition,
                Scale = Scale,
                IconId = 60421,
                PrimaryText = () => System.SystemConfig.IsGratis ? partyMember.NameString : $"Lv. {partyMember.Level} {partyMember.NameString}",
                SecondaryText = () => System.SystemConfig.IsGratis
                    ? $"{Service.DataManager.GetExcelSheet<ClassJob>().GetRow(partyMember.ClassJob).Abbreviation.ExtractText()} · Lv. {partyMember.Level}" : null,
            });
        }

        foreach (var allianceMember in GroupManager.Instance()->MainGroup.AllianceMembers) {
            if (allianceMember.EntityId is 0xE0000000) continue;
            if (System.SystemConfig.IsGratis && (allianceMember.EntityId == Service.ObjectTable.LocalPlayer?.EntityId ||
                GroupManager.Instance()->MainGroup.IsEntityIdInParty(allianceMember.EntityId))) continue;
            if (AgentMap.Instance()->SelectedMapId != AgentMap.Instance()->CurrentMapId) continue;

            DrawHelpers.DrawMapMarker(new MarkerInfo
            {
                Position = (new Vector2(allianceMember.Position.X, allianceMember.Position.Z) * DrawHelpers.GetMapScaleFactor() -
                            DrawHelpers.GetMapOffsetVector() +
                            DrawHelpers.GetMapCenterOffsetVector()) * Scale,
                Offset = DrawPosition,
                Scale = Scale,
                IconId = 60403,
                PrimaryText = () => System.SystemConfig.IsGratis ? allianceMember.NameString : $"Lv. {allianceMember.Level} {allianceMember.NameString}",
                SecondaryText = () => System.SystemConfig.IsGratis
                    ? $"{Service.DataManager.GetExcelSheet<ClassJob>().GetRow(allianceMember.ClassJob).Abbreviation.ExtractText()} · Lv. {allianceMember.Level}" : null,
            });
        }
    }
}
