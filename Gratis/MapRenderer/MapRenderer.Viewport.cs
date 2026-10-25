using System.Numerics;

namespace Gratis.MapRenderer;

public unsafe partial class MapRenderer {
    public void ZoomViewport(float requestedScale, Vector2 cursorFromCenter, bool animate = true) =>
        frame.ZoomViewport(requestedScale, cursorFromCenter, animate);
    public Vector2 ViewportSize => frame.ViewportSize;
    public void ZoomToCluster(Vector2 localPosition, float scale) => frame.ZoomToCluster(localPosition, scale);
}