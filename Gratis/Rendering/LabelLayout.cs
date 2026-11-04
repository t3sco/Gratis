using System.Collections.Generic;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class LabelLayout {
    private const float Margin = 3;
    private static readonly List<(Vector2 Min, Vector2 Max)> placed = [];
    private static int frame = -1;
    private static MapDrawContext? owner;

    private static readonly List<(Vector2 Min, Vector2 Max)> reserved = [];
    internal static IReadOnlyList<(Vector2 Min, Vector2 Max)> Reserved => reserved;
    internal static IReadOnlyList<(Vector2 Min, Vector2 Max)> Placed => placed;

    public static void Begin() {
        frame = ImGui.GetFrameCount();
        owner = MapDrawContext.Current;
        placed.Clear();
        reserved.Clear();
    }

    private static void EnsureFrame() {
        if (frame != ImGui.GetFrameCount() || !ReferenceEquals(owner, MapDrawContext.Current)) {
            Begin();
        }
    }

    public static void Reserve(Vector2 min, Vector2 max) {
        EnsureFrame();
        reserved.Add((min, max));
    }

    private static bool Intersects(Vector2 min, Vector2 max, Vector2 otherMin, Vector2 otherMax, float margin) =>
        min.X < otherMax.X + margin && max.X > otherMin.X - margin
        && min.Y < otherMax.Y + margin && max.Y > otherMin.Y - margin;

    // Try the native anchor first, then nearby positions. With hiding disabled,
    // retain the native position if every candidate is occupied.
    public static bool Place(ref Vector2 position, Vector2 size, bool hideOverlapping) {
        EnsureFrame();
        var origin = position;
        var gap = Margin * MapDrawContext.Current.GlobalScale + 2;
        for (var i = 0; i < 9; i++) {
            var offset = i switch {
                1 => new Vector2(0, -size.Y - gap), 2 => new Vector2(0, size.Y + gap),
                3 => new Vector2(-size.X - gap, 0), 4 => new Vector2(size.X + gap, 0),
                5 => new Vector2(-size.X - gap, -size.Y - gap),
                6 => new Vector2(size.X + gap, -size.Y - gap),
                7 => new Vector2(-size.X - gap, size.Y + gap),
                8 => new Vector2(size.X + gap, size.Y + gap), _ => Vector2.Zero,
            };
            var candidate = origin + offset;
            if (!Gratis.Classes.MapShape.ContainsSymbol(candidate + size / 2, size / 2 + Vector2.One))
                continue;
            if (TryPlace(candidate - Vector2.One, candidate + size + Vector2.One)) {
                position = candidate;
                return true;
            }
        }
        return !hideOverlapping;
    }

    public static bool TryPlace(Vector2 min, Vector2 max) {
        EnsureFrame();
        foreach (var (otherMin, otherMax) in reserved)
            if (Intersects(min, max, otherMin, otherMax, Margin * MapDrawContext.Current.GlobalScale))
                return false;
        var pad = new Vector2(Margin * MapDrawContext.Current.GlobalScale);
        foreach (var (otherMin, otherMax) in placed)
            if (min.X < otherMax.X + pad.X && max.X > otherMin.X - pad.X
                && min.Y < otherMax.Y + pad.Y && max.Y > otherMin.Y - pad.Y)
                return false;
        placed.Add((min, max));
        return true;
    }
}
