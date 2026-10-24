using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiLib.Window;
using Gratis.Data;
using Gratis.Windows;

namespace Gratis.Classes.MapWindowComponents;

public unsafe class MapContextMenu
{
    public MapContextMenu()
    {
        Rendering.MapDrawContext.Current.ContextMenu.Actions = new Rendering.GameContextMenuActions();
    }

    public void Draw(Vector2 mapDrawOffset)
    {
        using var contextMenu = ImRaii.ContextPopup("Gratis_Context_Menu");

        if (!contextMenu) return;
        Rendering.MapDrawContext.Current.Chrome.Reveal(ImGui.GetTime());

        if (System.SystemConfig.IsGratis && System.SystemConfig.Pins && Rendering.MapDrawContext.Current.Assistance?.PinLayer is { } pins) {
            if (ImGui.MenuItem("Add pin here")) {
                var world = System.MapRenderer.Frame.ScreenToWorld(ImGui.GetMousePosOnOpeningCurrentPopup(), mapDrawOffset);
                pins.Edit(AgentMap.Instance()->SelectedMapId, world);
            }
            if (ImGui.MenuItem("Import pin from clipboard")) pins.Import(ImGui.GetClipboardText());
            if (ImGui.BeginMenu("Pins on this map")) {
                foreach (var pin in pins.Store.ForMap(AgentMap.Instance()->SelectedMapId))
                    if (ImGui.MenuItem(pin.Title + "##" + pin.Id)) pins.Edit(pin.MapId, pin.World, pin);
                ImGui.EndMenu();
            }
            ImGui.Separator();
        }

        if (ImGui.MenuItem("Place Flag")) {
            var cursorPosition = ImGui.GetMousePosOnOpeningCurrentPopup();
            var mapChildOffset = mapDrawOffset;
            var mapDrawPositionOffset = System.MapRenderer.DrawPosition;
            var textureClickLocation = (cursorPosition - mapChildOffset - mapDrawPositionOffset) / MapRenderer.MapRenderer.Scale;
            var result = textureClickLocation - new Vector2(1024.0f, 1024.0f);
            var scaledResult = result / DrawHelpers.GetMapScaleFactor() + DrawHelpers.GetRawMapOffsetVector();

            AgentMap.Instance()->FlagMarkerCount = 0;
            AgentMap.Instance()->SetFlagMapMarker(AgentMap.Instance()->SelectedTerritoryId, AgentMap.Instance()->SelectedMapId, scaledResult.X, scaledResult.Y);
            System.MapRenderer.PingTexturePosition(textureClickLocation);
            AgentChatLog.Instance()->InsertTextCommandParam(1048, false);
        }

        if (ImGui.MenuItem("Remove Flag", false, AgentMap.Instance()->FlagMarkerCount is not 0)) {
            AgentMap.Instance()->FlagMarkerCount = 0;
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.MenuItem("Center on Player", false, Service.ObjectTable.LocalPlayer is not null) && Service.ObjectTable.LocalPlayer is not null) {
            System.IntegrationsController.OpenOccupiedMap();
            System.MapRenderer.CenterOnGameObject(Service.ObjectTable.LocalPlayer);
        }

        if (ImGui.MenuItem("Center on Map")) {
            System.SystemConfig.FollowPlayer = false;
            System.MapRenderer.DrawOffset = Vector2.Zero;
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.MenuItem("Lock Zoom", "", ref System.SystemConfig.ZoomLocked)) {
            SystemConfig.Save();
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.MenuItem("Open Quest List", false, System.WindowManager.GetWindow<QuestListWindow>() is null)) {
            System.WindowManager.AddWindow(new QuestListWindow(), WindowFlags.OpenImmediately | WindowFlags.RequireLoggedIn);
        }

        if (ImGui.MenuItem("Open Fate List", false, System.WindowManager.GetWindow<FateListWindow>() is null)) {
            System.WindowManager.AddWindow(new FateListWindow(), WindowFlags.OpenImmediately | WindowFlags.RequireLoggedIn);
        }

        if (ImGui.MenuItem("Open Flag List", false, System.WindowManager.GetWindow<FlagHistoryWindow>() is null)) {
            System.WindowManager.AddWindow(new FlagHistoryWindow(), WindowFlags.OpenImmediately | WindowFlags.RequireLoggedIn);
        }
    }
}
