using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif
using Gratis.Rendering;
using Math = global::System.Math;
using MathF = global::System.MathF;

namespace Gratis.Classes;

public static class MapShape {
    public static float Current { get; private set; }

    public static float Rounding(Vector2 size) {
        var config = MapDrawContext.Current.Config;
        var half = MathF.Min(size.X, size.Y) * 0.5f;
        var radius = config.IsGratis && config.NeutralFrame ? config.FlatCornerRadius : config.MapRounding;
        return config.MapCircle ? half : MathF.Min(radius * MapDrawContext.Current.GlobalScale, half);
    }

    public static void Update(Vector2 size) => Current = Rounding(size);

    public static (Vector2 Min, Vector2 Max) Bounds(Vector2 position, Vector2 size) {
        if (MapDrawContext.Current.Config is { IsGratis: true, MapCircle: true }) {
            var half = new Vector2(MathF.Min(size.X, size.Y) / 2);
            var center = position + size / 2;
            return (center - half, center + half);
        }
        return (position, position + size);
    }

    public static float FeatherWidth => MapDrawContext.Current.Config is { IsGratis: true, FeatherEnabled: true } config
        ? Math.Clamp(float.IsFinite(config.FeatherWidth) ? config.FeatherWidth : 6, 0, 48)
            * MapDrawContext.Current.GlobalScale
        : 0;

    public static bool ContainsSymbol(Vector2 center, Vector2 halfSize) {
        if (FeatherWidth <= 0)
            return true;
        var (min, max) = Bounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        var radius = Rounding(max - min);
        var feather = FeatherWidth;
        return FeatheredMapDrawing.Depth(center - halfSize, min, max, radius) >= feather
            && FeatheredMapDrawing.Depth(center + halfSize, min, max, radius) >= feather
            && FeatheredMapDrawing.Depth(center + new Vector2(halfSize.X, -halfSize.Y), min, max, radius) >= feather
            && FeatheredMapDrawing.Depth(center + new Vector2(-halfSize.X, halfSize.Y), min, max, radius) >= feather;
    }

    public static bool Contains(Vector2 screenPoint) {
        if (FeatherWidth > 0)
            return ContainsSymbol(screenPoint, Vector2.Zero);
        if (Current <= 0.0f)
            return true;

        var (min, max) = Bounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        var inner = new Vector2(Math.Clamp(screenPoint.X, min.X + Current, max.X - Current),
            Math.Clamp(screenPoint.Y, min.Y + Current, max.Y - Current));
        return Vector2.DistanceSquared(inner, screenPoint) <= Current * Current;
    }
}