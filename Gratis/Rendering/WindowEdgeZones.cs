using System;
using System.Numerics;

namespace Gratis.Rendering;

/// <summary>What a pointer over the map window's frame would do: move it (a band along an edge) or resize it (a corner).</summary>
public enum WindowEdgeZone {
    None, MoveNorth, MoveEast, MoveSouth, MoveWest, CornerNW, CornerNE, CornerSE, CornerSW
}

/// <summary>
/// Pure geometry for the window frame's grab zones. A thin band along each edge moves the window and a larger square
/// at each corner resizes it; the bands stop short of the corners so the two never compete. In circle mode the edge is
/// the rim and the corners are the rim points on the diagonals. No ImGui here: the glue passes plain numbers in.
/// </summary>
public static class WindowEdgeZones {
    public const float BandWidth = 6;
    public const float CornerSize = 14;
    // How near the pointer must be for the cue to fade in (the grab zones above are unchanged).
    public const float RevealBand = 24;
    public const float RevealCorner = 36;

    public static bool IsMove(WindowEdgeZone zone) => zone is >= WindowEdgeZone.MoveNorth and <= WindowEdgeZone.MoveWest;
    public static bool IsCorner(WindowEdgeZone zone) => zone is >= WindowEdgeZone.CornerNW and <= WindowEdgeZone.CornerSW;

    /// <summary>The corner's grab size, and the band depth. Both shrink on a window too small to hold them.</summary>
    public static (float Corner, float Band) Metrics(Vector2 size, float scale) {
        var corner = MathF.Max(0, MathF.Min(CornerSize * scale, MathF.Min(size.X, size.Y) / 2));
        return (corner, MathF.Min(BandWidth * scale, corner / 2));
    }

    /// <summary>
    /// Which zone the point is in. <paramref name="yield"/> is true while anything with a higher claim on the pointer
    /// (an item under it, a panel, a popup, the toolbar) wants it: the frame never takes priority over those.
    /// </summary>
    public static WindowEdgeZone At(Vector2 point, Vector2 min, Vector2 max, bool circle, float scale, bool yield = false) =>
        Find(point, min, max, circle, scale, yield, reveal: false);

    /// <summary>
    /// The zone the pointer is approaching: the same layout as <see cref="At"/> with a wider reach, so the edge pill or
    /// corner bracket can fade in before the pointer is close enough to grab. It never grabs anything itself.
    /// </summary>
    public static WindowEdgeZone Near(Vector2 point, Vector2 min, Vector2 max, bool circle, float scale, bool yield = false) =>
        Find(point, min, max, circle, scale, yield, reveal: true);

    private static WindowEdgeZone Find(Vector2 point, Vector2 min, Vector2 max, bool circle, float scale, bool yield,
        bool reveal) {
        if (yield || !float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(scale) || scale <= 0
            || !(max.X > min.X) || !(max.Y > min.Y))
            return WindowEdgeZone.None;
        var (corner, band) = reveal ? RevealMetrics(max - min, scale) : Metrics(max - min, scale);
        if (corner <= 0)
            return WindowEdgeZone.None;
        return circle ? AtCircle(point, min, max, corner, band) : AtRectangle(point, min, max, corner, band);
    }

    private static (float Corner, float Band) RevealMetrics(Vector2 size, float scale) {
        var corner = MathF.Max(0, MathF.Min(RevealCorner * scale, MathF.Min(size.X, size.Y) / 3));
        return (corner, MathF.Min(RevealBand * scale, corner * .67f));
    }

    private static WindowEdgeZone AtRectangle(Vector2 point, Vector2 min, Vector2 max, float corner, float band) {
        if (point.X < min.X || point.Y < min.Y || point.X > max.X || point.Y > max.Y)
            return WindowEdgeZone.None;
        var left = point.X - min.X;
        var right = max.X - point.X;
        var top = point.Y - min.Y;
        var bottom = max.Y - point.Y;
        var nearX = left < corner || right < corner;
        var nearY = top < corner || bottom < corner;
        if (nearX && nearY)
            return Corner(left < right, top < bottom);
        if (left < band)
            return WindowEdgeZone.MoveWest;
        if (right < band)
            return WindowEdgeZone.MoveEast;
        if (top < band)
            return WindowEdgeZone.MoveNorth;
        return bottom < band ? WindowEdgeZone.MoveSouth : WindowEdgeZone.None;
    }

    private static WindowEdgeZone AtCircle(Vector2 point, Vector2 min, Vector2 max, float corner, float band) {
        var center = (min + max) / 2;
        var radius = MathF.Min(max.X - min.X, max.Y - min.Y) / 2;
        var offset = point - center;
        var distance = offset.Length();
        if (distance > radius || distance < radius - corner)
            return WindowEdgeZone.None;
        var diagonal = radius * .70710678f;
        var anchor = new Vector2(offset.X < 0 ? -diagonal : diagonal, offset.Y < 0 ? -diagonal : diagonal);
        if (Vector2.Distance(offset, anchor) <= corner)
            return Corner(offset.X < 0, offset.Y < 0);
        if (distance < radius - band)
            return WindowEdgeZone.None;
        return MathF.Abs(offset.Y) > MathF.Abs(offset.X)
            ? offset.Y < 0 ? WindowEdgeZone.MoveNorth : WindowEdgeZone.MoveSouth
            : offset.X < 0 ? WindowEdgeZone.MoveWest : WindowEdgeZone.MoveEast;
    }

