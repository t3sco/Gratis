using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

internal readonly struct ChromePopupStyle : IDisposable {
    private readonly bool gratis;
    public ChromePopupStyle() : this(false, null) { }
    public ChromePopupStyle(bool transparent = false, float? scale = null) {
        gratis = UiMotion.IsGratis;
        var s = gratis ? scale ?? GratisTheme.ChromeScale : GratisTheme.ChromeScale;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, GratisTheme.Radius.Popup * s);
        if (gratis) {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, GratisTheme.Radius.Popup * s);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, GratisTheme.Radius.Card * s);
            ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, GratisTheme.Radius.Inner(GratisTheme.Radius.Control * s, 2 * s));
            ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, GratisTheme.Radius.Pill(new(ImGui.GetStyle().ScrollbarSize)));
        }
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, GratisTheme.Radius.Control * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 7) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 4) * s);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, transparent ? Vector4.Zero : GratisTheme.Surface with { W = 1 });
        ImGui.PushStyleColor(ImGuiCol.WindowBg, transparent ? Vector4.Zero : GratisTheme.Surface with { W = 1 });
        ImGui.PushStyleColor(ImGuiCol.Border, GratisTheme.Border);
        ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Primary);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, GratisTheme.Secondary);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(1, 1, 1, .04f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(1, 1, 1, .08f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(1, 1, 1, .12f));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(1, 1, 1, .04f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(1, 1, 1, .08f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(1, 1, 1, .12f));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1, 1, 1, .04f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1, 1, 1, .08f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1, 1, 1, .12f));
    }

    public void Dispose() {
        ImGui.PopStyleColor(14);
        ImGui.PopStyleVar(gratis ? 11 : 6);
    }
}
