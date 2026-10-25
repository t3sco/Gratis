using System.Numerics;
using System.Text.Json;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class HeadingVerification {
    public static void Run() {
        static float Rad(double degrees) => (float)(degrees * Math.PI / 180);
        static void Check(bool condition, string message) {
            if (!condition)
                throw new InvalidOperationException("Heading: " + message);
        }
        static float Step(PlayerHeading h, float raw, double t,
            PlayerArrowSmoothing level = PlayerArrowSmoothing.Medium, ulong map = 1, Vector3 position = default,
            bool reduced = false) =>
            h.Advance(raw, t, level, map, position, reduced);

        foreach (var sign in new[] { -1, 1 }) {
            var h = new PlayerHeading();
            Step(h, Rad(sign * 178), 0);
            var next = Step(h, Rad(-sign * 178), .05);
            var delta = PlayerHeading.Wrap(next - Rad(sign * 178));
            Check(Math.Sign(delta) == sign && Math.Abs(delta) < Rad(4), "shortest arc at +/-180");
        }
        var still = new PlayerHeading();
        Step(still, 0, 0);
        for (var i = 1; i <= 100; i++)
            Check(Step(still, Rad(i % 2 == 0 ? 1.9 : -1.9), i / 60d) == 0, "deadzone jitter");
        foreach (var level in new[] { PlayerArrowSmoothing.Low, PlayerArrowSmoothing.Medium,
            PlayerArrowSmoothing.High }) {
            var results = new List<float>();
            foreach (var fps in new[] { 30, 60, 144 }) {
                var h = new PlayerHeading();
                Step(h, 0, 0, level);
                var previous = 0f;
                for (var i = 1; i <= fps; i++) {
                    var next = Step(h, Rad(170), i / (double)fps, level);
                    Check(next >= previous && next <= Rad(170), "no overshoot");
                    Check(next - previous <= PlayerHeading.MaximumSpeed / fps + 1e-6, "angular speed cap");
                    previous = next;
                }
                results.Add(previous);
            }
            Check(results.Max() - results.Min() < 1e-6, "30/60/144 fps equivalence");
        }
        var spin = new PlayerHeading();
        Step(spin, 0, 0);
        var last = 0f;
        for (var i = 1; i <= 600; i++) {
            var next = Step(spin, Rad(Math.Min(i / 120d, 1) * 720), i / 120d);
            Check(Math.Abs(next) <= Math.PI
                && Math.Abs(PlayerHeading.Wrap(next - last)) <= PlayerHeading.MaximumSpeed / 120 + 1e-6,
                "720 degree spin stays bounded");
            last = next;
        }
        Check(Math.Abs(PlayerHeading.Wrap(last)) < 1e-5, "spin settles");
        var off = new PlayerHeading();
        for (var i = 0; i < 100; i++) {
            var raw = Rad(i * 17);
            Check(Step(off, raw, i / 60d, PlayerArrowSmoothing.Off) == raw, "Off equals raw");
        }
        var reset = new PlayerHeading();
        Step(reset, 0, 0);
        Check(Step(reset, 1, .01, map: 2) == 1, "map reset");
        Check(Step(reset, 2, .02, map: 2, position: new Vector3(31, 0, 0)) == 2, "teleport reset");
        Check(Step(reset, -1, .03, map: 2, position: new Vector3(31, 0, 0), reduced: true) == -1,
            "Reduce Motion reset");
        var json = new JsonSerializerOptions { IncludeFields = true };
        var legacy = JsonSerializer.Deserialize<MapDrawingConfig>("{\"PlayerIconScale\":0.35,\"ConeSize\":83.75}",
            json)!;
        Check(legacy.PlayerArrowSmoothing == PlayerArrowSmoothing.Low && legacy.PlayerIconScale == .35f
            && legacy.ConeSize == 83.75f,
            "old settings preserve values and default Low");
        foreach (var level in Enum.GetValues<PlayerArrowSmoothing>()) {
            legacy.PlayerArrowSmoothing = level;
            Check(JsonSerializer.Deserialize<MapDrawingConfig>(JsonSerializer.Serialize(legacy, json),
                json)!.PlayerArrowSmoothing == level,
                "setting roundtrip");
        }
        Console.WriteLine("Heading verified: wrap, deadzone, no overshoot, cap, identical 30/60/144 fps "
            + "held targets, 720-degree spin/settle, Off, resets, old settings.");
    }
}