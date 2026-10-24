using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Gratis.Rendering;

namespace Gratis.MapRenderer;

public unsafe partial class MapRenderer
{
    private readonly ViewportZoom viewportZoom = new();
    private Vector2 viewportSize;
    private Vector2 viewportTextureSize = new(2048);
    private Vector2 followTargetOffset;
    private Vector2 followZoomOffset;
    private bool wasFollowing;

    public void FollowGameObject(IGameObject obj)
    {
        if (!wasFollowing || !System.SystemConfig.ClampToMap) followZoomOffset = Vector2.Zero;
        followTargetOffset = CoordinateOffset(new Vector2(obj.Position.X, obj.Position.Z));
        DrawOffset = followTargetOffset + followZoomOffset;
    }

    private void PrepareViewport(Vector2 textureSize)
    {
        if (textureSize.X > 0 && textureSize.Y > 0) viewportTextureSize = textureSize;
        var config = System.SystemConfig;
        wasFollowing = config.FollowPlayer;
        if (!wasFollowing || !config.ClampToMap) followZoomOffset = Vector2.Zero;
        var circle = config.MapCircle && config.IsGratis;
        var minimum = config.ClampToMap
            ? ViewportMath.MinimumScale(viewportSize, viewportTextureSize, circle) : 0;
        Scale = viewportZoom.Resolve(viewportSize, Scale, config.ScaleWithFrame, minimum);
        if (config.ClampToMap) {
            var center = ViewportMath.ClampCenter(new Vector2(1024) - DrawOffset,
                viewportSize, viewportTextureSize, Scale, circle);
            DrawOffset = new Vector2(1024) - center;
        }
        UpdateDrawOffset();
    }

    public void ZoomViewport(float requestedScale, Vector2 cursorFromCenter)
    {
        var config = System.SystemConfig;
        if (!config.ClampToMap) {
            Scale = requestedScale;
            return;
        }
        var circle = config.MapCircle && config.IsGratis;
        var minimum = ViewportMath.MinimumScale(viewportSize, viewportTextureSize, circle);
        if (requestedScale <= minimum && Scale <= minimum) return;
        var scale = viewportZoom.Resolve(viewportSize, requestedScale, config.ScaleWithFrame, minimum);
        var center = ViewportMath.ZoomAtCursor(new Vector2(1024) - DrawOffset, cursorFromCenter, Scale, scale);
        DrawOffset = new Vector2(1024) - ViewportMath.ClampCenter(center,
            viewportSize, viewportTextureSize, scale, circle);
        if (config.FollowPlayer) followZoomOffset = DrawOffset - followTargetOffset;
        Scale = scale;
    }

    public Vector2 ViewportSize => viewportSize;
}