    private static WindowEdgeZone Corner(bool west, bool north) => west
        ? north ? WindowEdgeZone.CornerNW : WindowEdgeZone.CornerSW
        : north ? WindowEdgeZone.CornerNE : WindowEdgeZone.CornerSE;

    /// <summary>Which way a corner grows the window: -1 is the left/top edge, +1 the right/bottom edge.</summary>
    public static Vector2 Direction(WindowEdgeZone zone) => zone switch {
        WindowEdgeZone.CornerNW => new(-1, -1),
        WindowEdgeZone.CornerNE => new(1, -1),
        WindowEdgeZone.CornerSE => new(1, 1),
        WindowEdgeZone.CornerSW => new(-1, 1),
        _ => Vector2.Zero,
    };

    /// <summary>The rectangle a rectangular zone occupies, for hover feedback. Circle zones are drawn as rim arcs.</summary>
    public static (Vector2 Min, Vector2 Max) Rectangle(WindowEdgeZone zone, Vector2 min, Vector2 max, float scale) {
        var (corner, band) = Metrics(max - min, scale);
        return zone switch {
            WindowEdgeZone.MoveNorth => (new(min.X + corner, min.Y), new(max.X - corner, min.Y + band)),
            WindowEdgeZone.MoveSouth => (new(min.X + corner, max.Y - band), new(max.X - corner, max.Y)),
            WindowEdgeZone.MoveWest => (new(min.X, min.Y + corner), new(min.X + band, max.Y - corner)),
            WindowEdgeZone.MoveEast => (new(max.X - band, min.Y + corner), new(max.X, max.Y - corner)),
            WindowEdgeZone.CornerNW => (min, min + new Vector2(corner)),
            WindowEdgeZone.CornerNE => (new(max.X - corner, min.Y), new(max.X, min.Y + corner)),
            WindowEdgeZone.CornerSE => (max - new Vector2(corner), max),
            WindowEdgeZone.CornerSW => (new(min.X, max.Y - corner), new(min.X + corner, max.Y)),
            _ => (Vector2.Zero, Vector2.Zero),
        };
    }

    /// <summary>
    /// The window rectangle after dragging <paramref name="corner"/> by <paramref name="delta"/>, from the rectangle the
    /// drag started with. The opposite corner stays where it was. The size stays between <paramref name="minimum"/>
    /// (never forcing an already smaller saved window up) and the work area (never forcing a larger one down), and the
    /// dragged corner is kept on the work area. A circle keeps the aspect it started with.
    /// </summary>
    public static (Vector2 Position, Vector2 Size) Resize(WindowEdgeZone corner, Vector2 startPosition,
        Vector2 startSize, Vector2 delta, Vector2 minimum, MoveWorkArea area, bool keepAspect) {
        var direction = Direction(corner);
        if (direction == Vector2.Zero || !Finite(startPosition) || !Finite(startSize) || !Finite(delta)
            || startSize.X <= 0 || startSize.Y <= 0)
            return (startPosition, startSize);
        area = DisplayGeometry.SafeArea(area);
        minimum = DisplayGeometry.Minimum(minimum);
        var fixedEdge = new Vector2(direction.X > 0 ? startPosition.X : startPosition.X + startSize.X,
            direction.Y > 0 ? startPosition.Y : startPosition.Y + startSize.Y);
        var low = Vector2.Min(minimum, startSize);
        var high = new Vector2(Limit(fixedEdge.X, direction.X, startSize.X, area.Min.X, area.Max.X),
            Limit(fixedEdge.Y, direction.Y, startSize.Y, area.Min.Y, area.Max.Y));
        high = Vector2.Max(high, low);
        var wanted = startSize + direction * delta;
        Vector2 size;
        if (keepAspect) {
            var factor = (wanted.X / startSize.X + wanted.Y / startSize.Y) / 2;
            var smallest = MathF.Max(low.X / startSize.X, low.Y / startSize.Y);
            var largest = MathF.Min(high.X / startSize.X, high.Y / startSize.Y);
            // Whole pixels: ImGui truncates a window's size, which would otherwise nudge the aspect every frame.
            var scaled = startSize * Math.Clamp(factor, smallest, MathF.Max(smallest, largest));
            size = new(MathF.Round(scaled.X), MathF.Round(scaled.Y));
        }
        else
            size = Vector2.Clamp(wanted, low, high);
        return (new(direction.X > 0 ? fixedEdge.X : fixedEdge.X - size.X,
            direction.Y > 0 ? fixedEdge.Y : fixedEdge.Y - size.Y), size);
    }

    // The most the size can grow on one axis: the dragged edge may not leave the work area, but a window that already
    // extends past it is never forced to shrink.
    private static float Limit(float fixedEdge, float direction, float start, float areaMin, float areaMax) =>
        MathF.Max(start, MathF.Min(areaMax - areaMin, direction > 0 ? areaMax - fixedEdge : fixedEdge - areaMin));

    private static bool Finite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);
}
