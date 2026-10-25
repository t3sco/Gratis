using System.Numerics;
using Gratis.Rendering;
using SkiaSharp;

namespace Gratis.Preview;

internal static class ArrowSmoothingVerification {
    internal const int Fps = 600;
    internal static float[] Response(PlayerArrowSmoothing level) {
        var filter = new PlayerHeading();
        filter.Advance(0, 0, level, 1, Vector3.Zero, frameId: 0);
        var values = new float[Fps * 2 + 1];
        for (var i = 0; i < values.Length; i++)
            values[i] = filter.Advance(MathF.PI / 2, i / (double)Fps, level, 1, Vector3.Zero, frameId: i + 1);
        return values;
    }

    internal static double Ninety(float[] values) => Array.FindIndex(values, value => value >= .9f * MathF.PI / 2)
        / (double)Fps;
    private static void Check(bool value, string message) {
        if (!value)
            throw new InvalidOperationException("Arrow smoothing: " + message);
    }

    public static void Run() {
        var times = Enum.GetValues<PlayerArrowSmoothing>().Select(level => Ninety(Response(level))).ToArray();
        Check(times[0] == 0 && times[1] is > .25 and < .35 && times[2] is > .54 and < .64
            && times[3] is > 1.10 and < 1.20,
            "Off/Low/Medium/High must reach 90% at visibly different times");
        Check(times[2] > 1.8 * times[1] && times[3] > 1.8 * times[2],
            "Each smoothing level must be substantially slower");
        var config = new MapDrawingConfig {
            ArrowPoints = PlayerArrowDirection.Camera, PlayerArrowSmoothing = PlayerArrowSmoothing.High
        };
        Check(!config.ReduceMotion, "Reduce motion must default off");
        var orientation = new PlayerOrientation();
        orientation.Advance(config, 0, MathF.PI / 2, 0, 0, 1, Vector3.Zero, -90);
        for (var i = 1; i <= 90; i++)
            orientation.Advance(config, 0, MathF.PI, i / 60d, i, 1, new(i * .1f, 0, 0), -180);
        var h = orientation.Smoothing;
        Check(h.ResetCount == 1 && h.LastResetReason == "initial-sample",
            "Walking/camera updates must not reset every frame");
        Check(h.AdvanceCallsPerFrame == 1 && h.IntegratedCallsPerFrame == 1 && h.Tau == .5,
            "Exactly one valid integration per frame");
        var before = orientation.Angle;
        var beforeRaw = h.RawAngle;
        orientation.Advance(config, 0, 0, 1.5, 90, 1, new(9, 0, 0));
        Check(orientation.Angle == before && h.RawAngle == beforeRaw && h.AdvanceCallsPerFrame == 2
            && h.IntegratedCallsPerFrame == 1 && h.DuplicateFrameCalls == 1,
            "A second map layer must preserve source/output and only record a duplicate call");
        orientation.Advance(config, 0, null, 91 / 60d, 91, 1, new(9.1f, 0, 0));
        Check(orientation.Selection.Fallback && h.ResetCount == 1,
            "Temporary camera fallback must keep the smoother alive");
        config.ReduceMotion = true;
        var raw = PlayerOrientation.NativeConeAngle(0);
        Check(orientation.Advance(config, 0, 0, 92 / 60d, 92, 1, new(9.2f, 0, 0), 0) == raw
            && h.LastResetReason == "reduce-motion-enabled",
            "Reduce motion intentionally bypasses");
        var resets = h.ResetCount;
        orientation.Advance(config, 0, 0, 93 / 60d, 93, 1, new(9.3f, 0, 0), 0);
        Check(h.ResetCount == resets, "Reduced frames must not inflate reset counts");
        Check(orientation.MotionDiagnostics(config).Contains("bypass=ReduceMotion")
            && orientation.MotionDiagnostics(config).Contains("AdvanceCallsPerFrame=1"),
            "Diagnostics must expose the actual bypass and frame calls");
        var reset = new PlayerHeading();
        reset.Advance(0, 0, PlayerArrowSmoothing.High, 1, Vector3.Zero, frameId: 0);
        reset.Advance(1, .01, PlayerArrowSmoothing.High, 2, Vector3.Zero, frameId: 1);
        Check(reset.ResetCount == 2 && reset.LastResetReason == "map-changed", "Map reset diagnostic");
        reset.Advance(1, .02, PlayerArrowSmoothing.High, 2, new(31, 0, 0), frameId: 2);
        Check(reset.ResetCount == 3 && reset.LastResetReason == "teleport", "Teleport reset diagnostic");
        reset.Advance(1, -.01, PlayerArrowSmoothing.High, 2, new(31, 0, 0), frameId: 3);
        Check(reset.ResetCount == 4 && reset.LastResetReason == "clock-rewind", "Clock reset diagnostic");
        Console.WriteLine($"Arrow smoothing 90% step response: Off={times[0] * 1000:0}ms "
            + $"Low={times[1] * 1000:0}ms Medium={times[2] * 1000:0}ms "
            + $"High={times[3] * 1000:0}ms; persistent state, duplicate-layer guard, fallback "
            + "and bypass/reset diagnostics passed.");
    }

