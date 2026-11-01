using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Classes;

namespace Gratis.MapRenderer;

public partial class MapRenderer {
    // Tracked instance quests: the game marks only the instance map, so the entry NPC gets the quest's own icon here.
    private unsafe void DrawQuestEntries() {
        var agent = AgentMap.Instance();
        if (agent == null || Rendering.MapDrawContext.Current.QuestLogSource?.Snapshot is not { Status: Rendering.QuestLogStatus.Ready } snapshot)
            return;
        foreach (var quest in snapshot.Quests) {
            if (quest.Completed || quest.EntryMarker is not { World: { } world, Icon: not 0 } entry
                || entry.MapId != agent->SelectedMapId)
                continue;
            DrawHelpers.DrawMapMarker(new MarkerInfo {
                Source = Rendering.MapMarkerSource.NativeEvent,
                Position = world * Scale * DrawHelpers.GetMapScaleFactor() + DrawHelpers.GetCombinedOffsetVector() * Scale,
                IconId = entry.Icon,
                Category = Rendering.MarkerCategory.Objective,
                Offset = DrawPosition,
                Scale = Scale,
            });
        }
    }

    private unsafe void DrawFlag() {
        DrawQuestEntries();
        if (AgentMap.Instance()->FlagMarkerCount is not 0
            && AgentMap.Instance()->FlagMapMarkers[0].TerritoryId == AgentMap.Instance()->SelectedTerritoryId) {
            ref var flagMarker = ref AgentMap.Instance()->FlagMapMarkers[0];

            DrawHelpers.DrawMapMarker(new MarkerInfo {
                Source = Rendering.MapMarkerSource.NativeFlag,
                Position = new Vector2(flagMarker.XFloat, flagMarker.YFloat) * Scale * DrawHelpers.GetMapScaleFactor()
                    + DrawHelpers.GetCombinedOffsetVector() * Scale,
                IconId = flagMarker.MapMarker.IconId,
                Category = Rendering.MarkerCategory.Objective,
                IsFlag = true,
                Offset = DrawPosition,
                Scale = Scale,
            });
        }
    }
}