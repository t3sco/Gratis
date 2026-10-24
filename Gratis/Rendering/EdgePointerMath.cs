using System;
using System.Collections.Generic;
using System.Numerics;

namespace Gratis.Rendering;

public readonly record struct EdgePointerBounds(Vector2 Min, Vector2 Max)
{
    public bool Contains(Vector2 p) => p.X >= Min.X && p.Y >= Min.Y && p.X <= Max.X && p.Y <= Max.Y;
}

public static class EdgePointerMath
{
    public static float Outside(Vector2 point, Vector2 min, Vector2 max, bool circle)
    {
        var half = (max - min) / 2;
        var delta = Vector2.Abs(point - (min + max) / 2);
        return circle ? delta.Length() - MathF.Min(half.X, half.Y) : MathF.Max(delta.X - half.X, delta.Y - half.Y);
    }

    public static bool Project(Vector2 target, Vector2 min, Vector2 max, bool circle,
        Vector2 halfChip, float inset, IReadOnlyList<EdgePointerBounds> obstacles, out Vector2 point)
    {
        point = default;
        if (!float.IsFinite(target.X) || !float.IsFinite(target.Y)) return false;
        var center = (min + max) / 2;
        var half = (max - min) / 2 - halfChip - new Vector2(inset);
        if (half.X <= 0 || half.Y <= 0) return false;
        var delta = target - center;
        if (delta.LengthSquared() < .0001f) delta = -Vector2.UnitY;
        var start = MathF.Atan2(delta.Y, delta.X);
        for (var i = 0; i < 721; i++) {
            var angle = start + (i == 0 ? 0 : (i % 2 == 0 ? -1 : 1) * ((i + 1) / 2) * MathF.PI / 360);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var reach = circle ? MathF.Min((max.X - min.X) / 2, (max.Y - min.Y) / 2) - halfChip.Length() - inset
                : MathF.Min(half.X / MathF.Max(.000001f, MathF.Abs(direction.X)), half.Y / MathF.Max(.000001f, MathF.Abs(direction.Y)));
            if (reach <= 0) return false;
            var candidate = center + direction * reach;
            var blocked = false;
            foreach (var obstacle in obstacles)
                if (new EdgePointerBounds(obstacle.Min - halfChip, obstacle.Max + halfChip).Contains(candidate)) { blocked = true; break; }
            if (!blocked) { point = candidate; return true; }
        }
        return false;
    }
}
