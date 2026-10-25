using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Gratis.Rendering;

public sealed class ViewportMotion {
    public ViewportZoom Zoom { get; } = new();
    public FollowReturn Follow { get; } = new();
    public Spotlight Spotlight { get; } = new();
    public Vector2 SpotlightTargetTexture, SpotlightPlayerTexture;
    public float? PendingZoom { get; private set; }
    private Vector2 followVelocity, zoomCursor;
    public Vector2 FollowZoomOffset;
    private float zoomVelocity;
    public Vector2 PanVelocity { get; private set; }
    private double dragSampleTime = double.NegativeInfinity;
    private bool gliding;
    private double time = double.NaN;
    private long frame = -1;
    private ulong? map;
    private readonly Queue<double> calls = new();
    private readonly Dictionary<string, int> reasons = new(), writers = new();
    private string lastScaleWriter = "none", lastOffsetWriter = "none";
    public double LastElapsed { get; private set; }
    public float LastStep { get; private set; }
    public bool HasAdvanced(long frameId) => frame == frameId;

    private static float Minimum(MapDrawingConfig c, Vector2 size, Vector2 texture) => c.IsGratis
        ? ViewportMath.Limits(c, size, texture).Minimum : c.ClampToMap ? ViewportMath.MinimumScale(size, texture, false)
        : 0;
    private float Resolve(MapDrawingConfig c, Vector2 size, Vector2 texture, float scale, float minimum) =>
        ViewportMath.Constrain(c, size, texture, Zoom.Resolve(size, scale, c.ScaleWithFrame, minimum),
            Vector2.Zero).Scale;
    private static void Count(Dictionary<string, int> counts, string name) =>
        counts[name] = counts.GetValueOrDefault(name)
        + 1;
    private void Reason(string name) => Count(reasons, name);
    public void Written(string property, string writer) {
        Count(writers, property + ":" + writer);
        if (property == "scale")
            lastScaleWriter = writer;
        else
            lastOffsetWriter = writer;
    }

    public void Cancel() {
        CancelPan();
        followVelocity = Vector2.Zero;
        zoomVelocity = 0;
        PendingZoom = null;
    }

    public void CancelPan() {
        PanVelocity = Vector2.Zero;
        gliding = false;
    }

    public void SampleDrag(MapDrawingConfig config, Vector2 delta, double now, float scale) {
        var settings = config.Motion ??= new MotionSettings();
        if (!MapMotion.IsEnabled(config) || !settings.PanInertia || LastStep <= 0) {
            CancelPan();
            return;
        }
        var velocity = delta / LastStep;
        var ceiling = 4000 / MathF.Max(.05f, scale);
        if (velocity.LengthSquared() > ceiling * ceiling)
            velocity = Vector2.Normalize(velocity) * ceiling;
        PanVelocity = velocity;
        dragSampleTime = now;
        gliding = false;
    }

    public bool Interact(MapDrawingConfig config, double now, bool drag) {
        CancelPan();
        followVelocity = Vector2.Zero;
        if (drag) {
            Cancel();
            Reason("drag-cancel");
        }
        return Follow.Interact(config, now, drag);
    }

    public bool QueueZoom(MapDrawingConfig config, float requested, Vector2 cursor, float scale, Vector2 size,
        Vector2 texture, bool linear) {
        CancelPan();
        var motion = config.Motion ??= new MotionSettings();
        if (!MapMotion.IsEnabled(config) || !motion.EaseZoom)
            return false;
        if (PendingZoom is { } pending && scale > 0)
            requested = linear ? pending + requested - scale : pending * requested / scale;
        var minimum = MathF.Max(.05f, Minimum(config, size, texture));
        PendingZoom = Resolve(config, size, texture, requested, minimum);
        Zoom.Resolve(size, scale, config.ScaleWithFrame, minimum);
        zoomCursor = cursor;
        return true;
    }

