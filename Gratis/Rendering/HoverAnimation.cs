#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

internal static class HoverAnimation {
    public static float Progress(string key, bool hovered) =>
        UiMotion.Value(ImGui.GetID(key), 8, hovered ? 1 : 0, InterfaceMotionTokens.Micro, initial: 0);
}