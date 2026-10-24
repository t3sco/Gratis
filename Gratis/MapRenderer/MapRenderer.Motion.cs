using System.Numerics;
using Gratis.Rendering;

namespace Gratis.MapRenderer;

public unsafe partial class MapRenderer
{
    public void UpdateFollowInteraction(bool interacting, bool hovered, bool focused) => frame.UpdateFollowInteraction(interacting, hovered, focused);
    public void InteractWithViewport(bool drag = false) => frame.InteractWithViewport(drag);
    public void PanViewport(Vector2 delta) => frame.PanViewport(delta);
    public string TakeMotionDiagnostics() => frame.TakeMotionDiagnostics();
    public void PingTexturePosition(Vector2 position) => frame.PingTexturePosition(position);
    public void LocatePlayer()
    {
        if (Service.ObjectTable.LocalPlayer is not { } player) return;
        PingTexturePosition(new Vector2(1024) - CoordinateOffset(new Vector2(player.Position.X, player.Position.Z)));
    }
}
