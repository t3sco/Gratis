using System;
using System.Numerics;

namespace Gratis.Rendering;

public enum PlayerArrowDirection { Camera, Character }

public readonly record struct PlayerAngleSelection(float Character, float? Camera, float Raw, PlayerArrowDirection Source, bool Fallback);

public sealed class PlayerOrientation
{
    public PlayerHeading Smoothing { get; } = new();
    public PlayerAngleSelection Selection { get; private set; }
    public float Angle { get; private set; }
    public PlayerArrowDirection Preference { get; private set; }
    public long LastFrame { get; private set; } = -1;
    public static float CameraAngle(float yaw) => (float)PlayerHeading.Wrap(yaw - Math.PI / 2);
    public static float CharacterAngle(float rotation) => (float)PlayerHeading.Wrap(-rotation + Math.PI / 2);
    public static PlayerAngleSelection Select(PlayerArrowDirection preference, float characterRotation, float? cameraYaw)
    {
        var character = CharacterAngle(float.IsFinite(characterRotation) ? characterRotation : 0);
        float? camera = cameraYaw is { } yaw && float.IsFinite(yaw) ? CameraAngle(yaw) : null;
        var useCamera = preference == PlayerArrowDirection.Camera && camera.HasValue;
        return new(character, camera, useCamera ? camera!.Value : character,
            useCamera ? PlayerArrowDirection.Camera : PlayerArrowDirection.Character, preference == PlayerArrowDirection.Camera && !useCamera);
    }
    public float Advance(MapDrawingConfig config, float characterRotation, float? cameraYaw, double now,
        long frame, ulong map, Vector3 position)
    {
        if (frame >= 0 && LastFrame == frame) {
            Smoothing.Advance(Selection.Raw, now, config.PlayerArrowSmoothing, map, position, config.ReduceMotion || !config.IsGratis, frame);
            return Angle;
        }
        Preference = config.IsGratis ? config.ArrowPoints : PlayerArrowDirection.Character;
        Selection = Select(Preference, characterRotation, cameraYaw);
        LastFrame = frame;
        Angle = Smoothing.Advance(Selection.Raw, now, config.PlayerArrowSmoothing, map, position, config.ReduceMotion || !config.IsGratis, frame);
        return Angle;
    }
    public string CameraDiagnostics(string readState) => FormattableString.Invariant(
        $"cameraAngle={Degrees(Selection.Camera)} characterAngle={Degrees(Selection.Character)} requested={Preference} source={Selection.Source} fallback={Selection.Fallback} cameraRead={readState} lastDrawFrame={LastFrame} (degrees, east=0 clockwise)");
    public string MotionDiagnostics(MapDrawingConfig config, string clock = "host") => Smoothing.Diagnostics() +
        $" source={Selection.Source} fallback={Selection.Fallback} reduceMotion={config.ReduceMotion} classicBypass={!config.IsGratis} " +
        $"bypass={(!config.IsGratis ? "Classic" : config.ReduceMotion ? "ReduceMotion" : config.PlayerArrowSmoothing == PlayerArrowSmoothing.Off ? "Off" : "none")} clock={clock}";
    private static string Degrees(float? value) => value is { } angle ? (angle * 180 / MathF.PI).ToString("0.##", global::System.Globalization.CultureInfo.InvariantCulture) : "unavailable";
}
