using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Math = global::System.Math;
using MathF = global::System.MathF;

namespace Gratis.Classes;

public static class MapShape
{
    public static float Current { get; private set; }

    public static float Rounding(Vector2 size)
    {
        var config = System.SystemConfig;
        var half = MathF.Min(size.X, size.Y) * 0.5f;
        return config.MapCircle ? half : MathF.Min(config.MapRounding * ImGuiHelpers.GlobalScale, half);
    }

    public static void Update(Vector2 size) => Current = Rounding(size);

    public static bool Contains(Vector2 screenPoint)
    {
        if (Current <= 0.0f) return true;

        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();
        var inner = new Vector2(Math.Clamp(screenPoint.X, min.X + Current, max.X - Current), Math.Clamp(screenPoint.Y, min.Y + Current, max.Y - Current));
        return Vector2.DistanceSquared(inner, screenPoint) <= Current * Current;
    }
}
