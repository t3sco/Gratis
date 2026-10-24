using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Gratis.Rendering;
using KamiLib.Classes;

namespace Gratis.Windows;

public partial class MapFunctionsTab {
    private static bool DrawMapColourSection() {
        ImGuiTweaks.Header("Map Colour");
        using var indent = ImRaii.PushIndent();
        var settings = System.SystemConfig.MapColour ??= new MapColourSettings();
        var preset = (int)settings.Preset;
        var changed = ImGui.Combo("Preset##MapColour", ref preset, "Original\0Midnight\0Mono\0Blueprint\0Sepia\0Custom\0");
        if (changed) settings.Preset = (MapColourPreset)preset;
        if (settings.Preset == MapColourPreset.Custom) {
            changed |= ImGui.SliderFloat("Brightness", ref settings.Brightness, -1, 1);
            changed |= ImGui.SliderFloat("Contrast", ref settings.Contrast, 0, 3);
            changed |= ImGui.SliderFloat("Saturation", ref settings.Saturation, 0, 2);
            changed |= ImGui.SliderFloat("Hue shift", ref settings.HueShift, -180, 180, "%.0f degrees");
            changed |= ImGui.ColorEdit3("Tint", ref settings.Tint);
            changed |= ImGui.SliderFloat("Tint strength", ref settings.TintStrength, 0, 1);
            changed |= ImGui.Checkbox("Gradient map", ref settings.GradientEnabled);
            using (ImRaii.Disabled(!settings.GradientEnabled)) {
                changed |= ImGui.ColorEdit3("Shadow", ref settings.Shadow);
                changed |= ImGui.ColorEdit3("Midtone", ref settings.Mid);
                changed |= ImGui.ColorEdit3("Highlight", ref settings.Highlight);
            }
        }
        ImGui.TextDisabled("The map previews changes as they are ready.");
        if (ImGui.Button("Reset##MapColour")) {
            System.SystemConfig.MapColour = new MapColourSettings();
            changed = true;
        }
        return changed;
    }
}