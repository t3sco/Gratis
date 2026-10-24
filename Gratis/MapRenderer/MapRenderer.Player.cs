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

            DrawLookLine(position);
            DrawPlayerIcon(position);
        }
    }

    private void DrawLookLine(Vector2 position) => Rendering.PlayerDrawing.DrawLookLine(position, Scale, GetCameraRotation());

    private static unsafe float GetCameraRotation() => -DegreesToRadians(AreaMapNumberArray.Instance()->ConeRotation) - 0.5f * MathF.PI;

    private static float DegreesToRadians(float degrees) => MathF.PI / 180.0f * degrees;

    private void DrawPlayerIcon(Vector2 position)
    {
        if (!System.SystemConfig.ShowPlayerIcon) return;
        if (Service.ObjectTable is not { LocalPlayer: { } player }) return;

        var texture = Service.TextureProvider.GetFromGameIcon(60443).GetWrapOrEmpty();
        var angle = -player.Rotation + MathF.PI / 2.0f;

        Rendering.PlayerDrawing.DrawPlayerIcon(position, new Rendering.DrawTexture(texture.Handle, texture.Size), angle, Scale);
    }

}
