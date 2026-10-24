using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Gratis.Rendering;
using KamiLib.Classes;

namespace Gratis.Windows;

public partial class StyleOptionsTab
{
    private static bool DrawFramePresetSection()
    {
        var config = System.SystemConfig;
        if (!config.IsGratis) return false;
        ImGuiTweaks.Header("Frame presets");
        using var indent = ImRaii.PushIndent();
        var preset = (int)config.FramePreset;
        var changed = ImGui.Combo("Frame style", ref preset, "Hairline\0Double ring\0Inset glow\0Notched corners\0Soft shadow\0None\0");
        if (changed) config.FramePreset = (FramePreset)preset;
        ImGui.TextDisabled("Frame stays visible when chrome hides. Glow and brackets use the accent colour.");
        ImGui.TextDisabled(config.FramePreset == FramePreset.None
            ? "None hides the frame and ring; edge feathering still applies."
            : "Ring and edge feathering can be adjusted independently below.");
        if (ImGui.Button("Reset Frame Preset")) {
            config.ResetFramePreset();
            changed = true;
        }
        return changed;
    }
}
