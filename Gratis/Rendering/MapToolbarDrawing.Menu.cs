using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static partial class MapToolbarDrawing {
    private static void DrawMapMenu(Action<ToolbarAction> activate) {
        var context = MapDrawContext.Current;
        var layers = false;
        using var style = new ChromePopupStyle();
        using var font = context.GetChromeFont(GratisTheme.TextSize * GratisTheme.ChromeScale,
            ChromeFontRole.Body)?.Push();
        var s = GratisTheme.ChromeScale;
        var (min, max) = ChromeLayout.PopoverBounds();
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var height = 2 * ImGui.GetStyle().WindowPadding.Y + ImGui.GetFrameHeight() + 1
            + 4 * GratisTheme.ControlHeight * s + 7 * spacing;
        var size = Vector2.Min(new Vector2(240 * s, MathF.Ceiling(height)), max - min);
        var anchor = LastBounds.Z > LastBounds.X ? LastBounds : ButtonBounds[1];
        var b = ChromePopoverMath.Place(anchor, size, min, max, GratisTheme.Gap * s);
        var popup = UiMotion.Popup(MapMenu, !ImGui.IsKeyPressed(ImGuiKey.Escape, false), new(b.X, b.Y), size,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        if (!popup.Visible)
            return;
        try {
            UiMotion.Scroll();
            MapMenuOpen = popup.Interactive;
            context.Chrome.Reveal(ImGui.GetTime());
            ImGui.SetNextItemWidth(-1);
            if (LensDrawing.DrawSelector(context.Config))
                context.SaveConfig();
            ImGui.Separator();
            if (NavigationItem("Locate player", GratisIcons.LocateFixed)) {
                activate(ToolbarAction.CenterPlayer);
                ImGui.CloseCurrentPopup();
            }
            if (NavigationItem("Centre map", GratisIcons.LayoutGrid)) {
                activate(ToolbarAction.CenterMap);
                ImGui.CloseCurrentPopup();
            }
            if (NavigationItem("Parent map", GratisIcons.ArrowUp)) {
                activate(ToolbarAction.Parent);
                ImGui.CloseCurrentPopup();
            }
            if (NavigationItem("Map layers", GratisIcons.Layers)) {
                layers = true;
                ImGui.CloseCurrentPopup();
            }
        }
        finally { popup.Dispose(); }
        if (layers)
            activate(ToolbarAction.Layers);
    }

    private static bool NavigationItem(string label, char glyph) {
        var s = GratisTheme.ChromeScale;
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(MathF.Max(200 * s, ImGui.GetContentRegionAvail().X), GratisTheme.ControlHeight * s);
        var clicked = ChromeControls.Button("##navigation-" + label, size);
        var list = ImGui.GetWindowDrawList();
        ChromeControls.Feedback(list, pos, pos + size);
        GratisIcons.Draw(list, pos + new Vector2(16 * s, size.Y / 2), glyph, GratisTheme.Secondary,
            GratisTheme.IconSize * s);
        list.AddText(pos + new Vector2(36 * s, (size.Y - ImGui.GetFontSize()) / 2),
            ImGui.GetColorU32(GratisTheme.Primary), label);
        return clicked;
    }
}