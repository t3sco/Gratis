using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class SpotlightVerification {
    private static void Require(bool condition, string message) {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    public static void Fit() {
        var frame = new Vector2(640, 480);
        var texture = new Vector2(2048);
        var insets = new Vector4(48, 80, 48, 80);
        foreach (var circle in new[] { false, true }) {
            foreach (var clamp in new[] { false, true }) {
                foreach (var pair in new[] {
                    (new Vector2(1024), new Vector2(1024)),
                    (new Vector2(800, 900), new Vector2(1250, 1100)),
                    (new Vector2(100, 120), new Vector2(200, 220)),
                    (new Vector2(80, 1900), new Vector2(1900, 80)),
                    (new Vector2(-10000), new Vector2(10000)),
                }) {
                    var fit = ViewportMath.FitBounds(pair.Item1, pair.Item2, frame, texture, circle, clamp, 3, insets);
                    var minimum = clamp ? ViewportMath.MinimumScale(frame, texture, circle) : .05f;
                    Require(float.IsFinite(fit.Scale) && fit.Scale >= minimum && fit.Scale <= 3,
                        "Fit respects zoom bounds");
                    var center = new Vector2(1024) - fit.Offset;
                    if (clamp)
                        Require(Vector2.Distance(center,
                            ViewportMath.ClampCenter(center, frame, texture, fit.Scale, circle)) < .001f,
                            "Fit respects texture bounds");
                    if (fit.ContainsBoth) {
                        var safe = ViewportMath.HalfFrame(frame, circle) - new Vector2(48, 80);
                        foreach (var point in new[] { pair.Item1, pair.Item2 }) {
                            var p = Vector2.Abs(point - center) * fit.Scale / safe;
                            Require(circle ? p.LengthSquared() <= 1.00001f : p.X <= 1.00001f && p.Y <= 1.00001f,
                                "Both symbols must fit padded frame");
                        }
                    }
                    else
                        Require(MathF.Abs(fit.Scale - minimum) < .00001f,
                            "Impossible distances zoom out as far as clamp permits");
                }
            }
        }
        var close = ViewportMath.FitBounds(new(1000), new(1050), frame, texture, false, true, 1.5f, insets);
        Require(close.Scale == 1.5f, "Close targets use the close-up cap");
        var edge = ViewportMath.FitBounds(new(30, 1024), new(40, 1024), frame, texture, false, true, 3, insets);
        Require(edge.ContainsBoth && edge.Scale == 3,
            "Near-edge padding can require zooming in from the clamp minimum");
        var rect = ViewportMath.FitBounds(new(700), new(1300), frame, texture, false, false, 3, insets);
        var round = ViewportMath.FitBounds(new(700), new(1300), frame, texture, true, false, 3, insets);
        Require(round.Scale < rect.Scale, "Diagonal circle fitting accounts for the curved frame");
        foreach (var scaleWithFrame in new[] { false, true }) {
            var config = Config();
            config.ScaleWithFrame = scaleWithFrame;
            config.ReduceMotion = true;
            var motion = new ViewportMotion();
            var scale = .5f;
            var offset = new Vector2(20, -30);
            var player = new Vector2(-200, 100);
            var target = new Vector2(240, -100);
            motion.Advance(config, 1, 0, 0, frame, texture, player, -player, false, false, true, ref scale, ref offset);
            motion.Spotlight.Begin(new(target, 1, SpotlightSource.Search), config, 1, scale, offset, frame);
            motion.SpotlightTargetTexture = new Vector2(1024) + target;
            motion.SpotlightPlayerTexture = new Vector2(1024) + player;
            motion.Advance(config, 1, .1, 1, frame, texture, player, -player, false, false, true, ref scale,
                ref offset);
            motion.Advance(config, 1, .3, 2, frame * 2, texture, player, -player, false, false, true, ref scale,
                ref offset);
            motion.Spotlight.End(.3, "test restore");
            motion.Advance(config, 1, .6, 3, frame * 2, texture, player, -player, false, false, true, ref scale,
                ref offset);
            Require(MathF.Abs(scale - (scaleWithFrame ? 1 : .625f)) < .0001f,
                "Restoration honours frame scaling and the resized ClampToMap minimum");
            Require(!motion.Spotlight.OwnsViewport, "Reduced motion return finishes after fade");
        }
    }

    private static SystemConfig Config(bool follow = false) => new() {
        AcceptedSpoilerWarning = true, FadeMode = 0, HideWindowFrame = true,
        WindowPosition = new Vector2(32), WindowSize = new Vector2(640, 480),
        MapScale = .5f, FollowPlayer = follow,
    };
    private static FrameScript Scene() => new() {
        DrawOffset = new Vector2(45, -20), Player = new Vector2(-240, 120),
        Markers = [new(71001, new Vector2(320, -140), Objective: true), new(60412, new Vector2(0, 80))],
    };

    public static void Frames() {
        using (var driver = new ScriptedFrameDriver(Config(), Scene())) {
            driver.Step(new(0));
            var original = driver.Step(new(.1));
            driver.Step(new(.12, QuestLink: new(new Vector2(320, -140), 1, Icon: 71001)));
            var framed = driver.Step(new(.72, Redraws: 2));
            Require(driver.Renderer.Spotlight.Active && driver.Renderer.Spotlight.IncludePlayer,
                "Quest link starts player + target spotlight");
            Require(framed.QuestsDrawn > 0 && framed.Scale != original.Scale,
                "Shared render draws target with dynamic zoom");
            Require(framed.MotionAdvances == 1 && framed.TexturePasses == 2,
                "Spotlight advances once with multiple layers");
            driver.Context.Visibility.Overrides[(int)MarkerVisibility.Group(driver.Markers[0])] =
                MarkerVisibilityState.Hidden;
            var filtered = driver.Step(new(.74));
            Require(filtered.QuestsDrawn > 0 && MarkerVisual.Alpha(driver.Markers[0]) == 1,
                "Spotlight target stays fully visible through lens filters");
            Require(MathF.Abs(driver.Renderer.Spotlight.MarkerAlpha(driver.Markers[1]) - .06f) < .001f,
                "Ambient markers almost vanish at the start of a spotlight");
            Require(MathF.Abs(driver.Renderer.Spotlight.MarkerBreath(driver.Markers[0]) - 1) > .001f,
                "Target icon breathes with the pulse");
            driver.Step(new(.8, Mouse: new Vector2(350, 240)));
            driver.Step(new(.82, Left: true));
            Require(!driver.Renderer.Spotlight.Active, "Map click/drag immediately ends spotlight");
            driver.Step(new(.88, Mouse: new Vector2(390, 260)));
            driver.Step(new(.92, Left: false, Mouse: new Vector2(-1000)));
            FrameSnapshot restored = default;
            for (var i = 1; i <= 90; i++)
                restored = driver.Step(new(.92 + i / 60.0));
            Require(!driver.Renderer.Spotlight.OwnsViewport && driver.Renderer.Spotlight.Opacity == 0,
                "Fade and return release viewport ownership");
            Require(MathF.Abs(restored.Scale - framed.Scale) < .05f,
                "Dragging out of a spotlight keeps the view instead of gliding back");
            driver.Step(new(3, QuestLink: new(null, 1)));
            Require(!driver.Renderer.Spotlight.OwnsViewport, "Missing target uses normal behaviour");
            driver.Config.QuestSpotlight = false;
            driver.Step(new(3.1, QuestLink: new(new Vector2(320, -140), 1)));
            Require(!driver.Renderer.Spotlight.OwnsViewport, "Disabled spotlight uses normal behaviour");
        }
        using (var driver = new ScriptedFrameDriver(Config(true), Scene())) {
            driver.Config.FollowReturnToPlayer = true;
            driver.Step(new(0));
            driver.Step(new(.1));
            driver.Step(new(.12, QuestLink: new(new Vector2(320, -140), 1)));
            driver.Step(new(.4));
            Require(driver.Config.FollowPlayer && driver.Renderer.Spotlight.Active,
                "Spotlight detaches without mutating saved follow preference");
            driver.Step(new(.5, Escape: true));
            for (var i = 1; i <= 90; i++)
                driver.Step(new(.5 + i / 60.0, Player: new Vector2(-200, 120)));
            Require(driver.Config.FollowPlayer && !driver.Renderer.Spotlight.OwnsViewport,
                "Escape smoothly restores following: " + driver.Renderer.Spotlight.Diagnostics());
            var offset = driver.Renderer.DrawOffset;
            Require(Vector2.Distance(offset, driver.CoordinateOffset(new Vector2(-200, 120))) < .01f,
                "Follow return restores the moving player's current position");
            driver.Step(new(2.1, QuestLink: new(new Vector2(320, -140), 1)));
            driver.Step(new(2.4));
            driver.Step(new(2.5, TerritoryId: 2));
            Require(!driver.Renderer.Spotlight.OwnsViewport && driver.Renderer.Spotlight.Opacity == 0,
                "Zone change with same map ID cannot leak dim state");
        }
        using (var driver = new ScriptedFrameDriver(Config(), Scene())) {
            driver.Config.ReduceMotion = true;
            driver.Config.SpotlightSeconds = .25f;
            driver.Step(new(0));
            var before = driver.Step(new(.1));
            driver.Step(new(.12, QuestLink: new(new Vector2(320, -140), 1, SameMap: false)));
            Require(!driver.Renderer.Spotlight.IncludePlayer
                && driver.Config.MapScale == driver.Renderer.Spotlight.FitZoom,
                "Other maps frame only target; Reduce motion snaps fit");
            driver.Step(new(.32));
            var target = new SpotlightRequest(new Vector2(80, -60), 1, SpotlightSource.Pin);
            driver.Renderer.BeginSpotlight(target);
            driver.Step(new(.34));
            driver.Step(new(.64));
            driver.Step(new(.86));
            Require(!driver.Renderer.Spotlight.OwnsViewport && MathF.Abs(driver.Config.MapScale - before.Scale) < .0001f
                && Vector2.Distance(driver.Renderer.DrawOffset, before.Offset) < .01f,
                "Replacement and expiry restore the original viewport");
            driver.Step(new(.9, QuestLink: new(new Vector2(320, -140), 1)));
            driver.Step(new(1, MapId: 2));
            Require(driver.Renderer.Spotlight.Opacity == 0 && !driver.Renderer.Spotlight.OwnsViewport,
                "Map selection cancels spotlight");
            driver.Config.VisualStyle = 1;
            Require(!driver.Renderer.BeginSpotlight(target with { MapId = 2 }), "Classic ignores spotlight requests");
        }
    }
}