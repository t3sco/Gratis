using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using Gratis.Classes;
using Gratis.Extensions;

namespace Gratis.MapRenderer;

public partial class MapRenderer
{
    private readonly Rendering.PlayerHeading playerHeading = new();

    private unsafe void DrawPlayer()
    {
        if (AgentMap.Instance()->SelectedMapId != AgentMap.Instance()->CurrentMapId) return;

        if (Service.ObjectTable.LocalPlayer is { } localPlayer) {
            var motion = Rendering.MapDrawContext.Current.Motion;
            var texturePosition = localPlayer.GetMapPosition() - DrawHelpers.GetMapOffsetVector() + DrawHelpers.GetMapCenterOffsetVector();
            if (Rendering.MapMotion.Enabled && System.SystemConfig.Motion.PlayerTrail)
                motion.Trail.Add(texturePosition, ImGui.GetTime(), System.SystemConfig.Motion.TrailSeconds);
            motion.DrawTrail(ImGui.GetWindowPos() + DrawPosition, Scale, ImGui.GetTime());
            var position = ImGui.GetWindowPos() +
                           DrawPosition +
                           (localPlayer.GetMapPosition() -
                            DrawHelpers.GetMapOffsetVector() +
                            DrawHelpers.GetMapCenterOffsetVector()) * Scale;

            var config = Rendering.MapDrawContext.Current.Config;
            var raw = -localPlayer.Rotation + MathF.PI / 2.0f;
            var angle = playerHeading.Advance(raw, ImGui.GetTime(), config.PlayerArrowSmoothing,
                AgentMap.Instance()->CurrentMapId, localPlayer.Position, config.ReduceMotion || !config.IsGratis);
            Rendering.PlayerDrawing.DrawLookLine(position, Scale,
                config is { IsGratis: true, FlatPlayer: true } ? angle : GetCameraRotation());
            DrawPlayerIcon(position, config.IsGratis ? angle : raw);
        }
    }

    private static unsafe float GetCameraRotation() => -DegreesToRadians(AreaMapNumberArray.Instance()->ConeRotation) - 0.5f * MathF.PI;

    private static float DegreesToRadians(float degrees) => MathF.PI / 180.0f * degrees;

    private void DrawPlayerIcon(Vector2 position, float angle)
    {
        if (!System.SystemConfig.ShowPlayerIcon) return;
        var texture = Service.TextureProvider.GetFromGameIcon(60443).GetWrapOrEmpty();

        Rendering.PlayerDrawing.DrawPlayerIcon(position, new Rendering.DrawTexture(texture.Handle, texture.Size), angle, Scale);
    }

}
