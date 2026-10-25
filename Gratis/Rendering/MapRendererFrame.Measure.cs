using System.Numerics;

namespace Gratis.Rendering;

public sealed partial class MapRendererFrame {
    public Vector2 WorldToMapCoordinates(Vector2 world) => Vector2.One
        + (new Vector2(1024) - host.CoordinateOffset(world)) * (41f / (2048 * MapDrawContext.Current.MapSizeFactor));
}