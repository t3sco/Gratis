using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Gratis.Rendering;

namespace Gratis.MapRenderer;

public unsafe partial class MapRenderer
{
    public void FollowGameObject(IGameObject obj) => frame.FollowPlayer(new Vector2(obj.Position.X, obj.Position.Z));
    private void PrepareViewport(Vector2 textureSize) => frame.PrepareViewport(textureSize);
    public void ZoomViewport(float requestedScale, Vector2 cursorFromCenter, bool animate = true) => frame.ZoomViewport(requestedScale, cursorFromCenter, animate);
    public Vector2 ViewportSize => frame.ViewportSize;
    public void ZoomToCluster(Vector2 localPosition, float scale) => frame.ZoomToCluster(localPosition, scale);
}
