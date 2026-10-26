using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static partial class ChromeControls {
    public static bool Selectable(string label, bool selected = false, Vector2 size = default, float? scale = null) {
        if (!UiMotion.IsGratis)
            return ImGui.Selectable(label, selected, ImGuiSelectableFlags.None, size);
        using var row = new NativeRow(scale);
        var clicked = ImGui.Selectable(label, selected, ImGuiSelectableFlags.None, size);
        row.Draw(selected);
        return clicked;
    }

    public static bool MenuItem(string label, string shortcut = "", bool selected = false, bool enabled = true) {
        if (!UiMotion.IsGratis)
            return ImGui.MenuItem(label, shortcut, selected, enabled);
        using var row = new NativeRow(null);
        var clicked = ImGui.MenuItem(label, shortcut, selected, enabled);
        row.Draw(selected);
        return clicked;
    }

    private readonly struct NativeRow : IDisposable {
        private readonly ImDrawListPtr list;
        private readonly Vector4 normal, hovered, active;
        private readonly float scale;
        public NativeRow(float? scale) {
            this.scale = scale ?? GratisTheme.ChromeScale;
            list = ImGui.GetWindowDrawList();
            var colors = ImGui.GetStyle().Colors;
            normal = colors[(int)ImGuiCol.Header];
            hovered = colors[(int)ImGuiCol.HeaderHovered];
            active = colors[(int)ImGuiCol.HeaderActive];
            ImGui.PushStyleColor(ImGuiCol.Header, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, Vector4.Zero);
            list.ChannelsSplit(2);
            list.ChannelsSetCurrent(1);
        }
        public void Draw(bool selected) {
            if (!ImGui.IsItemVisible())
                return;
            var focused = ImGui.IsItemFocused() && ImGui.GetIO().NavVisible;
            var color = ImGui.IsItemActive() ? active : ImGui.IsItemHovered() || focused ? hovered
                : selected ? normal : Vector4.Zero;
            list.ChannelsSetCurrent(0);
            list.AddRectFilled(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetColorU32(color),
                GratisTheme.Radius.Control * scale);
        }
        public void Dispose() {
            list.ChannelsMerge();
            ImGui.PopStyleColor(3);
        }
    }
}
