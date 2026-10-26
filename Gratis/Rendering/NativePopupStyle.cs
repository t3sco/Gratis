using System;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

internal readonly struct NativePopupStyle : IDisposable {
    internal NativePopupStyle(float scale) {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, GratisTheme.Radius.Popup * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, GratisTheme.Radius.Popup * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 0);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, GratisTheme.Surface with { W = 1 });
        ImGui.PushStyleColor(ImGuiCol.WindowBg, GratisTheme.Surface with { W = 1 });
    }

    public void Dispose() {
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(4);
    }
}
