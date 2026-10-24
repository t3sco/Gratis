using Gratis.Classes;
#if !GRATIS_PREVIEW
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
#endif

namespace Gratis.Rendering;

public static class MarkerRelevance
{
    public static bool IsProtected(Classes.MarkerInfo marker)
    {
        if (marker.IsTarget || marker.IsFlag || marker.IsTracked || marker.IsHovered || marker.IsSpotlighted || marker.IsObjective ||
            marker.MarkerType is MarkerType.Fate or MarkerType.Stellar)
            return true;
        return MarkerIcon.Family(marker) is not (MarkerIconFamily.Neutral or MarkerIconFamily.Hunt or MarkerIconFamily.Landmark or MarkerIconFamily.Shop or MarkerIconFamily.Mender or
            MarkerIconFamily.Inn or MarkerIconFamily.Housing or MarkerIconFamily.Stable or MarkerIconFamily.Mail or
            MarkerIconFamily.Bell or MarkerIconFamily.Market);
    }

#if !GRATIS_PREVIEW
    private static readonly HashSet<uint> Accepted = new(64);
    private static int lastFrame = -1;

    public static unsafe bool IsActive(uint objective)
    {
        if (lastFrame != ImGui.GetFrameCount()) {
            Accepted.Clear();
            foreach (var quest in Map.Instance()->QuestMarkers)
                if (quest.ObjectiveId != 0) Accepted.Add(quest.ObjectiveId);
            lastFrame = ImGui.GetFrameCount();
        }
        return Accepted.Contains(objective);
    }
#endif
}
