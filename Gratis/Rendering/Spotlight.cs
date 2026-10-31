using System;
using System.Numerics;

namespace Gratis.Rendering;

public enum SpotlightSource {
    Quest, Flag, Search, Pin
}

public sealed record SpotlightOptions(float? Dim = null, bool? Pulse = null, float? Seconds = null,
    bool? DistanceChip = null, bool? GuideLine = null, float MaxZoomMultiplier = 3, float MaxScale = 3);

public sealed record SpotlightRequest(Vector2 Target, uint MapId, SpotlightSource Source, uint? MarkerId = null,
    uint? Icon = null, bool? IncludePlayer = null, SpotlightOptions? Options = null);

public sealed partial class Spotlight {
    public SpotlightRequest? Request { get; private set; }
    public bool Active => Request is not null && double.IsNaN(ended);
    public bool OwnsViewport => Request is not null && !released;
    public bool Visible => Request is not null;
    public float Opacity { get; private set; }
    public float FitZoom { get; private set; }
    public string Decision { get; private set; } = "none";
    public SpotlightRequest? LastRequest { get; private set; }
    public Vector2 TargetTexture { get; private set; }
    public Vector2 PlayerTexture { get; private set; }
    public bool IncludePlayer { get; private set; }
    public float Age { get; private set; }
    public float Remaining { get; private set; }
    public float Dim { get; private set; }
    public bool Pulse { get; private set; }
    public bool DistanceChip { get; private set; }
    public bool GuideLine { get; private set; }
    public bool HideOthers { get; private set; }
    public float Distance { get; private set; }
    private float savedScale, savedExtent, zoomVelocity, seconds, endOpacity;
    private Vector2 savedOffset, panVelocity;
    private bool savedFollow, started, released, savedKnown;
    private SpotlightOptions options = new();
    private ulong map;
    private double began, ended = double.NaN;

    public void Begin(SpotlightRequest request, MapDrawingConfig config, ulong mapKey, float scale, Vector2 offset,
        Vector2 frame, bool viewportReady = true) {
        if (!OwnsViewport || map != mapKey) {
            savedScale = scale;
            savedOffset = offset;
            savedFollow = config.FollowPlayer;
            savedExtent = ViewportMath.FrameExtent(frame);
        }
        if (!OwnsViewport || map != mapKey)
            savedKnown = viewportReady;
        Request = LastRequest = request;
        map = mapKey;
        started = false;
        released = false;
        ended = double.NaN;
        panVelocity = Vector2.Zero;
        zoomVelocity = 0;
        Opacity = Age = 0;
        options = request.Options ?? new SpotlightOptions();
        Dim = Safe(options.Dim ?? config.SpotlightDim, 0, .85f, .35f);
        seconds = Safe(options.Seconds ?? config.SpotlightSeconds, .25f, 60, 8);
        Pulse = options.Pulse ?? config.SpotlightPulse;
        DistanceChip = options.DistanceChip ?? config.SpotlightDistanceChip;
        GuideLine = options.GuideLine ?? config.SpotlightGuideLine;
        HideOthers = config.SpotlightHideOthers;
        IncludePlayer = request.IncludePlayer ?? config.SpotlightIncludePlayer;
        Remaining = seconds;
        Decision = IncludePlayer ? "frame player + target" : "frame target (other map/player excluded)";
    }

    private static float Safe(float value, float min, float max, float fallback) => float.IsFinite(value)
        ? Math.Clamp(value, min, max) : fallback;

    public void Reject(SpotlightRequest request, string reason) {
        LastRequest = request;
        Decision = reason;
    }

    public void End(double now, string reason) {
        if (!Active)
            return;
        endOpacity = Opacity;
        ended = now;
        Decision = reason;
        Remaining = 0;
        panVelocity = Vector2.Zero;
        zoomVelocity = 0;
    }

    public void Release(double now, string reason) {
        if (Request is null || released)
            return;
        End(now, reason);
        released = true;
    }

    public void Cancel(string reason) {
        Request = null;
        released = false;
        Opacity = Remaining = 0;
        Decision = reason;
        panVelocity = Vector2.Zero;
        zoomVelocity = 0;
    }

