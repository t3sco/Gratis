#if !GRATIS_PREVIEW
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Gratis.Rendering;

internal static class MarkerRelevance
{
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
}
#endif
