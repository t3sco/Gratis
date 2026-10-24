using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Rendering;

namespace Gratis.MapRenderer;

public unsafe partial class MapRenderer
{
    private Vector2 followVelocity;
    private float zoomVelocity;
    private float? pendingZoom;
    private Vector2 zoomCursor;
    private double motionTime = double.NegativeInfinity;
    private uint? motionMap;
    private Vector2? pendingPing;
    private uint pendingPingMap;

    private void CancelViewportMotion()
    {
        followVelocity = Vector2.Zero;
        zoomVelocity = 0;
        pendingZoom = null;
    }

    private bool QueueViewportZoom(float requested, Vector2 cursor)
    {
        var config = System.SystemConfig;
        if (!MapMotion.Enabled || !config.Motion.EaseZoom) return false;
        if (pendingZoom is { } pending && Scale > 0)
            requested = config.UseLinearZoom ? pending + requested - Scale : pending * requested / Scale;
        var minimum = config.ClampToMap ? ViewportMath.MinimumScale(viewportSize, viewportTextureSize, config.MapCircle) : .05f;
        pendingZoom = viewportZoom.Resolve(viewportSize, requested, config.ScaleWithFrame, minimum);
        viewportZoom.Resolve(viewportSize, Scale, config.ScaleWithFrame, minimum);
        zoomCursor = cursor;
        return true;
    }

    private void AdvanceViewportMotion(float previousScale, float minimum)
    {
        var config = System.SystemConfig;
        var now = ImGui.GetTime();
        var elapsed = now - motionTime;
        motionTime = now;
        if (!config.IsGratis) { CancelViewportMotion(); return; }
        if (motionMap != AgentMap.Instance()->SelectedMapId) {
            CancelViewportMotion();
            motionMap = AgentMap.Instance()->SelectedMapId;
            elapsed = double.PositiveInfinity;
        }
        if (ImGui.IsMouseDragging(ImGuiMouseButton.Left)) CancelViewportMotion();
        var step = (float)Math.Clamp(elapsed, 0, .1);
        var snap = !MapMotion.Enabled || elapsed > .25 || elapsed <= 0;
        if (config.FollowPlayer) {
            var target = followTargetOffset + followZoomOffset;
            if (snap || !config.Motion.EaseFollow) { DrawOffset = target; followVelocity = Vector2.Zero; }
            else DrawOffset = MotionMath.Damp(DrawOffset, target, ref followVelocity, step, config.Motion.SettleSeconds);
        }
        else followVelocity = Vector2.Zero;
        if (pendingZoom is not { } zoom) return;
        if (previousScale > 0 && Scale != previousScale) zoom *= Scale / previousScale;
        zoom = MathF.Max(minimum, zoom);
        pendingZoom = zoom;
        var next = snap || !config.Motion.EaseZoom ? zoom : MotionMath.Damp(Scale, zoom, ref zoomVelocity, step, config.Motion.SettleSeconds);
        if (MathF.Abs(next - zoom) < .0001f) { next = zoom; pendingZoom = null; zoomVelocity = 0; }
        var resolved = viewportZoom.Resolve(viewportSize, next, config.ScaleWithFrame, minimum);
        var center = ViewportMath.ZoomAtCursor(new Vector2(1024) - DrawOffset, zoomCursor, Scale, resolved);
        if (config.ClampToMap) center = ViewportMath.ClampCenter(center, viewportSize, viewportTextureSize, resolved, config.MapCircle);
        var offset = new Vector2(1024) - center;
        if (config.FollowPlayer) followZoomOffset += offset - DrawOffset;
        DrawOffset = offset;
        Scale = resolved;
    }

    public void PingTexturePosition(Vector2 position)
    {
        pendingPing = position;
        pendingPingMap = AgentMap.Instance()->SelectedMapId;
    }

    private void FlushPendingPing()
    {
        if (pendingPing is not { } position) return;
        if (pendingPingMap == AgentMap.Instance()->SelectedMapId)
            MapDrawContext.Current.Motion.PingAt(position, ImGui.GetTime());
        pendingPing = null;
    }

    public void LocatePlayer()
    {
        if (Service.ObjectTable.LocalPlayer is not { } player) return;
        PingTexturePosition(new Vector2(1024) - CoordinateOffset(new Vector2(player.Position.X, player.Position.Z)));
    }
}
