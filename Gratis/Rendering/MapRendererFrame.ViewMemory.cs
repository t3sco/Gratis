using System;
using System.Numerics;

namespace Gratis.Rendering;

public sealed partial class MapRendererFrame
{
    public MapViewMemory ViewMemory { get; } = new();
    private uint? rememberedMap;
    private RememberedMapView? restoringView;
    private Vector2 restorePanVelocity;
    private float restoreZoomVelocity;
    private double lastViewSave;
    private bool viewDirty;
    public void RestoreMapViewOnOpen() => rememberedMap = null;
    public string ViewMemoryDiagnostics() => $"map={host.MapId} rememberView/lens={host.Config.RememberMapView}/{host.Config.RememberMapLens} " +
        $"entries={ViewMemory.Normalize(host.Config.MapViews).Count}/{MapViewMemory.Capacity} restoring={restoringView is not null} follow={host.Config.FollowPlayer}";

    private void PrepareRememberedView()
    {
        if (!host.Config.IsGratis || rememberedMap == host.MapId) return;
        if (viewDirty) { MapDrawContext.Current.SaveConfig(); viewDirty = false; }
        rememberedMap = host.MapId;
        restorePanVelocity = Vector2.Zero; restoreZoomVelocity = 0;
        var saved = ViewMemory.Find(host.Config, host.MapId, true);
        restoringView = host.Config.RememberMapView ? saved : null;
        if (host.Config.RememberMapLens && saved is not null)
            host.Config.ActiveLens = Lenses.Find(host.Config, saved.Lens).Name;
    }

    private void AdvanceRememberedView()
    {
        var config = host.Config;
        if (!config.IsGratis || !config.RememberMapView) restoringView = null;
        if (restoringView is { } saved && !Spotlight.OwnsViewport) {
            var scale = saved.ResolveScale(config, viewportSize, viewportTextureSize);
            var motion = config.Motion ??= new MotionSettings();
            var ease = MapMotion.IsEnabled(config);
            var next = ease && motion.EaseZoom ? MotionMath.Damp(Scale, scale, ref restoreZoomVelocity,
                viewportMotion.LastStep, motion.SettleSeconds) : scale;
            host.Config.MapScale = next;
            viewportMotion.Zoom.Resolve(viewportSize, next, config.ScaleWithFrame);
            var target = !config.FollowPlayer && !config.LockCenterOnMap ? saved.Offset : null;
            if (target is { } pan) DrawOffset = ease && motion.EaseFollow
                ? MotionMath.DampBounded(DrawOffset, pan, ref restorePanVelocity, viewportMotion.LastStep, motion.SettleSeconds) : pan;
            if (config.ClampToMap) DrawOffset = new Vector2(1024) - ViewportMath.ClampCenter(new Vector2(1024) - DrawOffset,
                viewportSize, viewportTextureSize, next, config.MapCircle);
            var clampedTarget = target is { } t && config.ClampToMap ? new Vector2(1024) - ViewportMath.ClampCenter(
                new Vector2(1024) - t, viewportSize, viewportTextureSize, next, config.MapCircle) : target;
            if (MathF.Abs(next - scale) < .0001f && (clampedTarget is null || Vector2.DistanceSquared(DrawOffset, clampedTarget.Value) < .0025f))
                restoringView = null;
        }
        if (restoringView is null && !Spotlight.OwnsViewport)
            viewDirty |= ViewMemory.Capture(config, host.MapId, Scale, DrawOffset, viewportSize);
        if (viewDirty && host.Now - lastViewSave >= 2) {
            MapDrawContext.Current.SaveConfig(); lastViewSave = host.Now; viewDirty = false;
        }
    }

    private void RememberSpotlightReturn()
    {
        PrepareRememberedView();
        if (!Spotlight.OwnsViewport && restoringView is null)
            ViewMemory.Capture(host.Config, host.MapId, Scale, DrawOffset, viewportSize.X > 0 ? viewportSize : host.Config.WindowSize);
    }
}
