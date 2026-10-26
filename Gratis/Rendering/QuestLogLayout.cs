using System;
using System.Numerics;

namespace Gratis.Rendering;

public readonly record struct QuestLogLayout(Vector4 Toggle, Vector4 Panel, bool Compact) {
    public static QuestLogLayout Resolve(Vector2 min, Vector2 max, float top, float width, float scale) {
        var gap = GratisTheme.Gap * scale;
        var button = GratisTheme.ToolbarHeight * scale;
        var toggle = new Vector4(MathF.Max(min.X, max.X - button), top, max.X, top + button);
        var available = MathF.Max(1, max.X - min.X);
        var compact = available < (width + 240) * scale;
        var actual = MathF.Min(available, (compact ? MathF.Min(width, 288) : width) * scale);
        var y = MathF.Min(max.Y, top + button + gap);
        return new(toggle, new(max.X - actual, y, max.X, max.Y), compact);
    }
}
