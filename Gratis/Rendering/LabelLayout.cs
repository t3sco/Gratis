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
        if (!ReferenceEquals(owner, MapDrawContext.Current) || memory.Count > 512)
            memory.Clear();
        owner = MapDrawContext.Current;
        placed.Clear();
        reserved.Clear();
        occurrences.Clear();
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

    // A label keeps the position it last used while that position still fits, so a label on the edge of an icon
    // does not flip between positions every frame while the map scrolls under a moving character. It returns to
    // its native anchor only after that anchor has been free for ReturnFrames in a row.
    private const int ReturnFrames = 30;
    private const int ForgetFrames = 8;
    private struct Memory { public int Index; public int NativeFree; public int LastFrame; }
    private static readonly Dictionary<(int Text, int Nth), Memory> memory = [];
    private static readonly Dictionary<int, int> occurrences = [];

    private static Vector2 Offset(int i, Vector2 size, float gap) => i switch {
        1 => new Vector2(0, -size.Y - gap), 2 => new Vector2(0, size.Y + gap),
        3 => new Vector2(-size.X - gap, 0), 4 => new Vector2(size.X + gap, 0),
        5 => new Vector2(-size.X - gap, -size.Y - gap),
        6 => new Vector2(size.X + gap, -size.Y - gap),
        7 => new Vector2(-size.X - gap, size.Y + gap),
        8 => new Vector2(size.X + gap, size.Y + gap), _ => Vector2.Zero,
    };

    private static bool Fits(Vector2 candidate, Vector2 size) =>
        Gratis.Classes.MapShape.ContainsSymbol(candidate + size / 2, size / 2 + Vector2.One)
        && !Collides(candidate - Vector2.One, candidate + size + Vector2.One);

    // Try the remembered position, then the native anchor and nearby positions. With hiding disabled,
    // retain the native position if every candidate is occupied. A key of 0 means no memory.
    public static bool Place(ref Vector2 position, Vector2 size, bool hideOverlapping, int key = 0) {
        EnsureFrame();
        var origin = position;
        var gap = Margin * MapDrawContext.Current.GlobalScale + 2;
        var slot = (Text: key, Nth: 0);
        var known = false;
        var state = default(Memory);
        if (key != 0) {
            occurrences.TryGetValue(key, out var nth);
            occurrences[key] = nth + 1;
            slot.Nth = nth;
            known = memory.TryGetValue(slot, out state) && frame - state.LastFrame <= ForgetFrames;
        }
        var preferred = 0;
        if (known && state.Index != 0) {
            state.NativeFree = Fits(origin, size) ? state.NativeFree + 1 : 0;
            if (state.NativeFree < ReturnFrames)
                preferred = state.Index;
        }
        for (var n = preferred == 0 ? 0 : -1; n < 9; n++) {
            var i = n < 0 ? preferred : n;
            var candidate = origin + Offset(i, size, gap);
            if (!Gratis.Classes.MapShape.ContainsSymbol(candidate + size / 2, size / 2 + Vector2.One))
                continue;
            if (TryPlace(candidate - Vector2.One, candidate + size + Vector2.One)) {
                position = candidate;
                if (key != 0) {
                    if (i == 0)
                        state.NativeFree = 0;
                    state.Index = i;
                    state.LastFrame = frame;
                    memory[slot] = state;
                }
                return true;
            }
        }
        return !hideOverlapping;
    }

    private static bool Collides(Vector2 min, Vector2 max) {
        var scale = MapDrawContext.Current.GlobalScale;
        foreach (var (otherMin, otherMax) in reserved)
            if (Intersects(min, max, otherMin, otherMax, Margin * scale))
                return true;
        var pad = new Vector2(Margin * scale);
        foreach (var (otherMin, otherMax) in placed)
            if (min.X < otherMax.X + pad.X && max.X > otherMin.X - pad.X
                && min.Y < otherMax.Y + pad.Y && max.Y > otherMin.Y - pad.Y)
                return true;
        return false;
    }

    public static bool TryPlace(Vector2 min, Vector2 max) {
        EnsureFrame();
        if (Collides(min, max))
            return false;
        placed.Add((min, max));
        return true;
    }
}
