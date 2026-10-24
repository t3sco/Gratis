using System.Numerics;

namespace Gratis.Rendering;

public sealed partial class MapRendererFrame
{
    public uint ViewedMapId => host.MapId;
    public Vector2 WorldToScreen(Vector2 world, Vector2 mapOrigin) =>
        mapOrigin + DrawPosition + (new Vector2(1024) - host.CoordinateOffset(world)) * Scale;
    public Vector2 ScreenToWorld(Vector2 screen, Vector2 mapOrigin)
    {
        var texture = (screen - mapOrigin - DrawPosition) / Scale;
        var zero = host.CoordinateOffset(Vector2.Zero);
        var step = host.CoordinateOffset(Vector2.One) - zero;
        return (new Vector2(1024) - texture - zero) / step;
    }
}
