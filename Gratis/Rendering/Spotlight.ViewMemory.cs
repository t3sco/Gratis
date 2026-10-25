using System.Numerics;

namespace Gratis.Rendering;

public sealed partial class Spotlight {
    public void RememberReturn(RememberedMapView view, MapDrawingConfig config, Vector2 frame, Vector2 texture) {
        savedScale = view.ResolveScale(config, frame, texture);
        savedExtent = ViewportMath.FrameExtent(frame);
        if (!savedFollow && view.Offset is { } offset)
            savedOffset = offset;
    }
}