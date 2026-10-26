#if GRATIS_PREVIEW
namespace Gratis.Rendering;
internal static class PopupDiagnostics {
    internal static void Capture(string kind, float radius) { }
}
#else
using System;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;

namespace Gratis.Rendering;

internal static class PopupDiagnostics {
    internal static Action<string>? Pending;

    internal static unsafe void Capture(string kind, float radius) {
        if (Pending is not { } report) return;
        var window = ImGuiP.GetCurrentWindow();
        if (window.Hidden) return;
        Pending = null;
        var viewport = ImGui.GetWindowViewport();
        var style = ImGui.GetStyle();
        var list = ImGui.GetWindowDrawList();
        var line = $"[Gratis popups] kind={kind} name={Marshal.PtrToStringUTF8((nint)window.Name)} "
            + $"flags={window.Flags} (0x{(uint)window.Flags:X}) NoBackground={window.Flags.HasFlag(ImGuiWindowFlags.NoBackground)} "
            + $"viewport={viewport.ID:X} main={ImGui.GetMainViewport().ID:X} secondary={viewport.ID != ImGui.GetMainViewport().ID} "
            + $"owned={window.ViewportOwned} ViewportFlags={viewport.Flags} config={ImGui.GetIO().ConfigFlags} "
            + $"actualRounding={window.WindowRounding:0.##} WindowRounding={style.WindowRounding:0.##} PopupRounding={style.PopupRounding:0.##} "
            + $"PopupBgAlpha={style.Colors[(int)ImGuiCol.PopupBg].W:0.###} WindowBgAlpha={style.Colors[(int)ImGuiCol.WindowBg].W:0.###} "
            + $"clip={list.GetClipRectMin()}..{list.GetClipRectMax()} rect={ImGui.GetWindowPos()}+{ImGui.GetWindowSize()} "
            + $"surfaceRadius={radius:0.##} DrawSurface=none(native-fill)";
        Service.Log.Information(line);
        report(line);
    }
}
#endif
