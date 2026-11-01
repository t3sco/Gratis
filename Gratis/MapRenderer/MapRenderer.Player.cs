using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using Gratis.Classes;
using Gratis.Extensions;

namespace Gratis.MapRenderer;

public partial class MapRenderer {
    private readonly Rendering.PlayerOrientation playerOrientation = new();
    private readonly CameraHeadingReader cameraHeading;
    public string CameraDiagnostics() {
        var sample = cameraHeading.Latest;
        return playerOrientation.CameraDiagnostics($"cone:{sample.ConeState}/manager:{sample.State}");
    }

    public string HeadingDiagnostics() => playerOrientation.MotionDiagnostics(System.SystemConfig, "ImGui.GetTime");

    private unsafe void DrawPlayer() {
        if (AgentMap.Instance()->SelectedMapId != AgentMap.Instance()->CurrentMapId)
            return;

        if (Service.ObjectTable.LocalPlayer is { } localPlayer) {
            var motion = Rendering.MapDrawContext.Current.Motion;
            // Within a frame the map position, window position and ImGui clock do not change, so read each once.
            var texturePosition = localPlayer.GetMapPosition() - DrawHelpers.GetMapOffsetVector()
                + DrawHelpers.GetMapCenterOffsetVector();
            var now = ImGui.GetTime();
            var windowOrigin = ImGui.GetWindowPos() + DrawPosition;
            if (Rendering.MapMotion.Enabled && System.SystemConfig.Motion.PlayerTrail)
                motion.Trail.Add(texturePosition, now, System.SystemConfig.Motion.TrailSeconds);
            motion.DrawTrail(windowOrigin, Scale, now);
            var position = windowOrigin + texturePosition * Scale;

            var config = Rendering.MapDrawContext.Current.Config;
            var raw = -localPlayer.Rotation + MathF.PI / 2.0f;
            var camera = cameraHeading.Latest;
            var angle = playerOrientation.Advance(config, localPlayer.Rotation, camera.Yaw, now,
                ImGui.GetFrameCount(), AgentMap.Instance()->CurrentMapId, localPlayer.Position,
                camera.NativeConeDegrees);
            Rendering.PlayerDrawing.DrawLookLine(position, Scale, config.IsGratis ? angle : GetCameraRotation());
            DrawPlayerIcon(position, config.IsGratis ? angle : raw);
        }
    }

    private static unsafe float GetCameraRotation() => -DegreesToRadians(AreaMapNumberArray.Instance()->ConeRotation)
        - 0.5f * MathF.PI;

    private static float DegreesToRadians(float degrees) => MathF.PI / 180.0f * degrees;

    private void DrawPlayerIcon(Vector2 position, float angle) {
        if (!System.SystemConfig.ShowPlayerIcon)
            return;
        // The flat arrow is built from triangles and never samples the icon, so skip the texture lookup for it.
        var drawTexture = default(Rendering.DrawTexture);
        if (Rendering.MapDrawContext.Current.Config is not { IsGratis: true, FlatPlayer: true }) {
            var texture = Service.TextureProvider.GetFromGameIcon(60443).GetWrapOrEmpty();
            drawTexture = new Rendering.DrawTexture(texture.Handle, texture.Size);
        }

        Rendering.PlayerDrawing.DrawPlayerIcon(position, drawTexture, angle, Scale);
    }

}