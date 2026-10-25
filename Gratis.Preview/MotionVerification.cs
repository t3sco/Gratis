using System.Numerics;
using System.Text.Json;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class MotionVerification {
    public static void Run() {
        var checks = 0;
        void Check(bool condition, string name) {
            if (!condition)
                throw new InvalidOperationException("Motion assertion: " + name);
            checks++;
        }
        Check(MotionMath.Ease(-1) == 0 && MotionMath.Ease(0) == 0 && MotionMath.Ease(1) == 1 && MotionMath.Ease(2) == 1,
            "ease endpoints");
        Check(MotionMath.Transition(2, 1, .18f) == 1 && MotionMath.Transition(1, 1, .18f) == 0, "zone endpoints");
        Check(MathF.Abs(MotionMath.Ease(.5f) - .5f) < 1e-6f, "ease midpoint");
        var velocity = 0f;
        Check(MotionMath.Damp(0, 1, ref velocity, 0, .12f) == 0, "spring zero dt");
        Check(MotionMath.Damp(0, 1, ref velocity, .12f, .12f) > .989f, "spring ~99% settled at 120ms");
        velocity = 0;
        var largeStep = MotionMath.Damp(0, 1, ref velocity, .12f, .12f);
        velocity = 0;
        var value = 0f;
        for (var i = 0; i < 12; i++)
            value = MotionMath.Damp(value, 1, ref velocity, .01f, .12f);
        Check(MathF.Abs(value - largeStep) < 1e-6f, "spring timestep invariance");
        value = 0;
        velocity = 0;
        for (var i = 0; i < 120; i++) {
            var next = MotionMath.Damp(value, 1, ref velocity, 1f / 120, .12f);
            Check(next >= value && next <= 1, "spring monotonic without overshoot from rest");
            value = next;
        }
        Check(Math.Abs(value - 1) < 1e-5f, "spring endpoint");
        Check(MotionMath.Ring(-.1f, 0).Alpha == 0 && MotionMath.Ring(.9f, 0).Alpha == 0
            && MotionMath.Ring(.9f, 1).Alpha == 0,
            "ring endpoints");
        Check(MotionMath.Ring(.1f, 1).Alpha == 0 && MotionMath.Ring(.3f, 1).Alpha > 0, "second ring delay");
        var ring0 = MotionMath.Ring(.1f, 0);
        var ring1 = MotionMath.Ring(.3f, 0);
        Check(ring0.Alpha > ring1.Alpha && ring1.Radius > ring0.Radius, "rings expand and fade");
        var trail = new PlayerTrail();
        for (var i = 0; i <= 80; i++)
            trail.Add(new Vector2(i * 2, 0), i * .05, 4);
        Check(trail.Count == 80, "trail prunes four-second endpoint");
        trail.Prune(5, 4);
        Check(trail.Count == 60 && trail[0].Time > 1, "trail pruning");
        Check(MotionMath.TrailAlpha(2, 1, 4) == .75f && MotionMath.TrailAlpha(2, 3, 4) == 0,
            "trail alpha / clock rewind");
        trail.Prune(5, 0);
        Check(trail.Count == 0, "zero duration is off");
        for (var i = 0; i < 300; i++)
            trail.Add(new Vector2(i, 0), i * .05, 30);
        Check(trail.Count == 256, "trail storage bounded");
        trail.Prune(0, 4);
        Check(trail.Count == 0, "clock rewind clears trail");
        var serialization = new JsonSerializerOptions { IncludeFields = true };
        var config = JsonSerializer.Deserialize<MapDrawingConfig>("{}", serialization)!;
        Check(!config.ReduceMotion
            && config.Motion is {
                Pings: true, PlayerTrail: true, TrailSeconds: 8.3f, EaseFollow: true, EaseZoom: true,
                ZoneTransition: true
            },
            "old configuration defaults");
        config.ReduceMotion = true;
        Check(!MapMotion.IsEnabled(config), "ReduceMotion master gate");
        config.ReduceMotion = false;
        config.VisualStyle = 1;
        Check(!MapMotion.IsEnabled(config), "Classic motion gate");
        config.VisualStyle = 0;
        Check(MapMotion.IsEnabled(config), "Gratis motion gate");
        config.Motion.TrailSeconds = 0;
        config.ReduceMotion = true;
        config = JsonSerializer.Deserialize<MapDrawingConfig>(JsonSerializer.Serialize(config, serialization),
            serialization)!;
        Check(config.ReduceMotion && config.Motion.TrailSeconds == 0, "settings round trip");
        config.ResetMotion();
        Check(!config.ReduceMotion && config.Motion.TrailSeconds == 8.3f && config.Motion.SettleSeconds == .12f,
            "motion reset");

        var frame = new Vector2(960, 640);
        var texture = new Vector2(2048);
        var zoomVelocity = 0f;
        var panVelocity = Vector2.Zero;
        var zoom = 1f;
        var center = new Vector2(1024);
        for (var i = 0; i < 60; i++) {
            zoom = MathF.Max(ViewportMath.MinimumScale(frame, texture, false),
                MotionMath.Damp(zoom, .1f, ref zoomVelocity, 1f / 60, .12f));
            center = MotionMath.Damp(center, new Vector2(-1000), ref panVelocity, 1f / 60, .12f);
            center = ViewportMath.ClampCenter(center, frame, texture, zoom, false);
            var half = frame / (2 * zoom);
            Check(center.X >= half.X && center.Y >= half.Y && center.X + half.X <= 2048 && center.Y + half.Y <= 2048,
                "clamp remains authoritative while smoothing");
        }
        Console.WriteLine($"Motion selftest: {checks} numeric checks passed.");
    }
}