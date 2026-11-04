using System.Numerics;

namespace Gratis.Rendering;

/// <summary>Validation shared by monitor recovery and normalized window presets.</summary>
public static class DisplayGeometry {
    // Reject transient/minimized and corrupt host measurements before persisting geometry.
    public static bool ValidSize(Vector2 size) => WindowPresetMath.Finite(size)
        && size.X >= 1 && size.Y >= 1 && size.X <= 131072 && size.Y <= 131072;

    public static bool ValidArea(MoveWorkArea area) => WindowPresetMath.Finite(area.Min)
        && WindowPresetMath.Finite(area.Max) && global::System.MathF.Abs(area.Min.X) <= 131072
        && global::System.MathF.Abs(area.Min.Y) <= 131072 && ValidSize(area.Max - area.Min);

    public static MoveWorkArea SafeArea(MoveWorkArea area) => ValidArea(area)
        ? area : new(Vector2.Zero, new Vector2(1920, 1080));

    public static Vector2 Minimum(Vector2 minimum) => WindowPresetMath.Finite(minimum)
        ? Vector2.Clamp(minimum, Vector2.One, new Vector2(131072)) : new Vector2(400, 250);
}
