using System;
using System.Numerics;

namespace Gratis.Rendering;

public static class ViewportMath
{
    public static (float Scale, Vector2 Offset) Constrain(MapDrawingConfig config, Vector2 frame,
        Vector2 texture, float scale, Vector2 offset)
    {
        if (!config.ClampToMap) return (scale, offset);
        var circle = config.MapCircle && config.IsGratis;
        scale = MathF.Max(float.IsFinite(scale) ? scale : 1, MinimumScale(frame, texture, circle));
        if (!float.IsFinite(offset.X) || !float.IsFinite(offset.Y)) offset = Vector2.Zero;
        return (scale, ClampOffset(offset, frame, texture, scale, circle));
    }

    public static Vector2 ClampOffset(Vector2 offset, Vector2 frame, Vector2 texture, float scale, bool circle)
    {
        var half = Vector2.Min(HalfFrame(frame, circle) / scale, texture / 2);
        return Vector2.Clamp(offset, new Vector2(1024) - texture + half, new Vector2(1024) - half);
    }

    public readonly record struct BoundsFit(float Scale, Vector2 Offset, bool ContainsBoth);

    public static BoundsFit FitBounds(Vector2 a, Vector2 b, Vector2 frame, Vector2 texture,
        bool circle, bool clamp, float maximum, Vector4 insets)
    {
        var minimum = clamp ? MinimumScale(frame, texture, circle) : .05f;
        maximum = MathF.Max(minimum, maximum);
        var half = HalfFrame(frame, circle);
        var safeHalf = Vector2.Max(new Vector2(1), half - new Vector2(
            MathF.Max(insets.X, insets.Z), MathF.Max(insets.Y, insets.W)));
        var midpoint = (a + b) / 2;
        Vector2 Center(float scale) => clamp ? ClampCenter(midpoint, frame, texture, scale, circle) : midpoint;
        float Score(float scale) {
            var center = Center(scale);
            var x = Vector2.Abs(a - center) * scale / safeHalf;
            var y = Vector2.Abs(b - center) * scale / safeHalf;
            return circle ? MathF.Max(x.LengthSquared(), y.LengthSquared()) :
                MathF.Max(MathF.Max(x.X, x.Y), MathF.Max(y.X, y.Y));
        }
        if (Score(maximum) <= 1) return new BoundsFit(maximum, new Vector2(1024) - Center(maximum), true);
        var low = minimum; var high = maximum;
        if (Score(low) > 1) {
            for (var i = 0; i < 32; i++) {
                var left = low + (high - low) / 3;
                var right = high - (high - low) / 3;
                if (Score(left) < Score(right)) high = right; else low = left;
            }
            low = (low + high) / 2;
            if (Score(low) > 1) return new BoundsFit(minimum, new Vector2(1024) - Center(minimum), false);
        }
        high = maximum;
        for (var i = 0; i < 32; i++) {
            var mid = (low + high) / 2;
            if (Score(mid) <= 1) low = mid; else high = mid;
        }
        return new BoundsFit(low, new Vector2(1024) - Center(low), true);
    }

    public static Vector2 DrawPosition(Vector2 frame, Vector2 offset, float scale) =>
        frame / 2 - new Vector2(1024) * scale + offset * scale;

    public static float FrameExtent(Vector2 frame) => MathF.Max(1, MathF.Min(frame.X, frame.Y));

    public static Vector2 HalfFrame(Vector2 frame, bool circle) =>
        (circle ? new Vector2(FrameExtent(frame)) : frame) / 2;

    public static float MinimumScale(Vector2 frame, Vector2 texture, bool circle)
    {
        var extent = HalfFrame(frame, circle) * 2;
        return MathF.Max(extent.X / texture.X, extent.Y / texture.Y);
    }

    public static Vector2 ClampCenter(Vector2 center, Vector2 frame, Vector2 texture, float scale, bool circle)
    {
        var half = Vector2.Min(HalfFrame(frame, circle) / scale, texture / 2);
        return Vector2.Clamp(center, half, texture - half);
    }

    public static Vector2 ZoomAtCursor(Vector2 center, Vector2 cursorFromCenter, float oldScale, float newScale) =>
        center + cursorFromCenter / oldScale - cursorFromCenter / newScale;
}

public sealed class ViewportZoom
{
    private double zoomPerPixel;
    private double minimumZoomPerPixel;
    private double maximumZoomPerPixel;
    private float lastExtent;
    private float lastScale;
    private bool scaling;

    public float Resolve(Vector2 frame, float requestedScale, bool scaleWithFrame, float minimumScale = .05f)
    {
        var extent = ViewportMath.FrameExtent(frame);
        requestedScale = float.IsFinite(requestedScale) ? requestedScale : 1;
        if (lastExtent == 0 || scaling != scaleWithFrame) {
            var initial = requestedScale > 0 ? requestedScale : .05f;
            zoomPerPixel = initial / (double)extent;
            minimumZoomPerPixel = MathF.Min(.05f, initial) / (double)extent;
            maximumZoomPerPixel = MathF.Max(20, requestedScale) / (double)extent;
        }
        else if (requestedScale != lastScale) {
            zoomPerPixel = Math.Clamp(requestedScale / (double)lastExtent,
                minimumZoomPerPixel, maximumZoomPerPixel);
        }

        var scale = scaleWithFrame ? (float)(zoomPerPixel * extent) : requestedScale;
        lastScale = MathF.Max(minimumScale, scaleWithFrame ? scale : Math.Clamp(scale, .05f, 20));
        lastExtent = extent;
        scaling = scaleWithFrame;
        return lastScale;
    }
}
