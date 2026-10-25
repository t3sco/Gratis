using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

internal readonly struct ChromePopupStyle : IDisposable
{
    public ChromePopupStyle()
    {
        var s = GratisTheme.ChromeScale;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, GratisTheme.CardRadius * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 7) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 4) * s);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, GratisTheme.Surface with { W = 1 });
        ImGui.PushStyleColor(ImGuiCol.Border, GratisTheme.Border);
        ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Primary);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, GratisTheme.Secondary);
    }
    public void Dispose() { ImGui.PopStyleColor(4); ImGui.PopStyleVar(4); }
}