    internal static void Plot(string output) {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        var levels = Enum.GetValues<PlayerArrowSmoothing>();
        var series = levels.Select(Response).ToArray();
        SKColor[] colours = [new(210, 220, 230), new(50, 205, 190), new(246, 191, 80), new(157, 142, 255)];
        using var bitmap = new SKBitmap(1280, 720);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { IsAntialias = true };
        using var typeface = SKTypeface.Default;
        using var title = new SKFont(typeface, 27);
        using var font = new SKFont(typeface, 18);
        canvas.Clear(new SKColor(22, 25, 31));
        paint.Color = new(235, 239, 246);
        canvas.DrawText("Own-player arrow: 90° camera step", 60, 48, SKTextAlign.Left, title, paint);
        canvas.DrawText("Shared PlayerHeading filter · shortest arc · 540°/s limit · Reduce motion off", 60, 82,
            SKTextAlign.Left, font, paint);
        const float left = 80, right = 1200, top = 170, bottom = 610;
        float X(float seconds) => left + seconds / 2 * (right - left);
        float Y(float degrees) => bottom - degrees / 90 * (bottom - top);
        paint.StrokeWidth = 1;
        for (var degrees = 0; degrees <= 90; degrees += 15) {
            paint.Color = new(63, 69, 80);
            canvas.DrawLine(left, Y(degrees), right, Y(degrees), paint);
            paint.Color = new(180, 190, 204);
            canvas.DrawText(degrees + "°", 25, Y(degrees) + 6, SKTextAlign.Left, font, paint);
        }
        for (var tick = 0; tick <= 8; tick++) {
            var seconds = tick / 4f;
            paint.Color = new(63, 69, 80);
            canvas.DrawLine(X(seconds), top, X(seconds), bottom, paint);
            paint.Color = new(180, 190, 204);
            canvas.DrawText(seconds.ToString("0.00") + "s", X(seconds) - 16, 646, SKTextAlign.Left, font, paint);
        }
        paint.Color = new(115, 122, 136);
        paint.StrokeWidth = 1;
        canvas.DrawLine(left, Y(81), right, Y(81), paint);
        canvas.DrawText("90%", right - 45, Y(81) - 7, SKTextAlign.Left, font, paint);
        for (var k = 0; k < levels.Length; k++) {
            paint.Color = colours[k];
            paint.StrokeWidth = 3;
            paint.Style = SKPaintStyle.Stroke;
            using var path = new SKPath();
            for (var i = 0; i < series[k].Length; i++) {
                var point = new SKPoint(X(i / (float)Fps), Y(series[k][i] * 180 / MathF.PI));
                if (i == 0)
                    path.MoveTo(point);
                else
                    path.LineTo(point);
            }
            canvas.DrawPath(path, paint);
            paint.Style = SKPaintStyle.Fill;
            var t90 = Ninety(series[k]);
            canvas.DrawCircle(X((float)t90), Y(81), 5, paint);
            canvas.DrawText($"{levels[k]}  τ={PlayerHeading.TimeConstant(levels[k]):0.##}s / 90%={t90 * 1000:0}ms",
                60 + k * 305, 127, SKTextAlign.Left, font, paint);
        }
        paint.Color = new(180, 190, 204);
        canvas.DrawText("Time after camera step", 520, 688, SKTextAlign.Left, font, paint);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        using (var file = File.Create(output))
            png.SaveTo(file);
        using var csv = new StreamWriter(Path.ChangeExtension(output, ".csv"));
        csv.WriteLine("seconds,rawDegrees,offDegrees,lowDegrees,mediumDegrees,highDegrees");
        for (var i = 0; i < series[0].Length; i++)
            csv.WriteLine(string.Create(global::System.Globalization.CultureInfo.InvariantCulture,
                $"{i / (double)Fps:0.######},90,{series[0][i] * 180 / MathF.PI:0.######},"
                    + $"{series[1][i] * 180 / MathF.PI:0.######},{series[2][i] * 180 / MathF.PI:0.######},"
                    + $"{series[3][i] * 180 / MathF.PI:0.######}"));
        Console.WriteLine($"{output}: actual shared filter response; companion CSV written.");
    }
}

internal static partial class Program {
    private static bool TryArrowSmoothingScene(string[] args) {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || args[at + 1] != "arrow-smoothing")
            return false;
        var outputAt = Array.IndexOf(args, "--out");
        ArrowSmoothingVerification.Plot(outputAt >= 0 ? args[outputAt + 1] : "_preview/arrow-smoothing.png");
        return true;
    }
}