using System;
using System.Numerics;

namespace Gratis.Rendering;

public static class MeasureToolMath {
    public static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b) {
        var delta = b - a;
        var square = delta.LengthSquared();
        var t = square < .0001f ? 0 : Math.Clamp(Vector2.Dot(point - a, delta) / square, 0, 1);
        return Vector2.Distance(point, a + delta * t);
    }

    public static float Depth(Vector2 p, Vector2 min, Vector2 max, float radius) {
        var half = (max - min) / 2;
        radius = Math.Clamp(radius, 0, MathF.Min(half.X, half.Y));
        var q = Vector2.Abs(p - (min + max) / 2) - half + new Vector2(radius);
        return radius - Vector2.Max(q, Vector2.Zero).Length() - MathF.Min(MathF.Max(q.X, q.Y), 0);
    }

    public static bool Clip(
        Vector2 a, Vector2 b, Vector2 min, Vector2 max, float radius, float inset, out Vector2 start, out Vector2 end) {
        start = a;
        end = b;
        if (!float.IsFinite(a.X) || !float.IsFinite(a.Y) || !float.IsFinite(b.X) || !float.IsFinite(b.Y))
            return false;
        if (max.X <= min.X || max.Y <= min.Y)
            return false;
        float At(float t) => Depth(Vector2.Lerp(a, b, t), min, max, radius) - inset;
        var lo = 0f;
        var hi = 1f;
        for (var i = 0; i < 36; i++) {
            var l = lo + (hi - lo) / 3;
            var r = hi - (hi - lo) / 3;
            if (At(l) < At(r))
                lo = l;
            else
                hi = r;
        }
        var peak = (lo + hi) / 2;
        if (At(peak) < -.0001f)
            return false;
        if (At(0) < 0) {
            lo = 0;
            hi = peak;
            for (var i = 0; i < 28; i++) {
                var mid = (lo + hi) / 2;
                if (At(mid) >= 0)
                    hi = mid;
                else
                    lo = mid;
            }
            start = Vector2.Lerp(a, b, hi);
        }
        if (At(1) < 0) {
            lo = peak;
            hi = 1;
            for (var i = 0; i < 28; i++) {
                var mid = (lo + hi) / 2;
                if (At(mid) >= 0)
                    lo = mid;
                else
                    hi = mid;
            }
            end = Vector2.Lerp(a, b, lo);
        }
        return true;
    }
}