    public bool Advance(MapDrawingConfig config, ulong mapKey, double now, float dt, Vector2 frame, Vector2 texture,
        Vector2 targetTexture, Vector2 playerTexture, Vector2 playerWorld, Vector2 followOffset, bool interacting,
        ref float scale, ref Vector2 offset) {
        if (Request is not { } request)
            return false;
        if (map != mapKey || !config.IsGratis) {
            Cancel("cancelled: zone/map/style changed");
            return false;
        }
        if (!started) {
            began = now;
            started = true;
        }
        if (now < began) {
            Cancel("cancelled: clock rewind");
            return false;
        }
        TargetTexture = targetTexture;
        PlayerTexture = playerTexture;
        Distance = Vector2.Distance(request.Target, playerWorld);
        Age = (float)(now - began);
        if (Active && Age >= seconds)
            End(began + seconds, "expired: restoring viewport");
        if (Active && interacting)
            Release(now, "input: keeping view");
        if (!Active && !savedKnown && !released)
            released = true;
        if (released) {
            Opacity = endOpacity * (1 - MotionMath.Transition(now, ended, .2f));
            if (Opacity <= 0) {
                Request = null;
                released = false;
            }
            return false;
        }
        Remaining = Active ? MathF.Max(0, seconds - Age) : 0;
        Opacity = Active ? MotionMath.Transition(now, began, .2f)
            : endOpacity * (1 - MotionMath.Transition(now, ended, .2f));
        var relativeScale = config.ScaleWithFrame ? ViewportMath.FrameExtent(frame) / savedExtent : 1;
        var maximum = MathF.Min(Safe(options.MaxScale, .05f, 20, 3) * relativeScale,
            savedScale * relativeScale * Safe(options.MaxZoomMultiplier, 1, 20, 3));
        var fit = ViewportMath.FitBounds(targetTexture, IncludePlayer ? playerTexture : targetTexture, frame, texture,
            config.MapCircle, config.ClampToMap, maximum, new Vector4(48, 80, 48, 80) * UiScale,
            config.IsGratis ? ViewportMath.Limits(config, frame, texture).Minimum : null);
        FitZoom = fit.Scale;
        var restoring = !Active;
        var targetScale = restoring ? savedScale * relativeScale : fit.Scale;
        var targetOffset = restoring ? (savedFollow ? followOffset : savedOffset) : fit.Offset;
        if (config.ClampToMap) {
            targetScale = MathF.Max(targetScale, ViewportMath.Limits(config, frame, texture).Minimum);
            targetOffset = new Vector2(1024)
                - ViewportMath.ClampCenter(new Vector2(1024) - targetOffset, frame, texture, targetScale,
                    config.MapCircle);
        }
        if (config.IsGratis) {
            var safe = ViewportMath.Constrain(config, frame, texture, targetScale, targetOffset);
            targetScale = safe.Scale;
            targetOffset = safe.Offset;
        }
        var motion = config.Motion ??= new MotionSettings();
        var enabled = MapMotion.IsEnabled(config);
        if (!restoring || !interacting) {
            scale = !enabled || !motion.EaseZoom ? targetScale
                : MotionMath.Damp(scale, targetScale, ref zoomVelocity, dt, motion.SettleSeconds);
            offset = !enabled || !motion.EaseFollow ? targetOffset
                : MotionMath.DampBounded(offset, targetOffset, ref panVelocity, dt, motion.SettleSeconds);
            if (restoring && Opacity == 0 && MathF.Abs(scale - targetScale) < .0001f
                && Vector2.DistanceSquared(offset, targetOffset) < .0025f) {
                scale = targetScale;
                offset = targetOffset;
                Request = null;
            }
        }
        return true;
    }

    public float UiScale { get; set; } = 1;
    public string Diagnostics() => $"source={LastRequest?.Source.ToString() ?? "none"} "
        + $"target={LastRequest?.Target.ToString() ?? "none"} "
        + $"decision={Decision} fitZoom={FitZoom:0.###} remaining={Remaining:0.0}s active={Active}";
}