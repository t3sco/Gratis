using System.Numerics;
using System.Text.Json;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class FollowVerification
{
    public static void Run()
    {
        var checks = 0;
        void Check(bool ok, string name)
        {
            if (!ok) throw new InvalidOperationException("Follow assertion: " + name);
            checks++;
        }
        ViewportTimeline Detached()
        {
            var t = new ViewportTimeline();
            t.Tick(0);
            t.Drag(.01, new Vector2(-100, 60));
            return t;
        }
        var t = Detached();
        Check(t.Config.FollowPlayer && t.Motion.Follow.State == FollowState.Detached, "drag detaches and keeps toolbar follow on");
        t.Player = new Vector2(1, 0); t.Target = t.Player;
        t.Tick(.02);
        Check(t.Motion.Follow.State == FollowState.Detached, "movement cannot return while held");
        t.Held = false; t.Tick(.03);
        Check(t.Motion.Follow.State == FollowState.Detached, "release under cursor remains detached");
        t.Hovered = false; t.Tick(.04);
        Check(t.Motion.Follow.State == FollowState.Detached, "movement during drag/hover is not latched");
        t.Player += new Vector2(.4f, 0); t.Target = t.Player; t.Tick(.05);
        Check(t.Motion.Follow.State == FollowState.Detached, "epsilon ignores sub-0.5 movement");
        t.Player += new Vector2(.2f, 0); t.Target = t.Player; t.Tick(.06);
        Check(t.Motion.Follow.State == FollowState.Returning, "release+leave+new movement starts return");
        var previous = t.Offset;
        for (var frame = 1; frame <= 120; frame++) {
            t.Tick(.06 + frame / 120.0);
            Check(t.Offset.X >= previous.X && t.Offset.X <= t.Target.X && t.Offset.Y <= previous.Y && t.Offset.Y >= t.Target.Y,
                "critical return never overshoots");
            previous = t.Offset;
        }
        Check(t.Motion.Follow.State == FollowState.Following && Vector2.Distance(t.Offset, t.Target) < .001f, "return settles into following");
        t = Detached(); t.Held = false; t.Focused = false; t.Player = Vector2.One; t.Tick(.02);
        Check(t.Motion.Follow.State == FollowState.Returning, "unfocused eligibility works even under cursor");
        t.Drag(.03, new Vector2(-80));
        Check(t.Motion.Follow.State == FollowState.Detached && t.Offset == new Vector2(-80), "grabbing during return immediately cancels it");
        t = Detached(); t.Config.FollowReturnTrigger = FollowReturnTrigger.WhenUnfocused;
        t.Held = t.Hovered = false; t.Player = Vector2.One; t.Tick(.02);
        Check(t.Motion.Follow.State == FollowState.Detached, "unfocused trigger ignores movement while focused");
        t.Focused = false; t.Tick(.03);
        Check(t.Motion.Follow.State == FollowState.Returning, "unfocused trigger needs no movement");
        t = Detached(); t.Config.FollowReturnTrigger = FollowReturnTrigger.AfterIdle;
        t.Held = t.Hovered = false; t.Tick(2.99);
        Check(t.Motion.Follow.State == FollowState.Detached, "idle trigger waits three seconds");
        t.Tick(3.02);
        Check(t.Motion.Follow.State == FollowState.Returning, "idle trigger works without movement");
        t = Detached(); t.Held = t.Hovered = false; t.Config.ReduceMotion = true; t.Player = Vector2.One; t.Target = new Vector2(90); t.Tick(.02);
        Check(t.Motion.Follow.State == FollowState.Following && t.Offset == t.Target, "ReduceMotion instant return");
        t = Detached(); t.Config.FollowReturnEaseMs = 0; t.Held = t.Hovered = false; t.Player = Vector2.One; t.Target = new Vector2(70); t.Tick(.02);
        Check(t.Offset == t.Target, "zero duration instant return");
        t = Detached(); t.Player = new Vector2(51, 0); t.Target = new Vector2(510); t.Tick(.02);
        Check(t.Offset == t.Target && t.Motion.Follow.State == FollowState.Following, "teleport over 50 world units snaps even while detached");
        t = Detached(); t.Map = 2; t.Target = new Vector2(300); t.Tick(.02);
        Check(t.Offset == t.Target && t.Motion.Follow.State == FollowState.Following, "map change immediately reattaches");
        t = Detached(); t.Map = (1UL << 32) | t.Map; t.Target = new Vector2(250); t.Tick(.02);
        Check(t.Offset == t.Target && t.Motion.Follow.State == FollowState.Following, "zone change with same map immediately reattaches");
        t = Detached(); t.Held = t.Hovered = false; t.Player = Vector2.One; t.Tick(.02); t.Tick(.08);
        var oldOffset = t.Offset; t.Target = oldOffset - new Vector2(1); t.Tick(.09);
        Check(t.Offset.X <= oldOffset.X && t.Offset.X >= t.Target.X, "retargeted spring cannot cross its new target");
        foreach (var circle in new[] { false, true }) {
            t = new ViewportTimeline(); t.Config.ClampToMap = true; t.Config.MapCircle = circle; t.Config.ScaleWithFrame = true;
            t.Config.Motion.EaseFollow = false;
            t.Tick(0); t.Drag(.01, new Vector2(9000)); t.Held = t.Hovered = false;
            t.Player = Vector2.One; t.Target = new Vector2(-9000); t.Tick(.02);
            var savedScale = t.Scale;
            for (var i = 1; i <= 120; i++) {
                if (i == 30) t.Size *= 1.5f;
                t.Tick(.02 + i / 120.0);
                var center = new Vector2(1024) - t.Offset;
                var half = ViewportMath.HalfFrame(t.Size, circle) / t.Scale;
                Check(center.X >= half.X - .001f && center.Y >= half.Y - .001f && center.X + half.X <= 2048.001f && center.Y + half.Y <= 2048.001f,
                    "clamp covers rectangle/circle during return and resize");
            }
            Check(Math.Abs(t.Scale - savedScale * 1.5f) < 1e-5f, "return preserves user's frame-relative zoom");
        }
        Vector2 AtRate(int fps)
        {
            var timeline = Detached(); timeline.Held = timeline.Hovered = false; timeline.Player = Vector2.One; timeline.Target = new Vector2(80, -30); timeline.Tick(.02);
            for (var i = 1; i <= fps / 2; i++) timeline.Tick(.02 + i / (double)fps);
            return timeline.Offset;
        }
        Check(Vector2.Distance(AtRate(30), AtRate(144)) < .0001f, "same return result at 30/144Hz");
        t = new ViewportTimeline(); t.Tick(0); t.Wheel(2); t.Player = Vector2.One; t.Tick(.02);
        Check(t.Config.FollowPlayer && t.Motion.Follow.State == FollowState.Detached && t.Motion.PendingZoom.HasValue,
            "wheel detaches without disabling follow and pending zoom cannot fight return");
        for (var i = 1; i <= 60; i++) t.Tick(.02 + i / 60.0);
        Check(t.Scale == 2 && t.Motion.Follow.State == FollowState.Detached, "completed wheel zoom stays detached until new movement");
        t.Player += Vector2.One; t.Tick(1.04);
        Check(t.Motion.Follow.State == FollowState.Returning && t.Scale == 2, "wheel return preserves final user zoom");
        t = new ViewportTimeline(); t.Config.FollowReturnToPlayer = false; t.Tick(0); t.Drag(.01, new Vector2(-100));
        Check(!t.Config.FollowPlayer && t.Offset == new Vector2(-100), "option off restores drag-to-unfollow");
        t = Detached(); t.Config.FollowReturnToPlayer = false; t.Drag(.02, new Vector2(-120));
        Check(!t.Config.FollowPlayer, "turning option off while detached restores old next-drag behaviour");
        t = new ViewportTimeline(); t.Config.VisualStyle = 1; t.Tick(0); t.Drag(.01, new Vector2(-100));
        Check(!t.Config.FollowPlayer, "Classic retains drag-to-unfollow");
        var json = new JsonSerializerOptions { IncludeFields = true };
        var old = JsonSerializer.Deserialize<MapDrawingConfig>("{}", json)!;
        Check(old.FollowReturnToPlayer && old.FollowReturnTrigger == FollowReturnTrigger.WhenPlayerMoves && old.FollowReturnIdleSeconds == 3 && old.FollowReturnEaseMs == 350, "old config loads new defaults");
        old.FollowReturnToPlayer = false; old.FollowReturnTrigger = FollowReturnTrigger.AfterIdle; old.FollowReturnIdleSeconds = 7; old.FollowReturnEaseMs = 900;
        old = JsonSerializer.Deserialize<MapDrawingConfig>(JsonSerializer.Serialize(old, json), json)!;
        Check(!old.FollowReturnToPlayer && old.FollowReturnTrigger == FollowReturnTrigger.AfterIdle && old.FollowReturnIdleSeconds == 7 && old.FollowReturnEaseMs == 900, "all options roundtrip");
        Console.WriteLine($"Follow selftest: {checks} numeric checks passed; state/trigger rules, bounded critical return, teleport/map snap, rect/circle clamp + resize, 30/144Hz, legacy config and option-off.");
    }
}
