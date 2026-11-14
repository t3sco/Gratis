using System;
using System.Numerics;
using Gratis.Classes;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

// A small gold "+" disc on the upper right of a FATE marker while its EXP bonus is active. Pure geometry plus one draw
// call: no allocation, no animation, no setting. The flag itself is sampled once a second with the rest of the FATE state.
public readonly record struct FateBonusLayout(Vector2 Center, float Radius, float Arm);

public static class FateBonusBadge {
    public static bool Shows(MarkerInfo marker) => marker.FateProgress is { HasBonus: true };

    public const float OutlineWidth = 2f;

    // radius: the marker's own drawn radius. The disc never gets smaller than 3.8 px (scaled), so it stays legible on the
    // smallest marker, and it sits on the diagonal so it cannot cover the icon centre. The "+" only appears once the disc is
    // big enough to hold one; below that it is a plain gold pip (a dark plus would swallow it).
    public static FateBonusLayout Layout(Vector2 center, float radius, float scale) {
        var disc = MathF.Max(3.8f * scale, radius * .45f);
        // Diagonal distance from the centre: on the rim, but never closer than the disc plus its outline and a hair of air.
        var offset = MathF.Max(radius * .72f + disc * .3f, (disc + (OutlineWidth + .25f) * scale) / 1.41421356f);
        return new(center + new Vector2(offset, -offset), disc, disc >= 5 * scale ? disc * .5f : 0);
    }

    // Marker drawn by FlatMarkerDrawing: called with its hover-scaled radius, inside the footprint already clipped to the map.
    public static void Draw(ImDrawListPtr list, Vector2 center, float radius, MarkerInfo marker) {
        if (!Shows(marker)) return;
        var scale = MapDrawContext.Current.GlobalScale;
        var badge = Layout(center, radius, scale);
        var dark = ImGui.GetColorU32(new Vector4(.025f, .03f, .045f, .95f));
        list.AddCircleFilled(badge.Center, badge.Radius + OutlineWidth * scale, dark, 20);
        list.AddCircleFilled(badge.Center, badge.Radius, ImGui.GetColorU32(GratisTheme.WarmAccent), 20);
        if (badge.Arm <= 0) return;
        var width = MathF.Max(1, 1.4f * scale);
        list.AddLine(badge.Center - new Vector2(badge.Arm, 0), badge.Center + new Vector2(badge.Arm, 0), dark, width);
        list.AddLine(badge.Center - new Vector2(0, badge.Arm), badge.Center + new Vector2(0, badge.Arm), dark, width);
    }

    // Game-icon markers have no hover-scaled radius of their own; use the icon's footprint.
    public static void DrawOnIcon(MarkerInfo marker) {
        if (!Shows(marker)) return;
        var scale = MapDrawContext.Current.GlobalScale;
        var half = MarkerVisual.HalfSize(marker);
        var center = marker.Position + marker.Offset + ImGui.GetWindowPos();
        if (!MapShape.ContainsSymbol(center, half)) return;
        Draw(ImGui.GetWindowDrawList(), center, MathF.Max(half.X - 2 * scale, 4 * scale), marker);
    }
}
