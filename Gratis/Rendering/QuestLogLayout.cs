using System;
using System.Numerics;

namespace Gratis.Rendering;

public readonly record struct QuestLogLayout(Vector4 Toggle, Vector4 Panel, bool Compact) {
    public Vector4 Pin { get; init; }
    public Vector4 Detail { get; init; }
    public bool DetailOverlay { get; init; }

    public static QuestLogLayout Resolve(Vector2 min, Vector2 max, float top, float width, float scale) {
        var gap = GratisTheme.Gap * scale;
        var button = GratisTheme.ToolbarHeight * scale;
        var toggle = new Vector4(MathF.Max(min.X, max.X - button), top, max.X, top + button);
        var available = MathF.Max(1, max.X - min.X);
        var compact = available < (width + 240) * scale;
        var actual = MathF.Min(available, (compact ? MathF.Min(width, 288) : width) * scale);
        var y = MathF.Min(max.Y, top + button + gap);
        var bottom = MathF.Max(y, max.Y - (BottomCardsDrawing.Height * scale + gap));
        var panel = new Vector4(max.X - actual, y, max.X, bottom);
        var detailWidth = MathF.Min(360 * scale, available);
        var overlay = available < actual + gap + detailWidth + 160 * scale;
        var detail = overlay ? panel : new Vector4(panel.X - gap - detailWidth, y, panel.X - gap, bottom);
        return new(toggle, panel, compact) {
            Pin = toggle - new Vector4(button + gap, 0, button + gap, 0),
            Detail = detail, DetailOverlay = overlay,
        };
    }

    public QuestLogLayout AvoidChrome(Vector4 toolbar, Vector4 title, Vector4 coordinates, float scale) {
        var top = Panel.Y;
        var bottom = Panel.W;
        var middle = (top + bottom) / 2;
        var gap = GratisTheme.Gap * scale;
        void Reserve(Vector4 bounds) {
            if (bounds.Z <= bounds.X || bounds.W <= bounds.Y)
                return;
            if ((bounds.Y + bounds.W) / 2 < middle)
                top = MathF.Max(top, bounds.W + gap);
            else
                bottom = MathF.Min(bottom, bounds.Y - gap);
        }
        Reserve(toolbar);
        Reserve(title);
        Reserve(coordinates);
        bottom = MathF.Max(top, bottom);
        return this with { Panel = Panel with { Y = top, W = bottom }, Detail = Detail with { Y = top, W = bottom } };
    }
}
