using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Gratis.Classes.MapWindowComponents;

public unsafe class MapCoordinateBar
{
    public void Draw(bool isMapHovered, Vector2 mapDrawOffset)
    {
        var agent = AgentMap.Instance();
        if (System.SystemConfig.IsGratis) {
            var offset = new Vector2(-agent->SelectedOffsetX, -agent->SelectedOffsetY);
            var local = Service.ObjectTable.LocalPlayer;
            var character = Rendering.CoordinateShare.WorldToMap(local == null ? Vector2.Zero : new(local.Position.X, local.Position.Z), offset, agent->SelectedMapSizeFactorFloat);
            Vector2? pointed = null;
            if (isMapHovered) {
                var world = System.MapRenderer.Frame.ScreenToWorld(ImGui.GetMousePos(), mapDrawOffset);
                pointed = Rendering.CoordinateShare.WorldToMap(world, offset, agent->SelectedMapSizeFactorFloat);
                Controllers.CoordinateShareGame.Observe(world, agent->SelectedMapId, agent->SelectedTerritoryId);
            }
            Rendering.MapCoordinateDrawing.Draw(character, local != null && agent->SelectedMapId == agent->CurrentMapId, pointed);
            return;
        }
        var offsetX = -agent->SelectedOffsetX;
        var offsetY = -agent->SelectedOffsetY;
        var scale = (uint)agent->SelectedMapSizeFactor;
        var player = MapUtil.WorldToMap(Service.ObjectTable.LocalPlayer?.Position ?? Vector3.Zero, offsetX, offsetY, 0, scale);
        Vector2? cursor = null;
        if (isMapHovered) {
            var position = (ImGui.GetMousePos() - mapDrawOffset - System.MapRenderer.DrawPosition) / MapRenderer.MapRenderer.Scale;
            position = (position - new Vector2(1024) - new Vector2(offsetX, offsetY)) / agent->SelectedMapSizeFactorFloat;
            var mapCursor = MapUtil.WorldToMap(new Vector3(position.X, 0, position.Y), offsetX, offsetY, 0, scale);
            cursor = new Vector2(mapCursor.X, mapCursor.Y);
        }
        Rendering.MapCoordinateDrawing.Draw(new Vector2(player.X, player.Y), agent->SelectedMapId == agent->CurrentMapId, cursor);
    }
}
