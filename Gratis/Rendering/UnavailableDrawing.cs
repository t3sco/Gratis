using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class UnavailableDrawing {
    public static void Control(string reason, float? scale = null) {
        if (reason.Length > 0)
            ChromeControls.Tooltip(reason, scale: scale, allowDisabled: true);
    }

    public static void Map(MapAvailability state) {
        var reason = UnavailableReason.Map(state);
        if (reason.Length == 0) return;
        var cursor = ImGui.GetCursorScreenPos();
        var size = ImGui.GetWindowSize();
        var width = global::System.MathF.Max(1, size.X * .65f);
        var height = ImGui.CalcTextSize(reason, false, width).Y;
        ImGui.SetCursorScreenPos(ImGui.GetWindowPos() + (size - new Vector2(width, height)) / 2);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
        ImGui.TextColored(GratisTheme.Secondary, reason);
        ImGui.PopTextWrapPos();
        ImGui.SetCursorScreenPos(cursor);
    }
}
