using System.Numerics;
using System.Text.Json;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;

namespace Gratis.Preview;

internal static class CameraVerification
{
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("Camera: " + message); }
    public static void Run()
    {
        static bool Near(float actual, double expected) => Math.Abs(PlayerHeading.Wrap(actual - expected)) < 2e-6;
        Check(Near(PlayerOrientation.CameraAngle(0), -Math.PI / 2), "north camera");
        Check(Near(PlayerOrientation.CameraAngle(MathF.PI / 2), 0), "east camera");
        Check(Near(PlayerOrientation.CameraAngle(MathF.PI), Math.PI / 2), "south camera");
        Check(Near(PlayerOrientation.CameraAngle(-MathF.PI / 2), Math.PI), "west camera");
        Check(Near(PlayerOrientation.CharacterAngle(0), Math.PI / 2) && Near(PlayerOrientation.CharacterAngle(MathF.PI / 2), 0), "character conversion preserves old orientation");
        for (var i = -20; i <= 20; i++) {
            var angle = PlayerOrientation.CameraAngle(i * .6f);
            Check(Math.Abs(angle) <= Math.PI && Near(PlayerOrientation.CameraAngle(i * .6f + 2 * MathF.PI), angle), "camera wrap");
        }
        foreach (float? unavailable in new float?[] { null, float.NaN, float.PositiveInfinity }) {
            var selection = PlayerOrientation.Select(PlayerArrowDirection.Camera, 0, unavailable);
            Check(selection.Fallback && selection.Source == PlayerArrowDirection.Character && Near(selection.Raw, Math.PI / 2), "missing/invalid camera falls back to character");
        }
        var character = PlayerOrientation.Select(PlayerArrowDirection.Character, 0, 0);
        Check(!character.Fallback && character.Source == PlayerArrowDirection.Character && Near(character.Raw, Math.PI / 2) && character.Camera.HasValue, "character option ignores available camera but retains diagnostics");
        var options = new JsonSerializerOptions { IncludeFields = true };
        var legacy = JsonSerializer.Deserialize<SystemConfig>("{}", options)!;
        Check(legacy.ArrowPoints == PlayerArrowDirection.Camera, "old settings default to Camera");
        legacy.ArrowPoints = PlayerArrowDirection.Character;
        Check(JsonSerializer.Deserialize<SystemConfig>(JsonSerializer.Serialize(legacy, options), options)!.ArrowPoints == PlayerArrowDirection.Character, "saved direction roundtrip");
        SettingsLayout.Reset(legacy, "Markers"); Check(legacy.ArrowPoints == PlayerArrowDirection.Camera, "reset restores Camera");
        foreach (var mode in Enum.GetValues<PlayerArrowDirection>()) {
            using var driver = Create(mode);
            driver.Step(new(0)); driver.Step(new(.1));
            Check(driver.PlayerOrientation.Selection.Source == mode && !driver.PlayerOrientation.Selection.Fallback, "live shared path selects configured source");
            Check(Near(driver.PlayerOrientation.Angle, mode == PlayerArrowDirection.Camera ? -Math.PI / 6 : Math.PI / 2), "arrow and cone receive selected angle");
        }
        Console.WriteLine("Camera: cardinal conversion, wrap, null/nonfinite fallback, source choice, legacy/default/reset and shared arrow/cone path passed.");
    }
    internal static ScriptedFrameDriver Create(PlayerArrowDirection mode) => new(new SystemConfig {
        AcceptedSpoilerWarning = true, FadeMode = 0, ArrowPoints = mode, AutoHideChrome = false,
        WindowPosition = new(24), WindowSize = new(592, 472), MapScale = .5f, ScaleWithFrame = false,
        PlayerIconScale = 1.5f, SoftConeLength = 140,
    }, new FrameScript { Width = 640, Height = 520, Heading = MathF.PI / 2, CameraYaw = MathF.PI / 3,
        Title = $"Arrow points: {mode} · character south / camera northeast",
        Markers = [new(60421, new(-160, -100)), new(60430, new(160, 100))] });
}

internal static partial class Program
{
    private static bool TryCameraScene(string[] args)
    {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || args[at + 1] is not ("arrow-camera" or "arrow-character")) return false;
        var scene = args[at + 1]; var outputAt = Array.IndexOf(args, "--out");
        var output = outputAt >= 0 ? args[outputAt + 1] : $"_preview/{scene}.png";
        using var driver = CameraVerification.Create(scene == "arrow-camera" ? PlayerArrowDirection.Camera : PlayerArrowDirection.Character);
        driver.Step(new(0)); driver.Step(new(.1)); driver.Save(output);
        Console.WriteLine(driver.PlayerOrientation.CameraDiagnostics("simulated"));
        return true;
    }
}
