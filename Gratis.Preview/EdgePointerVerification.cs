using System.Numerics;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class EdgePointerVerification {
    private static void Require(bool yes, string message) {
        if (!yes)
            throw new InvalidOperationException(message);
    }

    public static void Maths() {
        var min = new Vector2(32, 20);
        var max = min + new Vector2(640, 480);
        var center = (min + max) / 2;
        foreach (var circle in new[] { false, true }) {
            for (var i = 0; i < 360; i++) {
                var target = center + new Vector2(MathF.Cos(i * MathF.PI / 180), MathF.Sin(i * MathF.PI / 180)) * 1000;
                EdgePointerBounds[] obstacles = [new(min, new(max.X, min.Y + 95)), new(new(min.X, max.Y - 95), max)];
                Require(EdgePointerMath.Project(target, min, max, circle, new(35, 12), 8, obstacles, out var p),
                    "Perimeter must have space");
                Require(obstacles.All(o =>
                    !new EdgePointerBounds(o.Min - new Vector2(35, 12), o.Max + new Vector2(35, 12)).Contains(p)),
                    "Chrome overlap");
                foreach (var corner in new[] { new Vector2(-35, -12), new Vector2(35, 12), new Vector2(-35, 12),
                    new Vector2(35, -12) })
                    Require(EdgePointerMath.Outside(p + corner, min, max, circle) <= -7.9f, "Chip escaped inset frame");
            }
            Require(EdgePointerMath.Project(center, min, max, circle, new(12), 8, [], out _), "Zero direction");
            Require(!EdgePointerMath.Project(new(float.NaN), min, max, circle, new(12), 8, [], out _),
                "Invalid target");
            Require(!EdgePointerMath.Project(max * 10, min, max, circle, new(12), 8, [new(min, max)], out _),
                "Fully blocked perimeter");
            Require(!EdgePointerMath.Project(max, min, min + new Vector2(20), circle, new(12), 8, [], out _),
                "Tiny frame");
        }
    }

    internal static FrameScript Scene(bool crowded = false) => new() {
        Width = 960, Height = 800,
        Markers = Enumerable.Range(0, crowded ? 24 : 3).Select(i => {
            var angle = i * MathF.Tau / (crowded ? 24 : 3);
            return new ScriptMarker(i % 2 == 0 ? 71003u : 60071u,
                new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (650 + i * 12), Objective: true);
        }).ToList()
    };
    public static void Frames() {
        foreach (var circle in new[] { false, true }) {
            var config = PaletteVerification.Config();
            config.MapCircle = circle;
            config.EdgePointerMax = 3;
            using var driver = new ScriptedFrameDriver(config, Scene(true));
            driver.Step(new(0));
            driver.Step(new(.1));
            var pointers = driver.Context.EdgePointers;
            Require(pointers.Drawn.Count == 3, "Nearest max pointers");
            Require(pointers.Drawn.Zip(pointers.Drawn.Skip(1)).All(p => p.First.Distance <= p.Second.Distance),
                "Nearest ordering");
            var center = pointers.Drawn[0].Center;
            driver.Step(new(.12, Mouse: center));
            driver.Step(new(.14, Left: true));
            Require(driver.Renderer.Spotlight.Active && !driver.Window.Dragging,
                "Pointer click Spotlights without pan");
            driver.Step(new(.16, Left: false));
            Require(driver.Renderer.Spotlight.Active, "Pointer click must not cancel Spotlight");
            config.EdgePointers = false;
            driver.Step(new(.2));
            Require(pointers.Drawn.Count == 0, "Disabled pointers");
            config.EdgePointers = true;
            config.VisualStyle = 1;
            driver.Step(new(.3));
            Require(pointers.Drawn.Count == 0, "Classic unchanged");
        }
    }
}