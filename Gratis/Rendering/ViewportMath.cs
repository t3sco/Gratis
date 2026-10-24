using System;
using System.Numerics;

namespace Gratis.Rendering;

public static class ViewportMath
{
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
