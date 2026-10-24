using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using KamiLib.Classes;

namespace Gratis.Windows;

public partial class StyleOptionsTab
{
    private static bool DrawChromeVisibilitySection()
    {
        var config = System.SystemConfig;
        if (!config.IsGratis) return false;
        ImGuiTweaks.Header("Chrome visibility");
        using var indent = ImRaii.PushIndent();
        var changed = ImGui.Checkbox("Auto-hide chrome", ref config.AutoHideChrome);
        changed |= ImGui.Checkbox("Pin chrome", ref config.PinChrome);
        changed |= ImGui.SliderFloat("Idle delay", ref config.ChromeIdleDelay, 0, 10, "%.1f s");
        changed |= ImGui.SliderFloat("Fade duration", ref config.ChromeFadeDurationMs, 0, 2000, "%.0f ms");
        changed |= ImGui.SliderFloat("Minimum title alpha", ref config.ChromeTitleMinAlpha, 0, 1, "%.2f");
        changed |= ImGui.SliderFloat("Minimum toolbar / coordinates alpha", ref config.ChromeControlsMinAlpha, 0, 1, "%.2f");
        ImGui.TextDisabled("Hover or interact to reveal. Pin keeps chrome visible.");
        ImGui.TextDisabled("Auto-hide keeps toolbar buttons available; hover reveals before clicks.");
        if (ImGui.Button("Reset Chrome Visibility")) {
            config.ResetChromeVisibility();
            changed = true;
        }
        return changed;
    }
}
