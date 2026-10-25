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
        var context = MapDrawContext.Current; var s = GratisTheme.ChromeScale;
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
        using var font = context.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Chip)?.Push();
        var text = FormattableString.Invariant($"{Distance:0.0} yalms");
        var size = new Vector2(ImGui.CalcTextSize(text).X + 48 * s, GratisTheme.ControlHeight * s);
        var center = (clippedA + clippedB) / 2 + new Vector2(0, 28 * s);
        var (safeMin, safeMax) = ChromeLayout.SafeBounds();
        if (size.X > safeMax.X - safeMin.X || size.Y > safeMax.Y - safeMin.Y) return;
        var pos = Vector2.Clamp(center - size / 2, safeMin, safeMax - size);
        ChipMin = pos; ChipMax = pos + size;
        MapWindowDrawing.DrawSurface(list, pos, pos + size, GratisTheme.CardRadius * s, surfaceOpacity: 1);
        GratisIcons.Draw(list, pos + new Vector2(18 * s, size.Y / 2), GratisIcons.Crosshair, GratisTheme.Crystal, GratisTheme.IconSize * s);
        list.AddText(pos + new Vector2(36 * s, (size.Y - ImGui.GetFontSize()) / 2), ImGui.GetColorU32(GratisTheme.Primary), text);
        if (ImGui.IsMouseHoveringRect(pos, pos + size, false)) ImGui.SetTooltip(FormattableString.Invariant($"({first.X:0.0}, {first.Y:0.0}) → ({second.X:0.0}, {second.Y:0.0}) · Right-click to copy"));
    }
}
