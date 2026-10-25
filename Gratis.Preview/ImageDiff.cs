using System.Runtime.InteropServices;
using System.Text.Json;
using SkiaSharp;

namespace Gratis.Preview;

internal readonly record struct DiffResult(int Width, int Height, double MeanAbsoluteError, double NormalizedMeanError,
    int MaxChannelError, long ChangedPixels, double ChangedPixelFraction);

internal static class ImageDiff {
    public static DiffResult Run(string ours, string reference, string output, bool announce = true) {
        output = Path.GetFullPath(output);
        if (!Path.GetExtension(output).Equals(".png", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Diff output must be a PNG.");
        foreach (var input in new[] { ours, reference })
            if (Path.GetFullPath(input).Equals(output, StringComparison.OrdinalIgnoreCase)
                || Path.GetFullPath(input).Equals(Path.ChangeExtension(output, ".json"),
                    StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Diff outputs must differ from both input paths.");
        using var a = Decode(ours);
        using var b = Decode(reference);
        if (a.Width != b.Width || a.Height != b.Height)
            throw new ArgumentException(
                $"Image dimensions differ: {a.Width}x{a.Height} vs {b.Width}x{b.Height}. Crop to "
                + "identical bounds first; no resizing is performed.");
        using var heat = new SKBitmap(new SKImageInfo(a.Width, a.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var length = checked(a.Width * a.Height * 4);
        var first = new byte[length];
        var second = new byte[length];
        var pixels = new byte[length];
        Marshal.Copy(a.GetPixels(), first, 0, length);
        Marshal.Copy(b.GetPixels(), second, 0, length);
        long sum = 0, changed = 0;
        var maximum = 0;
        for (var index = 0; index < length; index += 4) {
            var error = 0;
            for (var channel = 0; channel < 3; channel++) {
                var delta = Math.Abs(first[index + channel] - second[index + channel]);
                error += delta;
                maximum = Math.Max(maximum, delta);
            }
            sum += error;
            if (error != 0)
                changed++;
            var value = error * 4 / 3;
            pixels[index] = (byte)Math.Clamp(value, 0, 255);
            pixels[index + 1] = (byte)Math.Clamp(value - 255, 0, 255);
            pixels[index + 3] = 255;
        }
        Marshal.Copy(pixels, 0, heat.GetPixels(), length);
        var mean = (double)sum / (a.Width * a.Height * 3L);
        var result = new DiffResult(a.Width, a.Height, mean, mean / 255, maximum, changed,
            (double)changed / (a.Width * a.Height));
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        using (var stream = File.Create(output))
            if (!heat.Encode(stream, SKEncodedImageFormat.Png, 100))
                throw new IOException("Cannot encode diff PNG.");
        File.WriteAllText(Path.ChangeExtension(output, ".json"),
            JsonSerializer.Serialize(result, ScriptedFrameDriver.Json));
        if (announce)
            Console.WriteLine($"RGB mean error {mean:F6}/255 ({mean / 255:P4}); max channel {maximum}; changed "
                + $"{changed}/{a.Width * a.Height} pixels. Heat-map: {output}");
        return result;
    }

    private static SKBitmap Decode(string path) {
        using var codec = SKCodec.Create(path) ?? throw new InvalidDataException($"Cannot decode {path}.");
        var bitmap = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888,
            SKAlphaType.Unpremul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) == SKCodecResult.Success)
            return bitmap;
        bitmap.Dispose();
        throw new InvalidDataException($"Cannot read {path}.");
    }

    public static void Verify() {
        var directory = Path.Combine(AppContext.BaseDirectory, "diff-selftest");
        Directory.CreateDirectory(directory);
        var a = Path.Combine(directory, "a.png");
        var b = Path.Combine(directory, "b.png");
        var output = Path.Combine(directory, "heat.png");
        using var image = new SKBitmap(2, 1);
        image.Erase(SKColors.Black);
        Save(image, a);
        var same = Run(a, a, output, false);
        if (same.MeanAbsoluteError != 0 || same.ChangedPixels != 0)
            throw new InvalidOperationException("Identical diff must be zero.");
        image.SetPixel(0, 0, SKColors.White);
        Save(image, b);
        var changed = Run(a, b, output, false);
        if (changed.MeanAbsoluteError != 127.5 || changed.MaxChannelError != 255 || changed.ChangedPixels != 1)
            throw new InvalidOperationException("Known pixel error must be exact.");
        using var heat = SKBitmap.Decode(output);
        if (heat.GetPixel(0, 0) != SKColors.Yellow || heat.GetPixel(1, 0) != SKColors.Black)
            throw new InvalidOperationException("Heat-map pixel values do not match the metric.");
        using var small = new SKBitmap(1, 1);
        small.Erase(SKColors.Black);
        Save(small, b);
        try {
            Run(a, b, output, false);
        }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Mismatched sizes must fail.");
    }

    private static void Save(SKBitmap image, string path) {
        using var stream = File.Create(path);
        if (!image.Encode(stream, SKEncodedImageFormat.Png, 100))
            throw new IOException("Cannot encode test PNG.");
    }
}