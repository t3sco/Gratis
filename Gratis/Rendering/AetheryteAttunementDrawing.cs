using System;
using System.Numerics;
using Gratis.Classes;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class AetheryteAttunementDrawing
{
    public static float Opacity(MarkerInfo marker)
    {
        var c = MapDrawContext.Current;
        return AetheryteAttunement.Opacity(c.Config, marker,
            c.Visibility.State(MarkerVisibility.Group(marker), MarkerVisibility.IsPriority(marker)) == MarkerVisibilityState.Loud);
    }
    public static void Ring(MarkerInfo marker)
    {
        var c = MapDrawContext.Current;
        if (AetheryteAttunement.Status(c.Config, marker) != false) return;
        var center = marker.Position + marker.Offset + ImGui.GetWindowPos();
        var half = MarkerVisual.HalfSize(marker);
        var radius = MathF.Max(half.X, half.Y);
        if (!MapShape.ContainsSymbol(center, new Vector2(radius + c.GlobalScale))) return;
        var loud = c.Visibility.State(MarkerVisibility.Group(marker), true) == MarkerVisibilityState.Loud;
        var color = ImGui.GetColorU32(FlatMarkerDrawing.Color(MarkerCategory.Transport) with { W = loud ? .95f : .7f });
        var list = ImGui.GetWindowDrawList();
        for (var dash = 0; dash < 12; dash++) {
            list.PathArcTo(center, radius, dash * MathF.Tau / 12, (dash + .55f) * MathF.Tau / 12, 3);
            list.PathStroke(color, ImDrawFlags.None, c.GlobalScale);
        }
    }
}
