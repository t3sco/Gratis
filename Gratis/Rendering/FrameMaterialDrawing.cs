using System;
using System.Collections.Generic;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

internal static class FrameMaterialDrawing {
    private static readonly List<Vector2> outline = [];

    internal static int ArcSegments(float radius, float sweep) =>
        Math.Clamp((int)MathF.Ceiling(MathF.Abs(sweep)
            / MathF.Min(.04f, 2 * MathF.Acos(Math.Clamp(1 - .05f / MathF.Max(radius, .05f), -1, 1)))),
            1, 2048);

    private static void Outline(Vector2 min, Vector2 max, float radius) {
        outline.Clear();
        radius = Math.Clamp(radius, 0, MathF.Min(max.X - min.X, max.Y - min.Y) / 2);
        var segments = ArcSegments(radius, MathF.PI / 2);
        for (var corner = 0; corner < 4; corner++) {
            var center = new Vector2(corner is 0 or 3 ? min.X + radius : max.X - radius,
                corner < 2 ? min.Y + radius : max.Y - radius);
            for (var step = 0; step <= segments; step++) {
                var angle = MathF.PI + corner * MathF.PI / 2 + step * MathF.PI / 2 / segments;
                var point = center + radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                if (outline.Count > 0) {
                    var previous = outline[^1];
                    var pieces = Math.Clamp((int)MathF.Ceiling(Vector2.Distance(previous, point) / 3), 1, 1024);
                    for (var piece = 1; piece < pieces; piece++)
                        outline.Add(Vector2.Lerp(previous, point, piece / (float)pieces));
                    if (Vector2.DistanceSquared(previous, point) < .0001f)
                        continue;
                }
                outline.Add(point);
            }
        }
        var last = outline[^1];
        var first = outline[0];
        var closing = Math.Clamp((int)MathF.Ceiling(Vector2.Distance(last, first) / 3), 1, 1024);
        for (var piece = 1; piece < closing; piece++)
            outline.Add(Vector2.Lerp(last, first, piece / (float)closing));
        if (Vector2.DistanceSquared(outline[^1], first) < .0001f)
            outline.RemoveAt(outline.Count - 1);
    }

    private static Vector4 Mix(Vector4 a, Vector4 b, float t) {
        var alpha = a.W + (b.W - a.W) * t;
        var rgb = Vector3.Lerp(new Vector3(a.X, a.Y, a.Z) * a.W, new Vector3(b.X, b.Y, b.Z) * b.W, t);
        return new Vector4(alpha > .00001f ? rgb / alpha : Vector3.Zero, alpha);
    }

    internal static void DrawStroke(ImDrawListPtr list, Vector2 min, Vector2 max, float radius, float width,
        Vector4 color, MapDrawingConfig? conic = null, bool topHighlight = false) {
        var shortSide = MathF.Min(max.X - min.X, max.Y - min.Y);
        if (shortSide <= 2 || width <= 0)
            return;
        width = Math.Clamp(width, .1f, shortSide / 4);
        var inset = (width + 1) / 2;
        if (max.X - min.X <= inset * 2 || max.Y - min.Y <= inset * 2)
            return;
        Outline(min + new Vector2(inset), max - new Vector2(inset), MathF.Max(0, radius - inset));
        var count = outline.Count;
        var center = (min + max) / 2;
        var angle = conic is null ? 0
            : (float)((conic.RingAngle + (conic.RingRotate ? ImGui.GetTime() * conic.RingRotationSpeed : 0)) % 360)
                * MathF.PI / 180;
        var uv = ImGui.GetFontTexUvWhitePixel();
        list.PrimReserve(count * 18, count * 4);
        var firstIndex = DrawBinding.VertexIndex(list);
        for (var index = 0; index < count; index++) {
            var point = outline[index];
            var before = Vector2.Normalize(point - outline[(index + count - 1) % count]);
            var after = Vector2.Normalize(outline[(index + 1) % count] - point);
            var normal = new Vector2(before.Y + after.Y, -before.X - after.X);
            normal /= MathF.Max(Vector2.Dot(normal, new Vector2(after.Y, -after.X)), .01f);
            var tint = color;
            if (conic is not null) {
                var turns = (MathF.Atan2(point.Y - center.Y, point.X - center.X) - angle) / MathF.Tau;
                turns -= MathF.Floor(turns);
                tint = Mix(conic.RingColorA, conic.RingColorB, 1 - MathF.Abs(turns * 2 - 1));
                var distance = MathF.Min(turns, 1 - turns) * 360;
                var sweep = Math.Clamp(1 - distance / MathF.Max(conic.RingSweepWidth / 2, .5f), 0, 1);
                sweep *= sweep * (3 - 2 * sweep);
                var highlight = Vector3.Lerp(new Vector3(conic.AccentColor.X, conic.AccentColor.Y, conic.AccentColor.Z),
                    new Vector3(conic.RingAccentColor.X, conic.RingAccentColor.Y, conic.RingAccentColor.Z),
                    Math.Clamp(conic.RingIntensity, 0, 1));
                tint = Mix(tint, new Vector4(highlight, conic.RingAccentColor.W), sweep);
            }
            if (topHighlight)
                tint.W *= MathF.Pow(MathF.Max(0, -Vector2.Normalize(normal).Y), 2);
            tint.W *= MathF.Min(width, 1);
            var packed = ImGui.GetColorU32(tint);
            var clear = packed & 0x00FFFFFFu;
            var half = MathF.Max(0, width - 1) / 2;
            list.PrimWriteVtx(point + normal * (half + 1), uv, clear);
            list.PrimWriteVtx(point + normal * half, uv, packed);
            list.PrimWriteVtx(point - normal * half, uv, packed);
            list.PrimWriteVtx(point - normal * (half + 1), uv, clear);
        }
        for (var index = 0; index < count; index++) {
            var next = (index + 1) % count;
            for (var band = 0; band < 3; band++) {
                var a = (ushort)(firstIndex + index * 4 + band);
                var b = (ushort)(firstIndex + next * 4 + band);
                list.PrimWriteIdx(a);
                list.PrimWriteIdx(b);
                list.PrimWriteIdx((ushort)(b + 1));
                list.PrimWriteIdx(a);
                list.PrimWriteIdx((ushort)(b + 1));
                list.PrimWriteIdx((ushort)(a + 1));
            }
        }
    }

