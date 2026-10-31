using System;
using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Windows;

public static class SettingsQuickSetupDrawing {
    /// <summary>The settings row for replaying Quick setup. It stores nothing; the flow itself sets the flag.</summary>
    public static bool Draw(SystemConfig config) {
        var scale = config.IsGratis ? SettingsTheme.Scale : (float?)null;
        var width = MathF.Max(1, ImGui.GetContentRegionAvail().X);
        if (ChromeControls.TextButton("Start", new Vector2(width, 0), scale: scale))
            Run(config);
        return false;
    }

    /// <summary>Starts Quick setup from the first step. Shared by the Quick setup row and the Essentials button.</summary>
    public static void Run(SystemConfig config) => MapDrawContext.Current.QuickSetup.Replay(config);
}
