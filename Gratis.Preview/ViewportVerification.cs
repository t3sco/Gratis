using System.Numerics;
using System.Text.Json;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class ViewportVerification {
    public static void Run() {
        var checks = 0;
        void Check(bool condition, string name) {
            if (!condition)
                throw new InvalidOperationException($"Viewport assertion: {name}");
            checks++;
        }
        var serialization = new JsonSerializerOptions { IncludeFields = true };
        var old = JsonSerializer.Deserialize<MapDrawingConfig>("{\"VisualStyle\":1}", serialization)!;
        Check(old.ScaleWithFrame && old.ClampToMap, "old configuration defaults, including Classic");
        old.ScaleWithFrame = old.ClampToMap = false;
        var saved = JsonSerializer.Deserialize<MapDrawingConfig>(JsonSerializer.Serialize(old, serialization),
            serialization)!;
        Check(!saved.ScaleWithFrame && !saved.ClampToMap, "explicit false settings survive reload");
        saved.ResetViewport();
        Check(saved.ScaleWithFrame && saved.ClampToMap, "reset defaults");

        var random = new Random(18015);
        var reference = new Vector2(432, 288);
        var texture = new Vector2(2048);
        var center = new Vector2(935, 1072);
        var zoom = new ViewportZoom();
        var scale = zoom.Resolve(reference, .7f, true);
        var referenceScale = scale;
        for (var i = 0; i < 1000; i++) {
            var factor = .3f + random.NextSingle() * 4;
            var frame = reference * factor;
            scale = zoom.Resolve(frame, scale, true);
            var originalCorner = center - reference / (2 * referenceScale);
            var resizedCorner = center - frame / (2 * scale);
            Check(Vector2.Distance(originalCorner, resizedCorner) * scale < .5f,
                "resize region within 0.5 screen pixels");
        }
        scale = zoom.Resolve(reference, scale, true);
        Check(scale == referenceScale, "resize returns without accumulated drift");
        scale = zoom.Resolve(reference, scale * 1.5f, true);
        scale = zoom.Resolve(reference * 2, scale, true);
        Check(MathF.Abs(scale - referenceScale * 3) < 1e-6f, "user zoom multiplies frame scaling");
        var stretched = zoom.Resolve(new Vector2(1800, 576), scale, true);
        Check(stretched == scale, "long-axis stretch uses the absolute short side");
        var limits = new ViewportZoom();
        var ceiling = limits.Resolve(reference, 20, true);
        ceiling = limits.Resolve(reference * 2, ceiling, true);
        Check(ceiling == 40, "zoom ceiling scales with the frame instead of cropping the region");
        ceiling = limits.Resolve(reference * 2, 60, true);
        Check(ceiling == 40, "user zoom ceiling is frame-relative");
        limits = new ViewportZoom();
        Check(limits.Resolve(reference * 2, ceiling, true) == 40,
            "saved effective zoom above 20 reloads without a snap");
        limits = new ViewportZoom();
        Check(limits.Resolve(reference / 2, .025f, true, 0) == .025f,
            "saved effective zoom below 0.05 reloads without a snap");

        var cursor = new Vector2(137, -82);
        var anchored = ViewportMath.ZoomAtCursor(center, cursor, .8f, 1.3f);
        Check(Vector2.Distance(center + cursor / .8f, anchored + cursor / 1.3f) * 1.3f < .001f, "cursor anchor");
        var back = ViewportMath.ZoomAtCursor(anchored, cursor, 1.3f, .8f);
        Check(Vector2.Distance(center, back) < .001f, "cursor zoom reverses without drift");

        foreach (var circle in new[] { false, true }) {
            foreach (var scaling in new[] { false, true }) {
                zoom = new ViewportZoom();
                scale = .8f;
                center = texture / 2;
                for (var i = 0; i < 1000; i++) {
                    var frame = new Vector2(80 + random.NextSingle() * 1720, 80 + random.NextSingle() * 1200);
                    var requested = scale * (.2f + random.NextSingle() * 1.7f);
                    var minimum = ViewportMath.MinimumScale(frame, texture, circle);
                    var next = zoom.Resolve(frame, requested, scaling, minimum);
                    var mouse = (new Vector2(random.NextSingle(), random.NextSingle()) - new Vector2(.5f)) * frame;
                    center = ViewportMath.ZoomAtCursor(center, mouse, scale, next);
                    center += new Vector2(random.NextSingle() - .5f, random.NextSingle() - .5f) * 10000;
                    center = ViewportMath.ClampCenter(center, frame, texture, next, circle);
                    var half = ViewportMath.HalfFrame(frame, circle) / next;
                    Check(next >= minimum, "minimum zoom covers frame");
                    Check(center.X - half.X >= -.001f && center.Y - half.Y >= -.001f
                        && center.X + half.X <= texture.X + .001f && center.Y + half.Y <= texture.Y + .001f,
                        "no void after random pan/zoom/resize");
                    scale = next;
                }
            }
        }

        zoom = new ViewportZoom();
        scale = zoom.Resolve(reference, .7f, true);
        scale = zoom.Resolve(new Vector2(1800, 288), scale, true, 1800f / 2048);
        scale = zoom.Resolve(reference, scale, true);
        Check(MathF.Abs(scale - .7f) < 1e-6f, "coverage constraint releases original zoom");
        var edge = ViewportMath.ClampCenter(new Vector2(-900, 5000), reference, texture, .7f, false);
        Check(edge.X == reference.X / 1.4f && MathF.Abs(edge.Y - (2048 - reference.Y / 1.4f)) < .001f,
            "follow target at the map edge is kept inside the texture");
        var rectangularTexture = new Vector2(1024, 2048);
        Check(ViewportMath.MinimumScale(new Vector2(800, 400), rectangularTexture, false) == .78125f,
            "coverage uses the actual texture width and height");
        Check(ViewportMath.MinimumScale(new Vector2(800, 400), rectangularTexture, true) == .390625f,
            "circle coverage uses its diameter");
        zoom = new ViewportZoom();
        foreach (var requested in new[] { .01f, .05f, .37f, 1, 20, 30 }) {
            scale = zoom.Resolve(new Vector2(900, 500), requested, false);
            Check(scale == Math.Clamp(requested, .05f, 20), "legacy scale limits");
        }
        Check(Vector2.Distance(ViewportMath.DrawPosition(new Vector2(900, 500), new Vector2(5000, -9000), .37f),
            new Vector2(1921.12f, -3458.88f)) < .001f,
            "legacy transform golden coordinates");
        Check(ViewportMath.DrawPosition(new Vector2(900, 500), new Vector2(5000, -9000), 1) ==
            new Vector2(4426, -9774), "legacy free pan remains unconstrained");
        Console.WriteLine($"Viewport selftest passed: {checks} assertions; 1,000 resize cycles and 1,000 "
            + "random sequences per rect/circle and scaling state; cursor anchor, legacy "
            + "toggles, config reload/reset.");
    }
}