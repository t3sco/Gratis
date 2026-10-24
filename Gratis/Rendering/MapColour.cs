using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace Gratis.Rendering;

public enum MapColourPreset { Original, Midnight, Mono, Blueprint, Sepia, Custom }

public sealed class MapColourSettings {
    public MapColourPreset Preset = MapColourPreset.Original;
    public float Brightness = 0, Contrast = 1, Saturation = 1, HueShift = 0, TintStrength = 0;
    public Vector3 Tint = Vector3.One;
    public bool GradientEnabled = false;
    public Vector3 Shadow = Vector3.Zero, Mid = new(.5f), Highlight = Vector3.One;

    [JsonIgnore]
    public MapColourParameters Parameters => Preset == MapColourPreset.Custom
        ? new MapColourParameters(Safe(Brightness, -1, 1, 0), Safe(Contrast, 0, 3, 1),
            Safe(Saturation, 0, 2, 1), Safe(HueShift, -180, 180, 0), Colour(Tint),
            Safe(TintStrength, 0, 1, 0), GradientEnabled, Colour(Shadow), Colour(Mid), Colour(Highlight))
        : MapColour.Preset(Preset);

    private static float Safe(float value, float min, float max, float fallback) =>
        float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    private static Vector3 Colour(Vector3 value) => new(Safe(value.X, 0, 1, 0), Safe(value.Y, 0, 1, 0), Safe(value.Z, 0, 1, 0));
}

public readonly record struct MapColourParameters(
    float Brightness, float Contrast, float Saturation, float HueShift,
    Vector3 Tint, float TintStrength, bool GradientEnabled,
    Vector3 Shadow, Vector3 Mid, Vector3 Highlight, float WaterAccent = 0) {
    public static MapColourParameters Identity => new(0, 1, 1, 0, Vector3.One, 0, false,
        Vector3.Zero, new Vector3(.5f), Vector3.One);
    public bool IsIdentity => Brightness == 0 && Contrast == 1 && Saturation == 1 && HueShift == 0 &&
                              TintStrength == 0 && !GradientEnabled && WaterAccent == 0;
}

public static class MapColour {
    public static Vector4 MarkerColour(Vector4 color, MapColourPreset preset) =>
        preset == MapColourPreset.Mono && MathF.Min(color.X, MathF.Min(color.Y, color.Z)) > .75f &&
        MathF.Max(color.X, MathF.Max(color.Y, color.Z)) - MathF.Min(color.X, MathF.Min(color.Y, color.Z)) < .12f
            ? new Vector4(.12f, .15f, .19f, color.W) : color;

    public static MapColourParameters Preset(MapColourPreset preset) => preset switch {
        MapColourPreset.Midnight => MapColourParameters.Identity with {
            Contrast = 1.12f,
            Saturation = .35f,
            GradientEnabled = true,
            Shadow = Rgb(194, 213, 220),
            Mid = Rgb(51, 69, 83),
            Highlight = Rgb(12, 22, 35),
            WaterAccent = .7f,
        },
        MapColourPreset.Mono => MapColourParameters.Identity with { Saturation = 0, Contrast = 1.15f },
        MapColourPreset.Blueprint => MapColourParameters.Identity with {
            Contrast = 1.2f,
            GradientEnabled = true,
            Shadow = Rgb(199, 248, 255),
            Mid = Rgb(35, 116, 156),
            Highlight = Rgb(7, 27, 63),
            WaterAccent = .35f,
        },
        MapColourPreset.Sepia => MapColourParameters.Identity with {
            Contrast = 1.06f,
            GradientEnabled = true,
            Shadow = Rgb(40, 26, 22),
            Mid = Rgb(151, 112, 71),
            Highlight = Rgb(244, 220, 170),
        },
        _ => MapColourParameters.Identity,
    };

    private static Vector3 Rgb(int r, int g, int b) => new Vector3(r, g, b) / 255;

