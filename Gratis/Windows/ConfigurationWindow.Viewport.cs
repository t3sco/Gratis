using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using KamiLib.Classes;

namespace Gratis.Windows;

public partial class MapFunctionsTab
{
    private static bool DrawViewportSection()
    {
        ImGuiTweaks.Header("Map Viewport");
        using var indent = ImRaii.PushIndent();
        var config = System.SystemConfig;
        var changed = ImGui.Checkbox("Scale with frame", ref config.ScaleWithFrame);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Resize using the shorter frame side (circle diameter). Your zoom is preserved; wider frames reveal more on the long side.");
        changed |= ImGui.Checkbox("Clamp to map", ref config.ClampToMap);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Keep the frame inside the map texture, including while following the player near an edge.");
        if (ImGui.Button("Reset Viewport Defaults")) {
            config.ResetViewport();
            changed = true;
        }
        return changed;
    }
}
