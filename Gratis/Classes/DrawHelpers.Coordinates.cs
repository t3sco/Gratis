using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
#endif
using Gratis.Rendering;

namespace Gratis.Classes;

public static partial class DrawHelpers {
#if !GRATIS_PREVIEW
    public static Vector2 GetMapOffsetVector() => GetRawMapOffsetVector() * GetMapScaleFactor();

    public static unsafe Vector2 GetRawMapOffsetVector() =>
        new(AgentMap.Instance()->SelectedOffsetX, AgentMap.Instance()->SelectedOffsetY);

    public static unsafe float GetMapScaleFactor() => MapDrawContext.Current.MapSizeFactor;

    public static Vector2 GetMapCenterOffsetVector() => new(1024.0f, 1024.0f);

    public static Vector2 GetCombinedOffsetVector() => -GetMapOffsetVector() + GetMapCenterOffsetVector();

#endif
}