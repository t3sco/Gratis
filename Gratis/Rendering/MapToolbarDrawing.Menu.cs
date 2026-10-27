using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static partial class MapToolbarDrawing {
    private static readonly MenuRowCache[] NavigationRows = [
        new("##navigation-Locate player"), new("##navigation-Centre map"),
        new("##navigation-Parent map"), new("##navigation-Map layers"),
    ];

    private static void DrawMapMenu(Action<ToolbarAction> activate) {
        var context = MapDrawContext.Current;
        var layers = false;
        using var style = PopupSurface.Push(PopupFaces.Chrome);
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
            if (NavigationItem("Locate player", GratisIcons.LocateFixed, NavigationRows[0])) {
                activate(ToolbarAction.CenterPlayer);
                ImGui.CloseCurrentPopup();
            }
            if (NavigationItem("Centre map", GratisIcons.LayoutGrid, NavigationRows[1])) {
                activate(ToolbarAction.CenterMap);
                ImGui.CloseCurrentPopup();
            }
            if (NavigationItem("Parent map", GratisIcons.ArrowUp, NavigationRows[2])) {
                activate(ToolbarAction.Parent);
                ImGui.CloseCurrentPopup();
            }
            if (NavigationItem("Map layers", GratisIcons.Layers, NavigationRows[3])) {
                layers = true;
                ImGui.CloseCurrentPopup();
            }
        }
        finally { popup.Dispose(); }
        if (layers)
            activate(ToolbarAction.Layers);
    }

    private static bool NavigationItem(string label, char glyph, MenuRowCache cache) {
        var row = new MenuRow(cache, new(RowFaces.Navigation, GratisTheme.ChromeScale, Icon: glyph));
        row.Draw(label);
        return row.Clicked;
    }
}