    public static byte[] Crossfade(byte[] original, byte[] target, float amount, CancellationToken cancellationToken = default) {
        if (original.Length != target.Length || original.Length % 4 != 0)
            throw new ArgumentException("Crossfade needs matching RGBA buffers.");
        if (amount <= 0) return original;
        if (amount >= 1) return target;
        var result = GC.AllocateUninitializedArray<byte>(original.Length);
        Parallel.For(0, (original.Length + 65535) / 65536,
            new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Math.Min(8, Environment.ProcessorCount) }, chunk => {
                var end = Math.Min(original.Length, (chunk + 1) * 65536);
                for (var i = chunk * 65536; i < end; i += 4) {
                    result[i] = (byte)(original[i] + (target[i] - original[i]) * amount + .5f);
                    result[i + 1] = (byte)(original[i + 1] + (target[i + 1] - original[i + 1]) * amount + .5f);
                    result[i + 2] = (byte)(original[i + 2] + (target[i + 2] - original[i + 2]) * amount + .5f);
                    result[i + 3] = original[i + 3];
                }
            });
        return result;
    }

    public static byte[] Apply(byte[] rgba, MapColourParameters parameters, bool premultiplied = false,
        CancellationToken cancellationToken = default) {
        if (rgba.Length % 4 != 0) throw new ArgumentException("Expected RGBA pixels.", nameof(rgba));
        if (parameters.IsIdentity) return rgba;
        var result = GC.AllocateUninitializedArray<byte>(rgba.Length);
        var transform = new Transform(parameters);
        const int chunkBytes = 65536;
        Parallel.For(0, (rgba.Length + chunkBytes - 1) / chunkBytes,
            new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Math.Min(8, Environment.ProcessorCount) }, chunk => {
                var start = chunk * chunkBytes;
                var length = Math.Min(chunkBytes, rgba.Length - start);
                transform.Apply(rgba.AsSpan(start, length), result.AsSpan(start, length), premultiplied);
            });
        return result;
    }

    private readonly struct Transform {
        private readonly Vector3 red, green, blue, offset;
        private readonly MapColourParameters p;

        public Transform(MapColourParameters parameters) {
            p = parameters;
            var angle = p.HueShift * MathF.PI / 180;
            var c = MathF.Cos(angle); var s = MathF.Sin(angle) / MathF.Sqrt(3);
            var d = (1 - c) / 3;
            var hueR = new Vector3(c + d, d - s, d + s);
            var hueG = new Vector3(d + s, c + d, d - s);
            var hueB = new Vector3(d - s, d + s, c + d);
            var luma = new Vector3(.2126f, .7152f, .0722f);
            var tintScale = Vector3.Lerp(Vector3.One, p.Tint, p.TintStrength);
            red = (hueR * p.Saturation + luma * (1 - p.Saturation)) * (p.Contrast * tintScale.X);
            green = (hueG * p.Saturation + luma * (1 - p.Saturation)) * (p.Contrast * tintScale.Y);
            blue = (hueB * p.Saturation + luma * (1 - p.Saturation)) * (p.Contrast * tintScale.Z);
            offset = new Vector3(.5f * (1 - p.Contrast) + p.Brightness) * tintScale;
        }

        public void Apply(ReadOnlySpan<byte> input, Span<byte> output, bool premultiplied) {
            for (var i = 0; i < input.Length; i += 4) {
                var alpha = input[i + 3];
                if (alpha == 0) { input.Slice(i, 4).CopyTo(output.Slice(i, 4)); continue; }
                var source = new Vector3(input[i], input[i + 1], input[i + 2]) / (premultiplied ? alpha : 255f);
                var rgb = Vector3.Clamp(new Vector3(Vector3.Dot(source, red), Vector3.Dot(source, green),
                    Vector3.Dot(source, blue)) + offset, Vector3.Zero, Vector3.One);
                if (p.GradientEnabled) {
                    var luminance = Vector3.Dot(rgb, new Vector3(.2126f, .7152f, .0722f));
                    rgb = luminance < .5f ? Vector3.Lerp(p.Shadow, p.Mid, luminance * 2)
                        : Vector3.Lerp(p.Mid, p.Highlight, luminance * 2 - 1);
                }
                if (p.WaterAccent > 0) {
                    var water = Math.Clamp((MathF.Min(source.Y, source.Z) - source.X) * 5, 0, 1) * p.WaterAccent;
                    rgb = Vector3.Lerp(rgb, rgb * new Vector3(.55f, 1.3f, 1.55f), water);
                }
                rgb = Vector3.Clamp(rgb, Vector3.Zero, Vector3.One) * (premultiplied ? alpha : 255);
                output[i] = (byte)(rgb.X + .5f); output[i + 1] = (byte)(rgb.Y + .5f);
                output[i + 2] = (byte)(rgb.Z + .5f); output[i + 3] = alpha;
            }
        }
    }
}
