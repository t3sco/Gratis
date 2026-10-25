using System.Text.Json;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;

namespace Gratis.Preview;

internal static class CameraVerification {
    private static void Check(bool condition, string message) {
        if (!condition)
            throw new InvalidOperationException("Camera: " + message);
    }

    public static void Run() {
        static bool Near(float actual, double expected) => Math.Abs(PlayerHeading.Wrap(actual - expected)) < 2e-6;
        (float Cone, double Screen, float Character)[] conventions = [
            (0, -Math.PI / 2, MathF.PI), (-90, 0, MathF.PI / 2), (180, Math.PI / 2, 0),
            (90, -Math.PI, -MathF.PI / 2),
                (270, 0, MathF.PI / 2),
                (-180, Math.PI / 2, 0),
                (360, -Math.PI / 2, MathF.PI),
        ];
        foreach (var (cone, screen, rotation) in conventions) {
            Check(Near(PlayerOrientation.NativeConeAngle(cone), screen), "Native cone cardinal/sign convention");
            Check(Near(PlayerOrientation.CharacterAngle(rotation), screen),
                "Character convention must remain unchanged");
            foreach (float? yaw in new float?[] { null, 0, 1.3f, float.NaN }) {
                var chosen = PlayerOrientation.Select(PlayerArrowDirection.Camera, rotation, yaw, cone);
                Check(chosen.Source == PlayerArrowDirection.Camera && !chosen.Fallback && Near(chosen.Raw, screen),
                    "Native cone must win regardless of manager yaw/availability");
            }
            Check(Near(PlayerOrientation.NativeConeAngle(cone + 720), screen), "Native cone wraps full turns");
        }
        Check(Near(PlayerOrientation.CharacterAngle(0), Math.PI / 2)
            && Near(PlayerOrientation.CharacterAngle(MathF.PI / 2), 0),
            "character conversion preserves old orientation");
        for (var i = -20; i <= 20; i++) {
            var angle = PlayerOrientation.CameraAngle(i * .6f);
            Check(Math.Abs(angle) <= Math.PI && Near(PlayerOrientation.CameraAngle(i * .6f + 2 * MathF.PI), angle),
                "camera wrap");
        }
        foreach (float? unavailable in new float?[] { null, float.NaN, float.PositiveInfinity }) {
            var selection = PlayerOrientation.Select(PlayerArrowDirection.Camera, 0, 0, unavailable);
            Check(selection.Fallback && selection.Source == PlayerArrowDirection.Character
                && Near(selection.Raw, Math.PI / 2),
                "missing/invalid camera falls back to character");
        }
        var character = PlayerOrientation.Select(PlayerArrowDirection.Character, 0, 0, 0);
        Check(!character.Fallback && character.Source == PlayerArrowDirection.Character
            && Near(character.Raw, Math.PI / 2) && character.Camera.HasValue,
            "character option ignores available camera but retains diagnostics");
        var off = new MapDrawingConfig {
            ArrowPoints = PlayerArrowDirection.Camera, PlayerArrowSmoothing = PlayerArrowSmoothing.Off
        };
        var moving = new PlayerOrientation();
        for (var frame = 0; frame <= 120; frame++) {
            var angle = moving.Advance(off, frame * .1f, frame * .15f, frame / 60d, frame, 1, new(frame * .1f, 0, 0),
                -90);
            Check(Near(angle, 0), "Moving/turning character and manager must not rotate an east-pointing native cone");
        }
        moving.Advance(off, 0, 0, 3, 121, 1, new(12, 0, 0), -90);
        var diagnostics = moving.CameraDiagnostics("simulated");
        foreach (var field in new[] {
            "nativeConeRotation=-90",
            "cameraManagerYaw=0",
            "characterRotation=0",
            "finalArrowAngle=0",
            "managerVsCone=-90" })
            Check(diagnostics.Contains(field), "Diagnostics must expose raw conventions: " + field);
        Check(moving.Smoothing.ResetCount == 1, "Ordinary movement must not reset the native-cone arrow");
        var options = new JsonSerializerOptions { IncludeFields = true };
        var legacy = JsonSerializer.Deserialize<SystemConfig>("{}", options)!;
        Check(legacy.ArrowPoints == PlayerArrowDirection.Character, "old settings default to Character");
        legacy.ArrowPoints = PlayerArrowDirection.Camera;
        Check(JsonSerializer.Deserialize<SystemConfig>(JsonSerializer.Serialize(legacy, options),
            options)!.ArrowPoints == PlayerArrowDirection.Camera,
            "saved direction roundtrip");
        SettingsLayout.Reset(legacy, "Markers");
        Check(legacy.ArrowPoints == PlayerArrowDirection.Character, "reset restores Character");
        foreach (var mode in Enum.GetValues<PlayerArrowDirection>()) {
            using var driver = Create(mode);
            driver.Step(new(0));
            driver.Step(new(.1));
            Check(driver.PlayerOrientation.Selection.Source == mode && !driver.PlayerOrientation.Selection.Fallback,
                "live shared path selects configured source");
            Check(Near(driver.PlayerOrientation.Angle,
                mode == PlayerArrowDirection.Camera ? -Math.PI / 6 : Math.PI / 2),
                "arrow and cone receive selected angle");
        }
        Console.WriteLine("Camera: native cone convention matrix, +/-360 wraps, 90-degree manager "
            + "disagreement, moving-character independence, fallback, diagnostics and shared arrow/cone path passed.");
    }

    internal static ScriptedFrameDriver Create(PlayerArrowDirection mode) => new(new SystemConfig {
        AcceptedSpoilerWarning = true, FadeMode = 0, ArrowPoints = mode, AutoHideChrome = false,
        WindowPosition = new(24), WindowSize = new(592, 472), MapScale = .5f, ScaleWithFrame = false,
        PlayerIconScale = 1.5f, SoftConeLength = 140,
    }, new FrameScript {
        Width = 640, Height = 520, Heading = MathF.PI / 2, CameraYaw = MathF.PI / 3, NativeConeDegrees = -60,
        Title = $"Arrow points: {mode} · character south / camera northeast",
        Markers = [new(60421, new(-160, -100)), new(60430, new(160, 100))]
    });
}

internal static partial class Program {
    private static bool TryCameraScene(string[] args) {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || args[at + 1] is not ("arrow-camera" or "arrow-character"))
            return false;
        var scene = args[at + 1];
        var outputAt = Array.IndexOf(args, "--out");
        var output = outputAt >= 0 ? args[outputAt + 1] : $"_preview/{scene}.png";
        using var driver = CameraVerification.Create(scene == "arrow-camera" ? PlayerArrowDirection.Camera
            : PlayerArrowDirection.Character);
        driver.Step(new(0));
        driver.Step(new(.1));
        driver.Save(output);
        Console.WriteLine(driver.PlayerOrientation.CameraDiagnostics("simulated"));
        return true;
    }
}