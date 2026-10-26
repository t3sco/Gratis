using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static partial class ChromeControls {
    public static bool TextButton(string label, Vector2 size = default, float? scale = null, float? radius = null) {
        if (!UiMotion.IsGratis)
            return ImGui.Button(label, size);
        var s = scale ?? GratisTheme.ChromeScale;
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, radius ?? GratisTheme.Radius.Control * s);
        var list = ImGui.GetWindowDrawList();
        var first = list.VtxBuffer.Size;
        var min = ImGui.GetCursorScreenPos();
        var fill = ImGui.GetStyle().Colors[(int)ImGuiCol.Button];
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, fill);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, fill);
        var clicked = ImGui.Button(label, size);
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar();
        UiMotion.ItemId(label);
        var max = ImGui.GetItemRectMax();
        using var press = UiMotion.Control(list, min, max, first);
        Feedback(list, min, max, scale: s, radius: radius);
        return clicked;
    }

    public static bool Combo(string id, ref int selected, string[] items, float? scale = null) {
        if (!UiMotion.IsGratis)
            return ImGui.Combo(id, ref selected, items, items.Length);
        selected = Math.Clamp(selected, 0, Math.Max(0, items.Length - 1));
        var s = scale ?? GratisTheme.ChromeScale;
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(MathF.Max(40 * s, ImGui.CalcItemWidth()), GratisTheme.ControlHeight * s);
        if (Button(id, size))
            ImGui.OpenPopup(id);
        var list = ImGui.GetWindowDrawList();
        using (var control = UiMotion.Control(list, pos, pos + size)) {
            list.AddRectFilled(pos, pos + size, ImGui.GetColorU32(new Vector4(1, 1, 1, .035f)),
                GratisTheme.Radius.Control * s);
            list.AddRect(pos, pos + size, ImGui.GetColorU32(GratisTheme.Border), GratisTheme.Radius.Control * s);
            Feedback(list, pos, pos + size, scale: s);
            var label = items.Length == 0 ? "" : FitLabel(items[selected], size.X - 38 * s);
            list.AddText(pos + new Vector2(10 * s, (size.Y - ImGui.GetFontSize()) / 2),
                ImGui.GetColorU32(GratisTheme.Primary), label);
            var center = pos + new Vector2(size.X - 15 * s, size.Y / 2);
            list.AddTriangleFilled(center + new Vector2(-4, -2) * s, center + new Vector2(4, -2) * s,
                center + new Vector2(0, 3) * s, ImGui.GetColorU32(GratisTheme.Secondary));
        }
        var viewport = ImGui.GetWindowViewport();
        var menuSize = new Vector2(size.X,
            MathF.Min(MathF.Min(items.Length, 8) * GratisTheme.ControlHeight * s + 16 * s, viewport.Size.Y - 28 * s));
        var bounds = ChromePopoverMath.Place(new(pos.X, pos.Y, pos.X + size.X, pos.Y + size.Y), menuSize,
            viewport.Pos + new Vector2(14 * s), viewport.Pos + viewport.Size - new Vector2(14 * s), 4 * s);
        using var style = new ChromePopupStyle(scale: s);
        using var popup = UiMotion.Popup(id, !ImGui.IsKeyPressed(ImGuiKey.Escape, false), new(bounds.X, bounds.Y),
            menuSize, ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings,
            scale: s);
        if (!popup.Visible)
            return false;
        UiMotion.Scroll();
        var changed = false;
        for (var i = 0; i < items.Length; i++) {
            ImGui.PushID(i);
            if (Selectable(items[i], selected == i, new(0, GratisTheme.ControlHeight * s), s)) {
                selected = i;
                changed = true;
                ImGui.CloseCurrentPopup();
            }
            if (selected == i && ImGui.IsWindowAppearing())
                UiMotion.ScrollToItem();
            ImGui.PopID();
        }
        return changed;
    }
}