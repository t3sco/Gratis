using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public enum FramePreset {
    Hairline, DoubleRing, InsetGlow, NotchedCorners, SoftShadow, None
}

internal static class FramePresetDrawing {
    internal static void Draw(ImDrawListPtr list, Vector2 min, Vector2 max, float radius, MapDrawingConfig config,
        float scale) {
        var shortSide = MathF.Min(max.X - min.X, max.Y - min.Y);
        if (shortSide <= 4 || config.FramePreset == FramePreset.None)
            return;
        var unit = MathF.Min(scale, shortSide / 64);
        var accent = config.AccentColor;
        void Stroke(float inset, Vector4 color) =>
            FrameMaterialDrawing.DrawStroke(list, min + new Vector2(inset * unit), max - new Vector2(inset * unit),
                MathF.Max(0, radius - inset * unit), unit, color);

        switch (config.FramePreset) {
            case FramePreset.DoubleRing:
                Stroke(0, new Vector4(1, 1, 1, .14f));
                Stroke(4, Vector4.Lerp(Vector4.One, accent with { W = 1 }, .65f) with { W = .70f });
                break;
            case FramePreset.InsetGlow:
                MapWindowDrawing.DrawGradientRing(list, min, max, radius, 10 * unit, accent with { W = .65f }, 2);
                Stroke(0, new Vector4(1, 1, 1, .14f));
                break;
            case FramePreset.NotchedCorners:
                DrawBrackets(list, min, max, radius, unit, config.MapCircle, accent);
                break;
            case FramePreset.SoftShadow:
                DrawShadow(min, max, radius, unit);
                break;
            default:
                Stroke(0, new Vector4(1, 1, 1, .14f));
                break;
        }
    }

    private static void DrawShadow(Vector2 min, Vector2 max, float radius, float unit) {
        var list = ImGui.GetBackgroundDrawList();
        var offset = new Vector2(0, 6 * unit);
        const int layers = 24;
        var previous = 0f;
        for (var layer = layers - 1; layer >= 0; layer--) {
            var t = layer / (float)layers;
            var opacity = .35f * (1 - t) * (1 - t);
            var alpha = (opacity - previous) / (1 - previous);
            var spread = new Vector2(18 * unit * t);
            list.AddRectFilled(min + offset - spread, max + offset + spread,
                ImGui.GetColorU32(new Vector4(0, 0, 0, alpha)), radius + spread.X);
            previous = opacity;
        }
    }

    private static void DrawBrackets(ImDrawListPtr list, Vector2 min, Vector2 max, float radius, float unit,
        bool circle, Vector4 accent) {
        var ink = ImGui.GetColorU32(accent with { W = .90f });
        var arm = 14 * unit;
        if (circle) {
            var center = (min + max) / 2;
            var r = MathF.Max(unit, radius - 2 * unit);
            var sweep = arm / r;
            for (var tick = 0; tick < 4; tick++) {
                var angle = tick * MathF.PI / 2;
                list.PathArcTo(center, r, angle - sweep / 2, angle + sweep / 2,
                    FrameMaterialDrawing.ArcSegments(r, sweep));
                list.PathStroke(ink, ImDrawFlags.None, 2 * unit);
            }
            return;
        }
        var inset = MathF.Max(2 * unit, radius * .3f);
        for (var corner = 0; corner < 4; corner++) {
            var left = corner is 0 or 3;
            var top = corner < 2;
            var point = new Vector2(left ? min.X + inset : max.X - inset, top ? min.Y + inset : max.Y - inset);
            list.PathLineTo(point + new Vector2(left ? arm : -arm, 0));
            list.PathLineTo(point);
            list.PathLineTo(point + new Vector2(0, top ? arm : -arm));
            list.PathStroke(ink, ImDrawFlags.None, 2 * unit);
        }
    }
}