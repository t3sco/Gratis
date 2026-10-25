using System;
using System.Numerics;
using Gratis.Classes;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

internal static class SpotlightDrawing
{
    private static bool Fits(Vector2 center, Vector2 half) => MapShape.ContainsSymbol(center, half) &&
        MapShape.Contains(center - half) && MapShape.Contains(center + half) &&
        MapShape.Contains(center + new Vector2(half.X, -half.Y)) &&
        MapShape.Contains(center + new Vector2(-half.X, half.Y));

    public static void DrawMapOverlay(Spotlight spotlight, Vector2 origin, float zoom)
    {
        if (spotlight.Opacity <= 0 || !spotlight.OwnsViewport) return;
        var list = ImGui.GetWindowDrawList();
        var (min, max) = MapShape.Bounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        var center = (min + max) / 2;
        var half = (max - min) / 2;
        var radius = MapShape.Rounding(max - min);
        var target = origin + spotlight.TargetTexture * zoom;
        var player = origin + spotlight.PlayerTexture * zoom;
        var focus = spotlight.IncludePlayer ? Vector2.Lerp(player, target, .65f) : target;
        var aperture = new Vector2(100, 88) * MapDrawContext.Current.GlobalScale;
        if (spotlight.IncludePlayer) aperture += Vector2.Abs(target - player) * .5f;
        uint Color(Vector2 point) {
            var d = ((point - focus) / aperture).LengthSquared();
            var vignette = .18f + .82f * (1 - MathF.Exp(-d * 1.5f));
            var depth = FeatheredMapDrawing.Depth(point, min, max, radius);
            var feather = Math.Clamp(depth / MathF.Max(1, MapShape.FeatherWidth), 0, 1);
            return ImGui.GetColorU32(new Vector4(.015f, .025f, .045f, spotlight.Dim * spotlight.Opacity * vignette * feather));
        }
        const int segments = 128, bands = 18;
        Span<Vector2> boundary = stackalloc Vector2[segments];
        for (var i = 0; i < segments; i++) {
            var angle = MathF.Tau * i / segments;
            var ray = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var lo = 0f; var hi = half.Length();
            for (var step = 0; step < 16; step++) {
                var mid = (lo + hi) / 2;
                if (FeatheredMapDrawing.Depth(center + ray * mid, min, max, radius) >= 0) lo = mid; else hi = mid;
            }
            boundary[i] = ray * lo;
        }
        DrawBinding.PushFontTexture(list);
        var uv = ImGui.GetFontTexUvWhitePixel();
        list.PrimReserve(segments * (3 + (bands - 1) * 6), 1 + segments * bands);
        var first = DrawBinding.VertexIndex(list);
        list.PrimWriteVtx(center, uv, Color(center));
        for (var band = 1; band <= bands; band++)
            for (var i = 0; i < segments; i++) {
                var p = center + boundary[i] * (band / (float)bands);
                list.PrimWriteVtx(p, uv, Color(p));
            }
        for (var i = 0; i < segments; i++) {
            var next = (i + 1) % segments;
            list.PrimWriteIdx((ushort)first);
            list.PrimWriteIdx((ushort)(first + 1 + i));
            list.PrimWriteIdx((ushort)(first + 1 + next));
            for (var band = 1; band < bands; band++) {
                var a = (ushort)(first + 1 + (band - 1) * segments + i);
                var b = (ushort)(first + 1 + (band - 1) * segments + next);
                var c = (ushort)(first + 1 + band * segments + next);
                var d = (ushort)(first + 1 + band * segments + i);
                list.PrimWriteIdx(a); list.PrimWriteIdx(b); list.PrimWriteIdx(c);
                list.PrimWriteIdx(a); list.PrimWriteIdx(c); list.PrimWriteIdx(d);
            }
        }
        DrawBinding.PopTexture(list);
        if (!spotlight.GuideLine || !spotlight.IncludePlayer) return;
        var length = Vector2.Distance(player, target);
        for (var distance = 18f; distance < length - 18; distance += 8 * MapDrawContext.Current.GlobalScale) {
            var point = Vector2.Lerp(player, target, distance / length);
            if (Fits(point, new Vector2(2)))
                list.AddCircleFilled(point, MapDrawContext.Current.GlobalScale,
                    ImGui.GetColorU32(MapDrawContext.Current.Config.AccentColor with { W = .45f * spotlight.Opacity }), 8);
        }
    }

    public static void DrawMarkerGlow(MarkerInfo marker)
    {
        var context = MapDrawContext.Current;
        if (context.Spotlight is not { Opacity: > 0 } s || !s.Matches(marker)) return;
        var center = marker.Position + marker.Offset + ImGui.GetWindowPos();
        var scale = context.GlobalScale;
        var list = ImGui.GetWindowDrawList();
        var accent = context.Config.AccentColor;
        for (var layer = 12; layer >= 1; layer--) {
            var radius = (9 + layer * 1.8f) * scale;
            if (!Fits(center, new Vector2(radius))) continue;
            list.AddCircleFilled(center, radius, ImGui.GetColorU32(accent with { W = .016f * s.Opacity }), 64);
        }
        var staticRadius = 12 * scale;
        if (Fits(center, new Vector2(staticRadius + 2))) {
            list.AddCircle(center, staticRadius, ImGui.GetColorU32(GratisTheme.Outline with { W = .65f * s.Opacity }), 48, 3 * scale);
            list.AddCircle(center, staticRadius, ImGui.GetColorU32(accent with { W = .8f * s.Opacity }), 48, scale);
        }
        if (!s.Pulse || context.Config.ReduceMotion) return;
        for (var ring = 0; ring < 2; ring++) {
            var (radius, alpha) = MotionMath.Ring(s.Age % 1.6f, ring, 1.6f);
            radius *= scale;
            if (alpha > 0 && Fits(center, new Vector2(radius + 2)))
                list.AddCircle(center, radius, ImGui.GetColorU32(accent with { W = alpha * s.Opacity }), 64, 1.5f * scale);
        }
    }

    public static void DrawDistance(Spotlight spotlight, Vector2 origin, float zoom)
    {
        if (!spotlight.DistanceChip || !spotlight.IncludePlayer) return;
        var context = MapDrawContext.Current;
        var opacity = context.Config.EffectiveInterfaceMotion == InterfaceAnimationMode.Full ? spotlight.Opacity : spotlight.Active ? 1 : 0;
        if (opacity <= 0) return;
        var scale = GratisTheme.ChromeScale;
        using var font = context.GetChromeFont(GratisTheme.TextSize * scale, ChromeFontRole.Chip)?.Push();
        var label = $"{spotlight.Distance:0} yalms";
        var half = new Vector2(ImGui.CalcTextSize(label).X + 24 * scale, GratisTheme.ControlHeight * scale) / 2;
        var center = origin + (spotlight.TargetTexture + spotlight.PlayerTexture) * .5f * zoom + new Vector2(0, 25) * scale;
        if (!Fits(center, half + new Vector2(2))) return;
        var list = ImGui.GetWindowDrawList();
        using var motion = UiMotion.Surface("##spotlight-distance", center, spotlight.Active);
        MapWindowDrawing.DrawSurface(list, center - half, center + half, GratisTheme.CardRadius * scale, opacity, surfaceOpacity: 1);
        TextDrawing.Draw(list, center - half + new Vector2(12 * scale, (half.Y * 2 - ImGui.GetFontSize()) / 2), ImGui.GetColorU32(Vector4.One with { W = opacity }), label);
    }
}
