using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using KamiLib.Classes;

namespace Gratis.Windows;

public partial class MapFunctionsTab
{
    private static bool DrawMotionSection()
    {
        ImGuiTweaks.Header("Map Motion (Gratis)");
        using var indent = ImRaii.PushIndent();
        var config = System.SystemConfig;
        var settings = config.Motion;
        var changed = ImGui.Checkbox("Reduce Motion", ref config.ReduceMotion);
        using (ImRaii.Disabled(config.ReduceMotion)) {
            changed |= ImGui.Checkbox("Flag / quest / FATE / locate pings", ref settings.Pings);
            changed |= ImGui.SliderFloat("Ping duration", ref settings.PingSeconds, .3f, 1.5f, "%.2f s");
            changed |= ImGui.Checkbox("Player trail", ref settings.PlayerTrail);
            changed |= ImGui.SliderFloat("Trail duration (0 = off)", ref settings.TrailSeconds, 0, 10, "%.1f s");
            changed |= ImGui.SliderFloat("Trail opacity", ref settings.TrailOpacity, .05f, .5f, "%.2f");
            changed |= ImGui.Checkbox("Ease player follow", ref settings.EaseFollow);
            changed |= ImGui.Checkbox("Ease wheel zoom", ref settings.EaseZoom);
            changed |= ImGui.SliderFloat("Pan / zoom settle time", ref settings.SettleSeconds, .05f, .3f, "%.2f s");
            changed |= ImGui.Checkbox("Zone change fade / slide", ref settings.ZoneTransition);
            changed |= ImGui.SliderFloat("Zone transition duration", ref settings.ZoneSeconds, .08f, .4f, "%.2f s");
            changed |= ImGui.SliderFloat("Zone slide distance", ref settings.ZoneSlidePixels, 0, 16, "%.0f px");
        }
        if (ImGui.Button("Reset Map Motion")) { config.ResetMotion(); changed = true; }
        return changed;
    }
}
