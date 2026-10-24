using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using KamiLib.Classes;
using Gratis.Rendering;

namespace Gratis.Windows;

public partial class MapFunctionsTab
{
    private static bool DrawMarkersSection()
    {
        ImGuiTweaks.Header("Markers");
        using var indent = ImRaii.PushIndent();
        var settings = System.SystemConfig.Markers;
        var iconStyle = (int)settings.IconStyle;
        var changed = ImGui.Combo("Marker icons (Gratis)", ref iconStyle, "Icons\0Shapes\0Game\0");
        if (changed) { settings.IconStyle = (MarkerIconStyle)iconStyle; System.SystemConfig.MarkerStyle = 1; }
        changed |= ImGui.Checkbox("Cluster ambient POIs / services (Gratis)", ref settings.Declutter);
        ImGui.TextDisabled("Quests, events, transport, players and tracked / hovered markers stay visible.");
        changed |= ImGui.Checkbox("Zoom-aware marker size (Gratis)", ref settings.ScaleWithZoom);
        changed |= ImGui.SliderFloat("Minimum marker scale", ref settings.MinimumScale, .3f, 1, "%.2fx");
        changed |= ImGui.SliderFloat("Maximum marker scale", ref settings.MaximumScale, 1, 3, "%.2fx");
        changed |= ImGui.SliderFloat("Cluster distance", ref settings.ClusterDistance, 8, 64, "%.0f px");
        changed |= ImGui.Checkbox("Dim irrelevant ambient markers", ref settings.DimIrrelevant);
        changed |= ImGui.SliderFloat("Irrelevant opacity", ref settings.IrrelevantAlpha, .1f, 1, "%.0f");
        if (ImGui.Button("Reset Markers")) { System.SystemConfig.ResetMarkers(); System.SystemConfig.MarkerStyle = 1; changed = true; }
        return changed;
    }
}