    public void Advance(MapDrawingConfig config, ulong mapId, double now, long frameId, Vector2 size, Vector2 texture,
        Vector2 player, Vector2 target, bool interacting, bool hovered, bool focused, ref float scale,
        ref Vector2 offset) {
        if (HasAdvanced(frameId))
            return;
        frame = frameId;
        calls.Enqueue(now);
        while (calls.Count > 512 || calls.Count > 0 && calls.Peek() < now - 1)
            calls.Dequeue();
        var elapsed = double.IsNaN(time) ? 0 : now - time;
        LastElapsed = elapsed;
        time = now;
        LastStep = (float)Math.Clamp(elapsed, 0, .3);
        var changed = map != mapId;
        map = mapId;
        if (changed || elapsed < 0) {
            Cancel();
            Reason(changed ? "map" : "clock-rewind");
            FollowZoomOffset = Vector2.Zero;
        }
        var minimum = Minimum(config, size, texture);
        var previousScale = scale;
        scale = Resolve(config, size, texture, scale, minimum);
        if (scale != previousScale)
            Written("scale", "resize/bounds");
        if (Spotlight.Advance(config, mapId, now, LastStep, size, texture, SpotlightTargetTexture,
            SpotlightPlayerTexture, player, target, interacting, ref scale, ref offset)) {
            PendingZoom = null;
            CancelPan();
            var constrained = ViewportMath.Constrain(config, size, texture, scale, offset);
            scale = constrained.Scale;
            offset = constrained.Offset;
            Zoom.Resolve(size, scale, config.ScaleWithFrame, minimum);
            Written("scale", "spotlight");
            Written("offset", "spotlight");
            if (!Spotlight.OwnsViewport) {
                Follow.Reset();
                Cancel();
                FollowZoomOffset = Vector2.Zero;
            }
            return;
        }
        var motion = config.Motion ??= new MotionSettings();
        var enabled = MapMotion.IsEnabled(config);
        if (!enabled || !motion.PanInertia)
            CancelPan();
        if (!interacting && !gliding && now - dragSampleTime > .08)
            CancelPan();
        if (!interacting && PanVelocity != Vector2.Zero)
            gliding = true;
        var returnEnabled = config.IsGratis && config.FollowReturnToPlayer;
        if (!config.IsGratis) {
            Cancel();
            offset = ClampOffset(config, size, texture, scale, offset);
            return;
        }

        var before = offset;
        var previousState = Follow.State;
        if (returnEnabled) {
            FollowZoomOffset = Vector2.Zero;
            var constrainedTarget = ClampOffset(config, size, texture, scale, target);
            offset = Follow.Update(config, mapId, player, constrainedTarget, offset, now,
                interacting || PendingZoom.HasValue || PanVelocity != Vector2.Zero, hovered, focused);
            if (Follow.Snapped) {
                Cancel();
                Reason(changed ? "map-attach" : "attach/teleport");
            }
        }
        else {
            Follow.Update(config, mapId, player, target, offset, now, false, hovered, focused);
            if (config.FollowPlayer) {
                var followTarget = ClampOffset(config, size, texture, scale, target + FollowZoomOffset);
                if (!enabled || !motion.EaseFollow || changed || elapsed < 0) {
                    offset = followTarget;
                    followVelocity = Vector2.Zero;
                    Reason(!enabled ? "disabled" : !motion.EaseFollow ? "follow-off" : "reset");
                }
                else
                    offset = MotionMath.DampBounded(offset, followTarget, ref followVelocity, LastStep,
                        motion.SettleSeconds);
            }
            else
                followVelocity = Vector2.Zero;
        }
        if (returnEnabled && config.FollowPlayer && Follow.State == FollowState.Following
            && previousState == FollowState.Following && !Follow.Snapped && !changed && elapsed >= 0 && enabled
            && motion.EaseFollow) {
            offset = MotionMath.DampBounded(before, offset, ref followVelocity, LastStep, motion.SettleSeconds);
        }
        else if (returnEnabled) {
            followVelocity = Vector2.Zero;
            if (config.FollowPlayer && Follow.State == FollowState.Following && !Follow.Snapped
                && (!enabled || !motion.EaseFollow))
                Reason(!enabled ? "disabled" : "follow-off");
        }
        if (offset != before)
            Written("offset", Follow.State == FollowState.Returning ? "return" : "follow");

        if (config.FollowPlayer && (!returnEnabled || Follow.State != FollowState.Detached))
            CancelPan();
        if (!interacting && gliding && PanVelocity != Vector2.Zero) {
            var velocity = PanVelocity;
            offset += MotionMath.Glide(ref velocity, LastStep, motion.PanInertiaStrength);
            PanVelocity = velocity.LengthSquared() * scale * scale < 1 ? Vector2.Zero : velocity;
            Written("offset", "pan-inertia");
        }

        if (PendingZoom is { } zoom) {
            if (previousScale > 0 && scale != previousScale)
                zoom *= scale / previousScale;
            zoom = Math.Clamp(zoom, minimum, ViewportMath.Limits(config, size, texture).Maximum);
            PendingZoom = zoom;
            var next = !enabled || !motion.EaseZoom ? zoom
                : MotionMath.Damp(scale, zoom, ref zoomVelocity, LastStep, motion.SettleSeconds);
            if (!enabled || !motion.EaseZoom)
                Reason(!enabled ? "disabled" : "zoom-off");
            if (MathF.Abs(next - zoom) < .0001f) {
                next = zoom;
                PendingZoom = null;
                zoomVelocity = 0;
            }
            var resolved = Resolve(config, size, texture, next, minimum);
            var center = ViewportMath.ZoomAtCursor(new Vector2(1024) - offset, zoomCursor, scale, resolved);
            var nextOffset = new Vector2(1024) - center;
            if (!returnEnabled && config.FollowPlayer)
                FollowZoomOffset += nextOffset - offset;
            offset = nextOffset;
            scale = resolved;
            Written("scale", "zoom");
            Written("offset", "zoom-anchor");
        }
        var clamped = ClampOffset(config, size, texture, scale, offset);
        if (clamped.X != offset.X)
            followVelocity.X = 0;
        if (clamped.Y != offset.Y)
            followVelocity.Y = 0;
        PanVelocity = new Vector2(clamped.X != offset.X ? 0 : PanVelocity.X, clamped.Y != offset.Y ? 0 : PanVelocity.Y);
        if (clamped != offset)
            Written("offset", "clamp");
        offset = clamped;
    }

