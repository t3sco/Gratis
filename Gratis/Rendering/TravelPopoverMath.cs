using System;
using System.Collections.Generic;
using System.Numerics;

namespace Gratis.Rendering;

public static class TravelPopoverMath {
    public static Vector4 Resolve(Vector2 min, Vector2 max, Vector2 size, Vector2 target,
        IReadOnlyList<Vector4> obstacles, float gap, bool allowOverlap = false) {
        if (size.X > max.X - min.X || size.Y > max.Y - min.Y)
            return Vector4.Zero;
        List<Vector2> candidates = [new(target.X + gap, target.Y - size.Y / 2),
            new(target.X - gap - size.X, target.Y - size.Y / 2),
            new(target.X - size.X / 2, target.Y + gap), new(target.X - size.X / 2, target.Y - gap - size.Y)];
        foreach (var obstacle in obstacles) {
            candidates.Add(new(target.X - size.X / 2, obstacle.Y - gap - size.Y));
            candidates.Add(new(target.X - size.X / 2, obstacle.W + gap));
            candidates.Add(new(obstacle.X - gap - size.X, target.Y - size.Y / 2));
            candidates.Add(new(obstacle.Z + gap, target.Y - size.Y / 2));
        }
        var best = Vector4.Zero;
        var distance = float.PositiveInfinity;
        var crowded = Vector4.Zero;
        var crowdedOverlap = float.PositiveInfinity;
        var crowdedDistance = float.PositiveInfinity;
        foreach (var candidate in candidates) {
            var p = Vector2.Clamp(candidate, min, max - size);
            var rect = new Vector4(p.X, p.Y, p.X + size.X, p.Y + size.Y);
            var overlap = 0f;
            foreach (var obstacle in obstacles)
                overlap += OverlapArea(rect, obstacle + new Vector4(-4, -4, 4, 4));
            var score = Vector2.DistanceSquared(target, Vector2.Clamp(target, p, p + size))
                + Vector2.DistanceSquared(p, candidate);
            if (overlap == 0) {
                if (score < distance) {
                    distance = score;
                    best = rect;
                }
            }
            else if (overlap < crowdedOverlap || overlap == crowdedOverlap && score < crowdedDistance) {
                crowdedOverlap = overlap;
                crowdedDistance = score;
                crowded = rect;
            }
        }
        // Small windows can leave no clear spot. A card the user asked for may cover some chrome; a click must not do nothing.
        return best != Vector4.Zero || !allowOverlap ? best : crowded;
    }

    private static float OverlapArea(Vector4 a, Vector4 b) =>
        MathF.Max(0, MathF.Min(a.Z, b.Z) - MathF.Max(a.X, b.X))
        * MathF.Max(0, MathF.Min(a.W, b.W) - MathF.Max(a.Y, b.Y));
}