    internal static float GlassTint(MapDrawingConfig config) =>
        Math.Clamp(float.IsFinite(config.GlassTintOpacity) ? config.GlassTintOpacity : .04f, 0, .08f);

    internal static float GlassGrain(MapDrawingConfig config) => config.GlassGrain
        ? Math.Clamp(float.IsFinite(config.GlassGrainIntensity) ? config.GlassGrainIntensity : .018f, 0, .04f) : 0;

    internal static float GlassChromeOpacity(MapDrawingConfig config) {
        var film = 1 - (1 - GlassTint(config)) * (1 - GlassGrain(config));
        var limit = (76 / 255f - film) / (1 - film);
        return Math.Clamp((1 - limit) / (1 - GratisTheme.Surface.Z), .78f, 1);
    }

    internal static float GlassBackingOpacity(MapDrawingConfig config) {
        var film = 1 - (1 - GlassTint(config)) * (1 - GlassGrain(config));
        var limit = (39 / 255f - film) / (1 - film);
        return Math.Clamp((1 - limit) / (1 - GratisTheme.Surface.Z), .86f, 1);
    }

    internal static void DrawGlass(ImDrawListPtr list, Vector2 min, Vector2 max, float radius, float alpha) {
        var context = MapDrawContext.Current;
        var config = context.Config;
        var scale = context.GlobalScale;
        list.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1, 1, 1, GlassTint(config) * alpha)), radius);
        var grainIntensity = GlassGrain(config);
        if (grainIntensity > 0) {
            var center = (min + max) / 2;
            var half = (max - min) / 2 - new Vector2(2 * scale);
            var r = MathF.Max(0, MathF.Min(radius - 2 * scale, MathF.Min(half.X, half.Y)));
            for (var y = 3 * scale; y < max.Y - min.Y - 3 * scale; y += 4 * scale)
                for (var x = 3 * scale; x < max.X - min.X - 3 * scale; x += 4 * scale) {
                    var point = min + new Vector2(x, y);
                    var q = Vector2.Abs(point - center) - half + new Vector2(r);
                    if (Vector2.Max(q, Vector2.Zero).Length() + MathF.Min(MathF.Max(q.X, q.Y), 0) > r)
                        continue;
                    var hash = unchecked((uint)(x / scale) * 374761393u + (uint)(y / scale) * 668265263u);
                    hash = (hash ^ (hash >> 13)) * 1274126177u;
                    var grain = new Vector4((hash & 1) == 0 ? Vector3.One : Vector3.Zero,
                        grainIntensity * alpha * (.4f + .6f * (hash >> 24) / 255f));
                    list.AddRectFilled(point, point + new Vector2(scale), ImGui.GetColorU32(grain));
                }
        }
        DrawStroke(list, min, max, radius, scale, new Vector4(1, 1, 1, config.GlassHighlight * alpha));
        DrawStroke(list, min + new Vector2(scale), max - new Vector2(scale), MathF.Max(0, radius - scale), scale,
            new Vector4(1, 1, 1, config.GlassHighlight * alpha), topHighlight: true);
    }
}