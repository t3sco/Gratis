using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

internal static class FeatheredMapDrawing {
    private const int Bands = 8;
    private const int MaxCornerSegments = 128;
    internal static int LastVertexCount { get; private set; }

    internal static float Depth(Vector2 point, Vector2 min, Vector2 max, float radius) {
        if (MapDrawContext.Current.Config.MapCircle)
            return MathF.Min(max.X - min.X, max.Y - min.Y) / 2 - Vector2.Distance(point, (min + max) / 2);
        var half = (max - min) / 2;
        var q = Vector2.Abs(point - (min + max) / 2) - half + new Vector2(radius);
        return radius - Vector2.Max(q, Vector2.Zero).Length() - MathF.Min(MathF.Max(q.X, q.Y), 0);
    }

    internal static void Draw(ImDrawListPtr list, DrawTexture texture, Vector2 imageMin, Vector2 imageSize, Vector2 min,
        Vector2 max, float radius, float feather) {
        LastVertexCount = 0;
        var visibleMin = Vector2.Max(imageMin, min);
        var visibleMax = Vector2.Min(imageMin + imageSize, max);
        if (visibleMax.X <= visibleMin.X || visibleMax.Y <= visibleMin.Y)
            return;
        var center = (min + max) / 2;
        var half = MathF.Min(max.X - min.X, max.Y - min.Y) / 2;
        if (half <= 0)
            return;
        var circle = MapDrawContext.Current.Config.MapCircle;
        if (circle) {
            radius = half;
            min = center - new Vector2(half);
            max = center + new Vector2(half);
        }
        radius = Math.Clamp(radius, 0, half);
        feather = MathF.Min(feather, half);
        var segments = radius <= .1f ? 1
            : Math.Clamp((int)MathF.Ceiling(MathF.PI / 2 / (2 * MathF.Acos(Math.Clamp(1 - .1f / radius, -1, 1)))), 1,
                MaxCornerSegments);
        var count = circle ? 4 * segments : 4 * (segments + 1);
        var vertices = 1 + count * (Bands + 1);
        list.PushClipRect(visibleMin, visibleMax, true);
        DrawBinding.PushTexture(list, texture);
        list.PrimReserve(count * (Bands * 6 + 3), vertices);
        var first = DrawBinding.VertexIndex(list);
        var white = ImGui.GetColorU32(Vector4.One);
        list.PrimWriteVtx(center, (center - imageMin) / imageSize, white);
        for (var band = 0; band <= Bands; band++) {
            var t = band / (float)Bands;
            var inset = feather * t;
            var lo = min + new Vector2(inset);
            var hi = max - new Vector2(inset);
            var r = MathF.Max(0, radius - inset);
            var color = ImGui.GetColorU32(Vector4.One with { W = t * t * (3 - 2 * t) });
            if (circle) {
                for (var point = 0; point < count; point++) {
                    var angle = (point + .37f) * MathF.Tau / count;
                    var p = center + r * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    list.PrimWriteVtx(p, (p - imageMin) / imageSize, color);
                }
                continue;
            }
            for (var corner = 0; corner < 4; corner++) {
                var origin = new Vector2(corner is 0 or 3 ? lo.X + r : hi.X - r, corner < 2 ? lo.Y + r : hi.Y - r);
                for (var step = 0; step <= segments; step++) {
                    var angle = MathF.PI + (corner + step / (float)segments) * MathF.PI / 2;
                    var p = origin + r * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    list.PrimWriteVtx(p, (p - imageMin) / imageSize, color);
                }
            }
        }
        for (var index = 0; index < count; index++) {
            var next = (index + 1) % count;
            for (var band = 0; band < Bands; band++) {
                var a = (ushort)(first + 1 + band * count + index);
                var b = (ushort)(first + 1 + band * count + next);
                var c = (ushort)(first + 1 + (band + 1) * count + next);
                var d = (ushort)(first + 1 + (band + 1) * count + index);
                list.PrimWriteIdx(a);
                list.PrimWriteIdx(b);
                list.PrimWriteIdx(c);
                list.PrimWriteIdx(a);
                list.PrimWriteIdx(c);
                list.PrimWriteIdx(d);
            }
            list.PrimWriteIdx((ushort)first);
            list.PrimWriteIdx((ushort)(first + 1 + Bands * count + index));
            list.PrimWriteIdx((ushort)(first + 1 + Bands * count + next));
        }
        DrawBinding.PopTexture(list);
        list.PopClipRect();
        LastVertexCount = vertices;
    }
}