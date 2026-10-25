using System;
using System.Numerics;

namespace Gratis.Rendering;

public static class ChromePopoverMath
{
    public static Vector4 Place(Vector4 anchor, Vector2 requested, Vector2 min, Vector2 max, float gap, bool centered = true)
    {
        var size = Vector2.Min(requested, Vector2.Max(Vector2.One, max - min));
        var below = max.Y - anchor.W - gap;
        var above = anchor.Y - min.Y - gap;
        var flip = below < size.Y && above > below;
        size.Y = MathF.Min(size.Y, MathF.Max(1, flip ? above : below));
        var x = centered ? (anchor.X + anchor.Z - size.X) / 2 : anchor.X;
        var y = flip ? anchor.Y - gap - size.Y : anchor.W + gap;
        var pos = Vector2.Clamp(new(x, y), min, Vector2.Max(min, max - size));
        return new(pos.X, pos.Y, pos.X + size.X, pos.Y + size.Y);
    }
}
