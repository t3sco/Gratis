using System;
using System.Numerics;

namespace Gratis.Rendering;

public sealed partial class MapRendererFrame
{
    private uint? smallRoomMap;
    private bool smallRoomTouched, hadRememberedRoomView;
    private MapContentBounds? roomBounds, fittedRoomBounds;
    private void TouchSmallRoomView() { if (smallRoomMap == host.MapId) smallRoomTouched = true; }
    public ViewportMath.ZoomLimits ZoomLimits => ViewportMath.Limits(host.Config, viewportSize, viewportTextureSize, roomBounds);
    public Vector2 TextureSize => viewportTextureSize;
    public string ViewportDiagnostics() => $"map={host.MapId} zoom={Scale:0.####} min/max/fit={ZoomLimits.Minimum:0.####}/{ZoomLimits.Maximum:0.####}/{ZoomLimits.Fit:0.####} " +
        $"window={viewportSize} texture={viewportTextureSize} small={ZoomLimits.Small} content={roomBounds} fitContent={host.Config.FitSmallRoomsToContent} markerScale={ViewportMath.SymbolScale(Scale):0.##} clamp=0.6..1.5";

    private void PrepareSmallRoomView()
    {
        var config = host.Config;
        if (smallRoomMap != host.MapId) {
            smallRoomMap = host.MapId; smallRoomTouched = false; roomBounds = fittedRoomBounds = null;
            hadRememberedRoomView = config.RememberMapView && ViewMemory.Find(config, host.MapId) is not null;
        }
        config.ViewportSmallMap = false;
        if (!config.IsGratis) return;
        roomBounds = host.ContentBounds;
        if (roomBounds is null && host.SmallMapHint) {
            var center = new Vector2(1024) - host.CoordinateOffset(followPlayerPosition);
            var min = center; var max = center;
            var targets = MapDrawContext.Current.MarkerTargets;
            if (targets.MapId == host.MapId) foreach (var (_, world) in targets.Targets) {
                var point = new Vector2(1024) - host.CoordinateOffset(world);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            roomBounds = new MapContentBounds(Vector2.Max(Vector2.Zero, min - new Vector2(128)), Vector2.Min(viewportTextureSize, max + new Vector2(128)));
        }
        config.ViewportSmallMap = roomBounds?.IsSmall(viewportTextureSize) == true;
        var limits = ZoomLimits;
        if (!limits.Small || smallRoomTouched || hadRememberedRoomView) return;
        var bounds = config.FitSmallRoomsToContent && roomBounds.HasValue ? roomBounds.Value : new MapContentBounds(Vector2.Zero, viewportTextureSize);
        if (fittedRoomBounds == bounds) return;
        fittedRoomBounds = bounds;
        CancelViewportMotion();
        host.Config.MapScale = limits.Fit;
        DrawOffset = Constrain(limits.Fit, new Vector2(1024) - bounds.Center).Offset;
        viewportZoom.Resolve(viewportSize, limits.Fit, config.ScaleWithFrame, limits.Minimum);
    }

    private Vector2 SmallRoomFollowTarget(Vector2 target)
    {
        if (!host.Config.IsGratis || !host.Config.FitSmallRoomsToContent || !ZoomLimits.Small || roomBounds is not { } bounds || Scale > ZoomLimits.Fit * 1.001f) return target;
        var half = ViewportMath.HalfFrame(viewportSize, host.Config.MapCircle) / Scale - new Vector2(40, 80) / Scale;
        if (half.X < bounds.Size.X / 2 || half.Y < bounds.Size.Y / 2) return target;
        var center = Vector2.Clamp(new Vector2(1024) - target, bounds.Max - half, bounds.Min + half);
        return new Vector2(1024) - center;
    }
}
