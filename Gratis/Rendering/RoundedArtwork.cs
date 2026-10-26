using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

internal static class RoundedArtwork {
    public static unsafe void Gradient(ImDrawListPtr list, Vector2 min, Vector2 max, uint top, uint bottom,
        float radius, ImDrawFlags flags = ImDrawFlags.None) {
        var first = list.VtxBuffer.Size;
        list.AddRectFilled(min, max, 0xffffffff, radius, flags);
        var a = ImGui.ColorConvertU32ToFloat4(top);
        var b = ImGui.ColorConvertU32ToFloat4(bottom);
        for (var i = first; i < list.VtxBuffer.Size; i++) {
            var vertex = (ImDrawVert*)list.VtxBuffer.Data + i;
            var position = (Vector2*)vertex;
            var ink = (uint*)((byte*)vertex + 16);
            var color = Vector4.Lerp(a, b, Math.Clamp((position->Y - min.Y) / (max.Y - min.Y), 0, 1));
            color.W *= (*ink >> 24) / 255f;
            *ink = ImGui.ColorConvertFloat4ToU32(color);
        }
    }

    public static void Triangle(ImDrawListPtr list, Vector2 min, Vector2 max, float radius,
        Vector2 a, Vector2 b, Vector2 c, uint color) {
        if (radius <= 0) {
            list.AddTriangleFilled(a, b, c, color);
            return;
        }
        radius = MathF.Min(radius, GratisTheme.Radius.Pill(max - min));
        Span<Vector2> contour = stackalloc Vector2[36];
        for (var corner = 0; corner < 4; corner++) {
            var center = corner switch {
                0 => new Vector2(max.X - radius, min.Y + radius),
                1 => max - new Vector2(radius),
                2 => new Vector2(min.X + radius, max.Y - radius),
                _ => min + new Vector2(radius),
            };
            for (var step = 0; step <= 8; step++) {
                var angle = (corner - 1 + step / 8f) * MathF.PI / 2;
                contour[corner * 9 + step] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            }
        }
        Span<Vector2> polygon = stackalloc Vector2[48];
        Span<Vector2> clipped = stackalloc Vector2[48];
        polygon[0] = a; polygon[1] = b; polygon[2] = c;
        var count = 3;
        for (var edge = 0; edge < contour.Length && count > 0; edge++) {
            var start = contour[edge];
            var direction = contour[(edge + 1) % contour.Length] - start;
            var previous = polygon[count - 1];
            var previousSide = Cross(direction, previous - start);
            var nextCount = 0;
            for (var i = 0; i < count; i++) {
                var current = polygon[i];
                var side = Cross(direction, current - start);
                if ((side >= 0) != (previousSide >= 0))
                    clipped[nextCount++] = Vector2.Lerp(previous, current, previousSide / (previousSide - side));
                if (side >= 0)
                    clipped[nextCount++] = current;
                previous = current;
                previousSide = side;
            }
            clipped[..nextCount].CopyTo(polygon);
            count = nextCount;
        }
        if (count < 3)
            return;
        for (var i = 0; i < count; i++)
            list.PathLineTo(polygon[i]);
        list.PathFillConvex(color);
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
}
