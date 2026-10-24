using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

internal static class DrawBinding
{
    public static Vector2 MeasureText(float size, string text, float wrap = 0) =>
        ImGui.CalcTextSize(text, false, wrap > 0 ? wrap * ImGui.GetFontSize() / size : 0) * (size / ImGui.GetFontSize());

    public static Vector2 ContentRegionMax() =>
#if GRATIS_PREVIEW
        ImGui.GetCursorPos() + ImGui.GetContentRegionAvail();
#else
        ImGui.GetContentRegionMax();
#endif

    public static uint VertexIndex(ImDrawListPtr list) =>
#if GRATIS_PREVIEW
        list._VtxCurrentIdx;
#else
        list.VtxCurrentIdx;
#endif
}
