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
    private string labelText = "";
    private Vector2 labelPos, labelSize;
    private void DrawLabelExit()
    {
        var alpha = UiMotion.Visibility(ImGui.GetID("##measure-label"), false);
        ChipMin = ChipMax = Vector2.Zero;
        if (alpha <= .001f || labelText.Length == 0) return;
        var s = GratisTheme.ChromeScale; var list = ImGui.GetWindowDrawList();
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Chip)?.Push();
        using var motion = new UiMotion.VisualScope(list, labelPos, alpha, UiMotion.Full ? new Vector2(0, (1 - alpha) * 6 * s) : Vector2.Zero);
        MapWindowDrawing.DrawSurface(list, labelPos, labelPos + labelSize, GratisTheme.CardRadius * s, surfaceOpacity: 1);
        GratisIcons.Draw(list, labelPos + new Vector2(18 * s, labelSize.Y / 2), GratisIcons.Crosshair, GratisTheme.Crystal, GratisTheme.IconSize * s);
        list.AddText(labelPos + new Vector2(36 * s, (labelSize.Y - ImGui.GetFontSize()) / 2), ImGui.GetColorU32(GratisTheme.Primary), labelText);
    }
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
        ChipMin = pos; ChipMax = pos + size; labelText = text; labelPos = pos; labelSize = size;
        using var motion = UiMotion.Surface("##measure-label", pos);
        MapWindowDrawing.DrawSurface(list, pos, pos + size, GratisTheme.CardRadius * s, surfaceOpacity: 1);
        GratisIcons.Draw(list, pos + new Vector2(18 * s, size.Y / 2), GratisIcons.Crosshair, GratisTheme.Crystal, GratisTheme.IconSize * s);
        list.AddText(pos + new Vector2(36 * s, (size.Y - ImGui.GetFontSize()) / 2), ImGui.GetColorU32(GratisTheme.Primary), text);
        if (ImGui.IsMouseHoveringRect(pos, pos + size, false)) UiMotion.TooltipText(FormattableString.Invariant($"({first.X:0.0}, {first.Y:0.0}) → ({second.X:0.0}, {second.Y:0.0}) · Right-click to copy"));
    }
}
