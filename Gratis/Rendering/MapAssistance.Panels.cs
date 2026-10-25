using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class MapAssistance {
    internal static Vector2 ConstrainPanel(Vector2 position, Vector2 size) {
        var (min, max) = Classes.MapShape.Bounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        if (MapDrawContext.Current.Config.MapCircle) {
            var inset = (max - min) * .14644661f;
            min += inset;
            max -= inset;
        }
        var margin = new Vector2(GratisTheme.Inset * GratisTheme.ChromeScale);
        return Vector2.Clamp(position, min + margin, Vector2.Max(min + margin, max - size - margin));
    }

    internal void Panel(string id, Vector2 pos, Vector2 size, Action content, bool scroll = false,
        bool interactive = true, Vector2? pivot = null) {
        if (interactive)
            panelBounds.Add((pos, pos + size));
        var alpha = UiMotion.Visibility(ImGui.GetID(id), interactive);
        var s = GratisTheme.ChromeScale;
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push();
        var cursor = ImGui.GetCursorPos();
        ImGui.SetCursorScreenPos(pos);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, GratisTheme.CardRadius * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, GratisTheme.ButtonRadius * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 4) * s);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.25f, .48f, .56f, .26f));
        ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Primary);
        try {
            var flags = ImGuiWindowFlags.NoBackground
                | ImGuiWindowFlags.NoScrollWithMouse
                | (scroll ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoScrollbar)
                | (interactive ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoInputs);
#if GRATIS_PREVIEW
            var visible = ImGui.BeginChild(id, size, ImGuiChildFlags.AlwaysUseWindowPadding, flags);
#else
            var visible = ImGui.BeginChild(id, size, false, flags | ImGuiWindowFlags.AlwaysUseWindowPadding);
#endif
            if (visible) {
                using var motion = new UiMotion.VisualScope(ImGui.GetWindowDrawList(), pivot ?? pos, alpha,
                    UiMotion.Full ? new Vector2(0, (1 - alpha) * 6 * s) : Vector2.Zero,
                    UiMotion.Full ? .97f + .03f * alpha : 1);
                if (scroll)
                    UiMotion.Scroll();
                ImGui.BeginDisabled(!interactive);
                MapWindowDrawing.DrawSurface(ImGui.GetWindowDrawList(), pos, pos + size, GratisTheme.CardRadius * s,
                    surfaceOpacity: 1);
                content();
                ImGui.EndDisabled();
                if (interactive && (ImGui.IsWindowHovered() || ImGui.IsWindowFocused() && ImGui.IsAnyItemActive())) {
                    InputActive = true;
                    MapDrawContext.Current.Chrome.Reveal(ImGui.GetTime());
                }
            }
            ImGui.EndChild();
            ImGui.SetCursorPos(cursor);
            ImGui.Dummy(Vector2.Zero);
        }
        finally { ImGui.PopStyleColor(3); ImGui.PopStyleVar(4); }
    }
}