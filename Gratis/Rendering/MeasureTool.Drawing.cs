using System;
using System.Numerics;
using Gratis.Classes;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class MeasureTool
{
    private void DrawRuler(IMapWindowHost host)
    {
        var context = MapDrawContext.Current; var s = context.GlobalScale;
        var origin = ImGui.GetWindowPos(); var (min, max) = MapShape.Bounds(origin, ImGui.GetWindowSize());
        var a = host.Renderer.WorldToScreen(Start, origin); var b = host.Renderer.WorldToScreen(End, origin);
        var rounding = MapShape.Rounding(max - min);
        var list = ImGui.GetWindowDrawList();
        ChipMin = ChipMax = Vector2.Zero;
        if (!MeasureToolMath.Clip(a, b, min, max, rounding, MathF.Max(3 * s, MapShape.FeatherWidth), out var clippedA, out var clippedB)) return;
        var colour = ImGui.GetColorU32(GratisTheme.Transport);
        list.AddLine(clippedA, clippedB, ImGui.GetColorU32(GratisTheme.Outline), 5 * s);
        list.AddLine(clippedA, clippedB, colour, 2 * s);
        foreach (var endpoint in new[] { a, b })
            if (MeasureToolMath.Depth(endpoint, min, max, rounding) >= 5 * s) {
                list.AddCircleFilled(endpoint, 5 * s, ImGui.GetColorU32(GratisTheme.Outline), 16);
                list.AddCircleFilled(endpoint, 3 * s, colour, 16);
            }
        var first = host.Renderer.WorldToMapCoordinates(Start); var second = host.Renderer.WorldToMapCoordinates(End);
        using var font = context.GetChromeFont(11 * s, ChromeFontRole.Chip)?.Push();
        var text = FormattableString.Invariant($"{Distance:0.0}y  ·  ({first.X:0.0}, {first.Y:0.0}) to ({second.X:0.0}, {second.Y:0.0})");
        var size = ImGui.CalcTextSize(text) + new Vector2(16, 10) * s;
        var center = (clippedA + clippedB) / 2;
        var safeMin = min + new Vector2(8 * s); var safeMax = max - new Vector2(8 * s);
        if (host.Config.MapCircle) { var inset = (max - min) * .14644661f; safeMin += inset; safeMax -= inset; }
        if (size.X > safeMax.X - safeMin.X || size.Y > safeMax.Y - safeMin.Y) return;
        var pos = Vector2.Clamp(center - size / 2, safeMin, safeMax - size);
        ChipMin = pos; ChipMax = pos + size;
        MapWindowDrawing.DrawSurface(list, pos, pos + size, 7 * s, surfaceOpacity: 1);
        list.AddText(pos + new Vector2(8, 5) * s, ImGui.GetColorU32(GratisTheme.Primary), text);
        if (ImGui.IsMouseHoveringRect(pos, pos + size, false)) ImGui.SetTooltip("Ruler · right-click to copy distance · Esc clears");
    }
}
