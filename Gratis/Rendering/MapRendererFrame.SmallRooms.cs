using System.Numerics;

namespace Gratis.Rendering;

public sealed partial class MapRendererFrame {
    public ViewportAutoFit AutoFit { get; } = new();
    private bool viewportReady, sawLocks, previousZoomLock, previousCenterLock;
    private MapContentBounds? roomBounds;
    private RememberedMapView? restoredMetadata;
    public bool ViewportReady => viewportReady;
    private static bool ValidSize(Vector2 size) => float.IsFinite(size.X) && float.IsFinite(size.Y) && size.X > 1
        && size.Y > 1;
    public ViewportMath.ZoomLimits ZoomLimits =>
        ViewportMath.Limits(host.Config, viewportSize, viewportTextureSize, roomBounds,
            MapDrawContext.Current.GlobalScale);
    public Vector2 TextureSize => viewportTextureSize;
    public string ViewportDiagnostics() => $"map={host.MapId} zoom={Scale:0.####} "
        + $"min/max/fit={ZoomLimits.Minimum:0.####}/{ZoomLimits.Maximum:0.####}/{ZoomLimits.Fit:0.####} "
        + $"window={viewportSize} texture={viewportTextureSize} small={ZoomLimits.Small} "
        + $"content={roomBounds} fitContent={host.Config.FitSmallRoomsToContent} "
        + $"markerScale={ViewportMath.SymbolScale(Scale):0.##} clamp=0.6..1.5 {AutoFit.Diagnostics()}";

    private void ObserveViewIdentity() {
        if (!host.Config.IsGratis || !AutoFit.Observe(host.MapKey, host.ViewSession, host.Now))
            return;
        viewportTextureSize = Vector2.Zero;
        viewportReady = false;
        roomBounds = null;
        rememberedMap = null;
        restoringView = restoredMetadata = null;
        sawLocks = false;
        host.Config.ViewportSmallMap = false;
        CancelViewportMotion();
        viewportZoom.Reset();
    }

    public void UseManualView(string reason = "explicit viewport", bool restoreRemembered = false) {
        if (!host.Config.IsGratis)
            return;
        ObserveViewIdentity();
        AutoFit.Manual(host.Now, reason);
        restoredMetadata = restoringView = null;
        if (!restoreRemembered)
            rememberedMap = host.MapId;
    }

    private void PrepareSmallRoomView() {
        var config = host.Config;
        config.ViewportSmallMap = false;
        if (!config.IsGratis)
            return;
        if (sawLocks && ((!previousZoomLock && config.ZoomLocked) || (!previousCenterLock && config.LockCenterOnMap)))
            UseManualView("viewport locked");
        sawLocks = true;
        previousZoomLock = config.ZoomLocked;
        previousCenterLock = config.LockCenterOnMap;
        roomBounds = host.ContentBounds;
        if (roomBounds is null && host.SmallMapHint) {
            var center = new Vector2(1024) - host.CoordinateOffset(followPlayerPosition);
            var min = center;
            var max = center;
            var targets = MapDrawContext.Current.MarkerTargets;
            if (targets.MapId == host.MapId)
                foreach (var (_, world) in targets.Targets) {
                    var point = new Vector2(1024) - host.CoordinateOffset(world);
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
            roomBounds = new MapContentBounds(Vector2.Max(Vector2.Zero, min - new Vector2(128)),
                Vector2.Min(viewportTextureSize, max + new Vector2(128)));
        }
        config.ViewportSmallMap = roomBounds?.IsSmall(viewportTextureSize) == true;
        if (restoredMetadata is { } restored
            && !restored.CanRestore(host.MapKey, host.ViewSession, host.InstanceMap, viewportTextureSize)) {
            restoredMetadata = restoringView = null;
            AutoFit.Resume(host.Now, "remembered texture/map changed");
        }
    }

    private void ApplyAutoFit() {
        if (!host.Config.IsGratis)
            return;
        AutoFit.Evaluate(host.Config, viewportSize, viewportTextureSize, MapDrawContext.Current.GlobalScale, roomBounds,
            host.Now);
        if (AutoFit.Mode != ViewportViewMode.AutoFit || Spotlight.OwnsViewport)
            return;
        var bounds = ZoomLimits.Small && host.Config.FitSmallRoomsToContent && roomBounds.HasValue ? roomBounds.Value
            : new MapContentBounds(Vector2.Zero, viewportTextureSize);
        var fit = ZoomLimits.Fit;
        var target = Constrain(fit, new Vector2(1024) - bounds.Center);
        host.Config.MapScale = target.Scale;
        DrawOffset = target.Offset;
        viewportZoom.Reset();
        viewportZoom.Resolve(viewportSize, target.Scale, host.Config.ScaleWithFrame, ZoomLimits.Minimum);
    }

    private Vector2 SmallRoomFollowTarget(Vector2 target) {
        if (!host.Config.IsGratis || !host.Config.FitSmallRoomsToContent || !ZoomLimits.Small
            || roomBounds is not { } bounds || Scale > ZoomLimits.Fit * 1.001f)
            return target;
        var half = (ViewportMath.HalfFrame(viewportSize, host.Config.MapCircle)
            - ViewportMath.FitPadding(viewportSize, MapDrawContext.Current.GlobalScale))
            / Scale;
        if (half.X < bounds.Size.X / 2 || half.Y < bounds.Size.Y / 2)
            return target;
        var center = Vector2.Clamp(new Vector2(1024) - target, bounds.Max - half, bounds.Min + half);
        return new Vector2(1024) - center;
    }
}