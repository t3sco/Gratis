using System.Collections.Generic;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class LabelLayout {
    private const float Margin = 3;
    private static readonly List<(Vector2 Min, Vector2 Max)> placed = [];
    private static int frame = -1;
    private static MapDrawContext? owner;

    public static bool TryPlace(Vector2 min, Vector2 max) {
        if (frame != ImGui.GetFrameCount() || !ReferenceEquals(owner, MapDrawContext.Current)) {
            frame = ImGui.GetFrameCount();
            owner = MapDrawContext.Current;
            placed.Clear();
        }
        var pad = new Vector2(Margin * MapDrawContext.Current.GlobalScale);
        foreach (var (otherMin, otherMax) in placed)
            if (min.X < otherMax.X + pad.X && max.X > otherMin.X - pad.X
                && min.Y < otherMax.Y + pad.Y && max.Y > otherMin.Y - pad.Y)
                return false;
        placed.Add((min, max));
        return true;
    }
}
