using System.Numerics;
using Gratis.Rendering;

namespace Gratis.Preview;

internal sealed class ViewportTimeline
{
    public readonly MapDrawingConfig Config = new() { ClampToMap = false, ScaleWithFrame = false };
    public readonly ViewportMotion Motion = new();
    public Vector2 Size = new(900, 600), Texture = new(2048), Player, Target, Offset;
    public float Scale = 1;
    public ulong Map = 1;
    public long Frame;
    public double Now;
    public bool Held, Hovered, Focused;

    public void Tick(double now)
    {
        Now = now;
        Motion.Advance(Config, Map, now, Frame++, Size, Texture, Player, Target, Held, Hovered, Focused, ref Scale, ref Offset);
    }
    public void LayerAgain() => Motion.Advance(Config, Map, Now, Frame - 1, Size, Texture, Player, Target,
        Held, Hovered, Focused, ref Scale, ref Offset);
    public void Drag(double now, Vector2 offset)
    {
        Config.FollowPlayer = Motion.Interact(Config, now, true);
        Offset = offset;
        Held = Hovered = Focused = true;
        Tick(now);
    }
    public bool Wheel(float requested, bool linear = false)
    {
        Config.FollowPlayer = Motion.Interact(Config, Now, false);
        return Motion.QueueZoom(Config, requested, new Vector2(80, -40), Scale, Size, Texture, linear);
    }
}

internal static class ViewportMotionVerification
{
    public static void Run()
    {
        var checks = 0;
        void Check(bool ok, string name)
        {
            if (!ok) throw new InvalidOperationException("Viewport timeline assertion: " + name);
            checks++;
        }
        foreach (var linear in new[] { false, true }) {
            var zoom = new ViewportTimeline();
            zoom.Config.FollowPlayer = false;
            zoom.Tick(0);
            Check(zoom.Wheel(2, linear), "wheel enters production queue");
            var intermediate = 0;
            for (var frame = 1; frame <= 120; frame++) {
                zoom.Tick(frame / 60.0);
                if (zoom.Scale > 1 && zoom.Scale < 2) intermediate++;
                var before = zoom.Scale; var offset = zoom.Offset;
                zoom.LayerAgain();
                Check(zoom.Scale == before && zoom.Offset == offset, "fog layer cannot snap/interpolate twice");
            }
            Check(intermediate >= 5 && zoom.Scale == 2, "120-frame wheel timeline includes eased intermediates and endpoint");
        }
        foreach (var returnOption in new[] { false, true }) {
            var follow = new ViewportTimeline();
            follow.Config.FollowReturnToPlayer = returnOption;
            follow.Tick(0);
            for (var frame = 1; frame <= 120; frame++) {
                follow.Player = new Vector2(frame * .1f, 0);
                follow.Target = follow.Player;
                follow.Tick(frame / 60.0);
                Check(follow.Offset.X > 0 && follow.Offset.X < follow.Target.X, "moving player is eased through real viewport path");
                var before = follow.Offset;
                follow.LayerAgain();
                Check(follow.Offset == before, "fog cannot overwrite damped follow");
            }
        }
        var coarse = new ViewportTimeline();
        coarse.Config.FollowReturnToPlayer = false;
        coarse.Config.Motion.SettleSeconds = .3f;
        coarse.Tick(0);
        coarse.Motion.QueueZoom(coarse.Config, 2, Vector2.Zero, coarse.Scale, coarse.Size, coarse.Texture, false);
        coarse.Target = new Vector2(100, 0);
        coarse.Tick(.3);
        Check(coarse.Scale > 1 && coarse.Scale < 2, "300ms zoom remains damped");
        Check(coarse.Offset.X > 0 && coarse.Offset.X < 100, "300ms follow remains damped");
        Check(Math.Abs(coarse.Motion.LastElapsed - .3) < 1e-8 && coarse.Motion.LastStep == .3f, "coarse elapsed reported");
        var zeroScale = coarse.Scale; var zeroOffset = coarse.Offset;
        coarse.Tick(.3);
        Check(coarse.Scale == zeroScale && coarse.Offset == zeroOffset, "zero elapsed new frame preserves motion rather than snapping");
        var reduced = new ViewportTimeline();
        reduced.Tick(0);
        reduced.Config.ReduceMotion = true;
        reduced.Target = new Vector2(100, 20);
        reduced.Tick(1.0 / 60);
        Check(reduced.Offset == reduced.Target && !reduced.Wheel(2), "ReduceMotion disables queue and snaps follow");
        var classic = new ViewportTimeline();
        classic.Config.VisualStyle = 1;
        classic.Tick(0);
        Check(!classic.Wheel(2), "Classic never queues animation");
        var recovery = new ViewportTimeline();
        recovery.Config.Motion = null!;
        recovery.Tick(0);
        Check(recovery.Config.Motion is { EaseFollow: true, EaseZoom: true, SettleSeconds: .12f }, "null old motion settings recover defaults");
        var diagnostics = coarse.Motion.Diagnostics(coarse.Config, .3);
        Check(diagnostics.Contains("calls/s=") && diagnostics.Contains("lastScale=zoom") && diagnostics.Contains("offset:follow="), "diagnostics report call cadence and named writers");
        Console.WriteLine($"Motion timeline selftest: {checks} numeric checks passed (120 frames at 60Hz, real viewport engine, duplicate layers, 300ms/zero dt, Classic/ReduceMotion).");
    }
}
