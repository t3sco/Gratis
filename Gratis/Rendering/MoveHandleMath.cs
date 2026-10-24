using System;
using System.Numerics;

namespace Gratis.Rendering;

public readonly record struct MoveWorkArea(Vector2 Min, Vector2 Max);
public readonly record struct MoveHandleGeometry(Vector2 Center, Vector2 HitMin, Vector2 HitMax)
{
    public bool Contains(Vector2 point) => point.X >= HitMin.X && point.X <= HitMax.X && point.Y >= HitMin.Y && point.Y <= HitMax.Y;
}

public static class MoveHandleMath
{
    public static MoveHandleGeometry Geometry(Vector2 min, Vector2 max, bool circle, float scale)
    {
        var radius = MathF.Min(max.X - min.X, max.Y - min.Y) / 2;
        var inset = circle ? radius - MathF.Sqrt(MathF.Max(0, radius * radius - 28 * scale * 28 * scale)) : 0;
        var center = new Vector2((min.X + max.X) / 2, min.Y + inset + 8 * scale);
        return new(center, center - new Vector2(28, 8) * scale, center + new Vector2(28, 8) * scale);
    }

    public static Vector2 Clamp(Vector2 position, Vector2 size, MoveWorkArea screen, MoveHandleGeometry localHandle, float scale)
    {
        var visible = Vector2.Min(size, new Vector2(48 * scale));
        var min = Vector2.Max(screen.Min - size + visible, screen.Min - localHandle.HitMin);
        var max = Vector2.Min(screen.Max - visible, screen.Max - localHandle.HitMax);
        return Vector2.Clamp(position, min, Vector2.Max(min, max));
    }

    public static Vector2 SnapDrag(Vector2 position, Vector2 size, MoveWorkArea screen, float scale)
    {
        var margin = 8 * scale;
        var grid = 8 * scale;
        var snapped = screen.Min + new Vector2(MathF.Round((position.X - screen.Min.X) / grid), MathF.Round((position.Y - screen.Min.Y) / grid)) * grid;
        var near = screen.Min + new Vector2(margin);
        var far = screen.Max - size - new Vector2(margin);
        for (var axis = 0; axis < 2; axis++) {
            var value = axis == 0 ? position.X : position.Y;
            var a = axis == 0 ? near.X : near.Y;
            var b = axis == 0 ? far.X : far.Y;
            var edge = MathF.Abs(value - a) <= MathF.Abs(value - b) ? a : b;
            if (MathF.Abs(value - edge) <= 12 * scale) {
                if (axis == 0) snapped.X = edge; else snapped.Y = edge;
            }
        }
        return snapped;
    }

    public static Vector2 NearestEdge(Vector2 position, Vector2 size, MoveWorkArea screen, float scale)
    {
        var near = screen.Min + new Vector2(8 * scale);
        var far = Vector2.Max(near, screen.Max - size - new Vector2(8 * scale));
        var x = MathF.Abs(position.X - near.X) <= MathF.Abs(position.X - far.X) ? near.X : far.X;
        var y = MathF.Abs(position.Y - near.Y) <= MathF.Abs(position.Y - far.Y) ? near.Y : far.Y;
        var dx = MathF.Abs(position.X - x); var dy = MathF.Abs(position.Y - y);
        if (MathF.Abs(dx - dy) <= 12 * scale) return new(x, y);
        return dx < dy ? new(x, position.Y) : new(position.X, y);
    }
}
