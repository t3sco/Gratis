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
        var changed = ImGui.Checkbox("Declutter (Gratis style)", ref settings.Declutter);
        changed |= ImGui.Checkbox("Zoom-aware marker size (Gratis)", ref settings.ScaleWithZoom);
        changed |= ImGui.SliderFloat("Minimum marker scale", ref settings.MinimumScale, .3f, 1, "%.2fx");
        changed |= ImGui.SliderFloat("Maximum marker scale", ref settings.MaximumScale, 1, 3, "%.2fx");
        changed |= ImGui.Checkbox("Hide low-priority markers at high density", ref settings.HideLowPriority);
        changed |= ImGui.SliderFloat("Cluster distance", ref settings.ClusterDistance, 8, 64, "%.0f px");
        changed |= ImGui.Checkbox("Dim irrelevant / distant markers", ref settings.DimIrrelevant);
        changed |= ImGui.SliderFloat("Irrelevant opacity", ref settings.IrrelevantAlpha, .1f, 1, "%.0f");
        ImGui.TextUnformatted("Priority (highest first)");
        for (var i = 0; i < settings.Priority.Length; i++) {
            ImGui.PushID(i);
            ImGui.TextUnformatted(settings.Priority[i] switch {
                MarkerPriority.Objectives => "Quest / FATE / target",
                MarkerPriority.Transport => "Aetheryte / exits",
                _ => "Ambient / services / players",
            });
            if (i > 0) {
                ImGui.SameLine();
                if (ImGui.SmallButton("Move up")) {
                    (settings.Priority[i - 1], settings.Priority[i]) = (settings.Priority[i], settings.Priority[i - 1]);
                    changed = true;
                }
            }
            ImGui.PopID();
        }
        if (ImGui.Button("Reset Markers")) { System.SystemConfig.ResetMarkers(); changed = true; }
        return changed;
    }
}
