using System.Diagnostics;
using System.Numerics;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class MapColourVerification
{
    public static void Run()
    {
        var source = new byte[256 * 4];
        for (var alpha = 0; alpha < 256; alpha++) {
            var i = alpha * 4;
            source[i] = (byte)alpha; source[i + 1] = (byte)(alpha / 2);
            source[i + 2] = (byte)(alpha / 3); source[i + 3] = (byte)alpha;
        }
        var snapshot = source.ToArray();
        foreach (var preset in Enum.GetValues<MapColourPreset>()) {
            var neutral = MapColour.MarkerColour(Vector4.One, preset);
            Require(neutral.W == 1 && (preset == MapColourPreset.Mono ? neutral.X < .2f : neutral == Vector4.One), "Neutral marker palette differs.");
            var category = new Vector4(.1f, .8f, 1, .6f);
            Require(MapColour.MarkerColour(category, preset) == category, "Saturated marker palette changed.");
        }
        Require(ReferenceEquals(source, MapColour.Apply(source, MapColour.Preset(MapColourPreset.Original))), "Original allocated a buffer.");
        Require(new MapColourSettings().Parameters.IsIdentity, "Old config defaults must be identity.");
        foreach (var preset in Enum.GetValues<MapColourPreset>()) {
            var parameters = MapColour.Preset(preset);
            var result = MapColour.Apply(source, parameters, premultiplied: true);
            var halfway = MapColour.Crossfade(source, result, .5f);
            for (var i = 0; i < result.Length; i += 4) {
                Require(result[i + 3] == source[i + 3] && halfway[i + 3] == source[i + 3], "Alpha changed.");
                Require(result[i] <= result[i + 3] && result[i + 1] <= result[i + 3] && result[i + 2] <= result[i + 3], "Invalid premultiplied RGB.");
            }
            var straight = MapColour.Apply(source, parameters);
            for (var i = 3; i < straight.Length; i += 4) Require(straight[i] == source[i], "Straight alpha changed.");
            var opaque = MapColour.Apply([255, 153, 51, 255], parameters);
            var premul = MapColour.Apply([85, 51, 17, 85], parameters, premultiplied: true);
            for (var i = 0; i < 3; i++) Require(Math.Abs(premul[i] - opaque[i] / 3f) <= 1, "Premultiplication changed colour semantics.");
        }
        Require(source.AsSpan().SequenceEqual(snapshot), "Source was mutated.");
        var jsonOptions = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        Require(System.Text.Json.JsonSerializer.Deserialize<MapColourSettings>("{}", jsonOptions)!.Parameters.IsIdentity, "Missing saved settings changed defaults.");
        var custom = new MapColourSettings { Preset = MapColourPreset.Custom, Tint = new Vector3(.2f, .4f, .8f), TintStrength = .5f };
        var json = System.Text.Json.JsonSerializer.Serialize(custom, jsonOptions);
        Require(!json.Contains("Parameters") && System.Text.Json.JsonSerializer.Deserialize<MapColourSettings>(json, jsonOptions)!.Parameters == custom.Parameters,
            "Custom settings failed serialization roundtrip.");
        byte[] grey = [0, 0, 0, 255, 128, 128, 128, 128, 255, 255, 255, 255];
        var gradient = MapColourParameters.Identity with { GradientEnabled = true, Shadow = Vector3.UnitX,
            Mid = Vector3.UnitY, Highlight = Vector3.UnitZ };
        var mapped = MapColour.Apply(grey, gradient);
        Require(mapped[0] == 255 && mapped[1] == 0 && mapped[2] == 0 && mapped[10] == 255, "Gradient endpoints differ.");
        Require(mapped[5] >= 253 && mapped[4] <= 2 && mapped[6] <= 2, "Gradient midpoint differs.");
        var tint = MapColour.Apply([200, 100, 50, 85], MapColourParameters.Identity with { Tint = new Vector3(.5f, 1, 0), TintStrength = 1 });
        Require(tint.AsSpan().SequenceEqual(new byte[] { 100, 100, 0, 85 }), "Tint multiplication differs.");
        var mono = MapColour.Apply([255, 0, 0, 255], MapColourParameters.Identity with { Saturation = 0 });
        Require(mono[0] == 54 && mono[0] == mono[1] && mono[1] == mono[2], "Luminance differs.");
        var adjusted = MapColour.Apply([64, 128, 192, 255], MapColourParameters.Identity with { Contrast = 2, Brightness = .1f });
        Require(adjusted.AsSpan().SequenceEqual(new byte[] { 26, 154, 255, 255 }), "Brightness/contrast differs.");
        var rotated = MapColour.Apply([128, 128, 128, 255], MapColourParameters.Identity with { HueShift = 120 });
        Require(rotated.AsSpan().SequenceEqual(new byte[] { 128, 128, 128, 255 }), "Hue changed neutral RGB.");

        var large = new byte[2048 * 2048 * 4];
        for (var i = 0; i < large.Length; i += source.Length) source.CopyTo(large, i);
        foreach (var preset in new[] { MapColourPreset.Midnight, MapColourPreset.Mono, MapColourPreset.Blueprint, MapColourPreset.Sepia }) {
            var parameters = MapColour.Preset(preset);
            _ = MapColour.Apply(large, parameters);
            var milliseconds = new double[3];
            for (var run = 0; run < milliseconds.Length; run++) {
                var timer = Stopwatch.StartNew();
                _ = MapColour.Apply(large, parameters);
                milliseconds[run] = timer.Elapsed.TotalMilliseconds;
            }
            Console.WriteLine($"Map Colour {preset}: 2048×2048 RGBA, median {milliseconds.Order().ElementAt(1):F1} ms, max {milliseconds.Max():F1} ms (includes output allocation).");
            Require(milliseconds.Max() < 200, $"{preset} exceeded 200 ms.");
        }
        Console.WriteLine("Map Colour verified: identity/reference, source immutability, all 256 alpha values, premultiplied bounds, crossfade alpha, luminance, tint, hue, brightness/contrast and gradient stops.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
