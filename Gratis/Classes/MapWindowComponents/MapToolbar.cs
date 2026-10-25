using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.Interop;
using Gratis.Compat.Window;
using Lumina.Excel.Sheets;
using Gratis.Windows;
using Gratis.Rendering;

namespace Gratis.Classes.MapWindowComponents;

public unsafe class MapToolbar {
    public void Draw() => Rendering.MapToolbarDrawing.Draw(Activate, DrawLayersContextMenu,
        System.MapWindow.PlacePalette.Overlay);

    private static void Activate(Rendering.ToolbarAction action) {
        switch (action) {
            case ToolbarAction.Parent: {
                    using var valueArgs = new RentedAtkValues(1);
                    valueArgs[0].SetInt(5);

                    using var returnValue = new RentedAtkValues(1);
                    AgentMap.Instance()->ReceiveEvent(returnValue, valueArgs, 1, 0);
                    break;
                }
            case ToolbarAction.Layers: {
                    ImGui.OpenPopup("Gratis_Show_Layers");
                    break;
                }
            case ToolbarAction.Follow: {
                    System.SystemConfig.FollowPlayer = !System.SystemConfig.FollowPlayer;

                    if (System.SystemConfig.FollowPlayer) {
                        System.IntegrationsController.OpenOccupiedMap();
                    }
                    break;
                }
            case ToolbarAction.CenterPlayer: {
                    if (Service.ObjectTable.LocalPlayer is null)
                        return;
                    System.MapRenderer.LocatePlayer();

                    if (!System.SystemConfig.FollowPlayer) {
                        System.IntegrationsController.OpenOccupiedMap();
                        System.MapRenderer.CenterOnGameObject(Service.ObjectTable.LocalPlayer);
                    }
                    break;
                }
            case ToolbarAction.CenterMap: {
                    System.SystemConfig.FollowPlayer = false;
                    System.MapRenderer.DrawOffset = Vector2.Zero;
                    break;
                }
            case ToolbarAction.Search: {
                    if (System.SystemConfig.IsGratis && System.SystemConfig.Palette) {
                        System.MapWindow.OpenPalette();
                        break;
                    }
                    System.WindowManager.AddWindow(new MapSelectionWindow {
                        SingleSelectionCallback = selection => {
                            if (selection?.Map != null) {
                                if (AgentMap.Instance()->SelectedMapId != selection.Map.RowId) {
                                    System.IntegrationsController.OpenMap(selection.Map.RowId);
                                }

                                if (selection.MarkerLocation is { } location) {
                                    System.SystemConfig.FollowPlayer = false;
                                    System.MapRenderer.DrawOffset = -location + DrawHelpers.GetMapCenterOffsetVector();
                                }
                            }
                        },
                    }, WindowFlags.OpenImmediately | WindowFlags.RequireLoggedIn);
                    break;
                }
            case ToolbarAction.Settings: {
                    System.ConfigWindow.UnCollapseOrShow();
                    ImGui.SetWindowFocus(System.ConfigWindow.WindowName);
                    break;
                }
            case ToolbarAction.Close: {
                    System.MapWindow.Close();
                    break;
                }
        }
    }

    private void DrawLayersContextMenu() {
        using var contextMenu = ImRaii.Popup("Gratis_Show_Layers");
        if (!contextMenu)
            return;

        var currentMap = Service.DataManager.GetExcelSheet<Map>().GetRow(AgentMap.Instance()->SelectedMapId);
        if (currentMap.RowId is 0)
            return;

        if (currentMap.MapType.RowId == 3) {
            foreach (var marker in AgentMap.Instance()->MapMarkers) {
                if (!DrawHelpers.IsRegionIcon(marker.MapMarker.IconId))
                    continue;

                var label = marker.MapMarker.Subtext.AsDalamudSeString();

                if (ImGui.MenuItem(label.ToString())) {
                    System.IntegrationsController.OpenMap(marker.DataKey);
                    System.SystemConfig.FollowPlayer = false;
                    System.MapRenderer.DrawOffset = Vector2.Zero;
                }
            }
        }

        else {
            var layers = Service.DataManager.GetExcelSheet<Map>().Where(eachMap =>
                eachMap.PlaceName.RowId == currentMap.PlaceName.RowId).Where(eachMap =>
                eachMap.MapIndex != 0).OrderBy(eachMap => eachMap.MapIndex).ToList();

            if (layers.Count is 0) {
                ImGui.Text("No layers for this map");
            }

            foreach (var layer in layers) {
                if (ImGui.MenuItem(layer.PlaceNameSub.Value.Name.ExtractText(), "",
                    AgentMap.Instance()->SelectedMapId == layer.RowId)) {
                    System.IntegrationsController.OpenMap(layer.RowId);
                    System.SystemConfig.FollowPlayer = false;
                    System.MapRenderer.DrawOffset = Vector2.Zero;
                }
            }
        }
    }
}