    private static Vector2 ClampOffset(MapDrawingConfig config, Vector2 size, Vector2 texture, float scale,
        Vector2 value) =>

        config.ClampToMap ? ViewportMath.ClampOffset(value, size, texture, scale, config.MapCircle && config.IsGratis)
            : value;

    public string Diagnostics(MapDrawingConfig config, double now) {
        while (calls.Count > 0 && calls.Peek() < now - 1)
            calls.Dequeue();
        var m = config.Motion ??= new MotionSettings();
        var message = $"Enabled={MapMotion.IsEnabled(config)} EaseZoom/Follow={m.EaseZoom}/{m.EaseFollow} "
            + $"SettleSeconds={m.SettleSeconds:0.###} "
            + $"calls/s={calls.Count} elapsed/step={LastElapsed:0.####}/{LastStep:0.####} "
            + $"snap/cancel=[{string.Join(",", reasons.Select(x => x.Key + "=" + x.Value))}] "
            + $"pendingZoom={PendingZoom?.ToString("0.####") ?? "none"} "
            + $"velocity=zoom:{zoomVelocity:0.####}/pan:{followVelocity}/glide:{PanVelocity} "
            + $"inertia={m.PanInertia}/{m.PanInertiaStrength:0.##} state={Follow.State} "
            + $"lastScale={lastScaleWriter} lastOffset={lastOffsetWriter} "
            + $"writes=[{string.Join(",", writers.Select(x => x.Key + "=" + x.Value))}]";
        reasons.Clear();
        writers.Clear();
        return message;
    }
}