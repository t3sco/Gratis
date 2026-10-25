using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif
using Gratis.Classes;

namespace Gratis.Rendering;

public static class MapToolbarDrawing
{
    public static Vector4 LastBounds { get; private set; }
    public static ChromeAnchor EffectiveAnchor { get; private set; }
    public static bool MapMenuOpen { get; private set; }
    public static void Draw(Action<ToolbarAction> activate, Action drawLayersContextMenu) {
        LastBounds = Vector4.Zero;
        MapMenuOpen = false;
        var context = MapDrawContext.Current;
        using var chromeAlpha = new ChromeVisibility.AlphaScope(context.Config.IsGratis, context.Chrome.ControlsAlpha(context.Config));
        if (!chromeAlpha.Interactive) return;
        if (context.Config.IsGratis) {
            DrawSegmented(activate, drawLayersContextMenu);
            return;
        }
        var borderPadding = 2.5f;
        var toolbarSize = new Vector2(DrawBinding.ContentRegionMax().X, 33.0f * context.GlobalScale);

        var surfaceList = ImGui.GetWindowDrawList();
        var surfaceOrigin = ImGui.GetCursorScreenPos();
        LastBounds = new(surfaceOrigin.X, surfaceOrigin.Y, surfaceOrigin.X + toolbarSize.X, surfaceOrigin.Y + toolbarSize.Y);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, context.Config.IsGratis ? Vector4.Zero : Vector4.Zero with { W = context.Config.ToolbarFade });
        var visible = ImGui.BeginChild("toolbar_child", toolbarSize);
        try {
            if (!visible) return;

            if (context.Config.IsGratis) MapWindowDrawing.DrawSurface(surfaceList, surfaceOrigin, surfaceOrigin + toolbarSize,
                GratisTheme.CardRadius * context.GlobalScale, surface: GlassSurface.Toolbar);

            var cornerInset = MapShape.Current * 0.29f;
            ImGui.SetCursorPos(new Vector2(borderPadding, borderPadding) * context.GlobalScale + new Vector2(cornerInset));

            if (context.IconButton(ToolbarAction.Parent, "up", "Open Parent Map")) {
                activate(ToolbarAction.Parent);
            }

            ImGui.SameLine();

            if (context.IconButton(ToolbarAction.Layers, "layers", "Show Map Layers")) {
                activate(ToolbarAction.Layers);
            }

            if (context.Config.IsGratis && ImGui.IsPopupOpen("Gratis_Show_Layers")) context.Chrome.Reveal(ImGui.GetTime());
            drawLayersContextMenu();

            ImGui.SameLine();

            var following = context.Config.FollowPlayer;
            if (following) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]);
            if (context.IconButton(ToolbarAction.Follow, "follow", "Toggle Follow Player")) {
                activate(ToolbarAction.Follow);
            }
            if (following) ImGui.PopStyleColor();

            ImGui.SameLine();

            if (context.IconButton(ToolbarAction.CenterPlayer, "centerPlayer", "Center on Player")) {
                activate(ToolbarAction.CenterPlayer);
            }

            ImGui.SameLine();

            if (context.IconButton(ToolbarAction.CenterMap, "centerMap", "Center on Map")) {
                activate(ToolbarAction.CenterMap);
            }

            ImGui.SameLine();

            if (context.IconButton(ToolbarAction.Search, "search", "Search for Map")) {
                activate(ToolbarAction.Search);
            }

            var buttonWidth = context.SettingsButtonWidth;

            var buttonCount = context.Config.HideWindowFrame ? 2 : 1;
            var rightWidth = buttonWidth * buttonCount + ImGui.GetStyle().ItemSpacing.X * (buttonCount - 1);
            ImGui.SameLine();
            ImGui.SetCursorPosX(DrawBinding.ContentRegionMax().X - rightWidth - context.GlobalScale * borderPadding - cornerInset);

            if (context.IconButton(ToolbarAction.Settings, "settings", "Open Settings")) {
                activate(ToolbarAction.Settings);
            }

            if (!context.Config.HideWindowFrame) return;

            ImGui.SameLine();

            if (context.IconButton(ToolbarAction.Close, "closeMap", "Close Map")) {
                activate(ToolbarAction.Close);
            }
        }
        finally {
            ImGui.EndChild();
            ImGui.PopStyleColor();
        }
    }

    public const int FullButtonCount = 6;
    private const string MapMenu = "Gratis_Map_Controls";
    private static readonly (string Id, ToolbarAction Action, string Tooltip)[] Buttons = [
        ("Search", ToolbarAction.Search, "Search places"), ("Lens", ToolbarAction.Layers, "Lenses & map layers"),
        ("Filters", ToolbarAction.CenterMap, "Filters"), ("Follow", ToolbarAction.Follow, "Follow player"),
        ("Settings", ToolbarAction.Settings, "Settings"), ("Close", ToolbarAction.Close, "Close map"),
    ];

    private static void DrawSegmented(Action<ToolbarAction> activate, Action drawLayersContextMenu)
    {
        var context = MapDrawContext.Current;
        var scale = GratisTheme.ChromeScale;
        var (areaMin, areaMax) = MapShape.Bounds(Vector2.Zero, ImGui.GetWindowSize());
        var area = areaMax - areaMin;
        var (safeMin, safeMax) = ChromeLayout.SafeBounds();
        var available = safeMax.X - safeMin.X;
        var fullCount = context.Config.UseCustomWindowChrome ? FullButtonCount : FullButtonCount - 1;
        var fullWidth = (fullCount * GratisTheme.ControlHeight + 8) * scale;
        var centerAllowance = 2 * (GratisTheme.ToolbarCenterMargin - GratisTheme.Inset) * scale;
        EffectiveAnchor = context.Config.ToolbarAnchor == ChromeAnchor.TopCenter && available < fullWidth + centerAllowance
            ? ChromeAnchor.TopLeft : context.Config.ToolbarAnchor;
        var compact = available < fullWidth || area.Y < 280 * scale;
        var count = compact ? (context.Config.UseCustomWindowChrome ? 3 : 2) : fullCount;
        if (available < (count * GratisTheme.ControlHeight + 8) * scale) count = 2;
        var width = context.Config.SegmentedToolbar ? (count * GratisTheme.ControlHeight + 8) * scale : available;
        if (!context.Config.SegmentedToolbar && EffectiveAnchor == ChromeAnchor.TopCenter)
            width -= centerAllowance;
        var height = GratisTheme.ToolbarHeight * scale;
        var bounds = ChromeLayout.Place("toolbar", new Vector2(width, height), EffectiveAnchor);
        if (bounds == Vector4.Zero) return;
        var surfaceList = ImGui.GetWindowDrawList();
        var surfaceOrigin = ChromeLayout.Position(bounds);
        ImGui.SetCursorScreenPos(surfaceOrigin); ImGui.Dummy(Vector2.Zero);
        ImGui.SetCursorScreenPos(surfaceOrigin);
        LastBounds = bounds;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        var visible = ImGui.BeginChild("toolbar_child", new Vector2(width, height));
        try {
            if (!visible) return;
            var origin = ImGui.GetWindowPos();
            var list = ImGui.GetWindowDrawList();
            MapWindowDrawing.DrawSurface(surfaceList, surfaceOrigin, surfaceOrigin + new Vector2(width, height), GratisTheme.CardRadius * scale,
                surface: GlassSurface.Toolbar);
            for (var index = 0; index < count; index++) {
                var buttonIndex = compact || count == 2 ? index == 0 ? 0 : index + 3 : index;
                var (id, action, tooltip) = Buttons[buttonIndex];
                var buttonX = !context.Config.SegmentedToolbar && index >= count - (context.Config.UseCustomWindowChrome ? 2 : 1)
                    ? width / scale - 4 - (count - index) * GratisTheme.ControlHeight : 4 + index * GratisTheme.ControlHeight;
                var min = origin + new Vector2(buttonX, 4) * scale;
                var active = action == ToolbarAction.Follow && context.Config.FollowPlayer || id == "Lens" &&
                    (ImGui.IsPopupOpen(MapMenu) || ImGui.IsPopupOpen("Gratis_Show_Layers")) || id == "Filters" && FilterBarDrawing.PopoverOpen;
                ImGui.SetCursorScreenPos(min);
                var buttonId = $"##gratis-{id}";
                context.Trace?.Widget(ImGui.GetID(buttonId));
                var clicked = ChromeControls.Button(buttonId, new Vector2(GratisTheme.ControlHeight * scale));
                var hovered = ImGui.IsItemHovered();
                var lift = HoverAnimation.Progress($"toolbar/{ImGui.GetID(id)}", hovered);
                if (active || lift > 0) list.AddRectFilled(min, min + new Vector2(GratisTheme.ControlHeight * scale),
                    ImGui.GetColorU32(active ? context.Config.AccentColor with { W = .12f } : new Vector4(1, 1, 1, .06f * lift)), GratisTheme.ButtonRadius * scale);
                ChromeControls.Feedback(list, min, min + new Vector2(GratisTheme.ControlHeight * scale), fill: false);
                if (id == "Filters") GratisIcons.Draw(list, min + new Vector2(GratisTheme.ControlHeight / 2 * scale), GratisIcons.Eye,
                    active ? context.Config.AccentColor : GratisTheme.Primary, GratisTheme.IconSize * scale);
                else DrawGlyph(list, min + new Vector2(GratisTheme.ControlHeight / 2 * scale), action, active ? context.Config.AccentColor : GratisTheme.Primary, scale, context.Config.FollowPlayer);
                ChromeControls.Tooltip(tooltip, id == "Search" && context.Config.Palette ? context.Config.PaletteShortcut : null);
                if (clicked) {
                    if (id == "Lens") ImGui.OpenPopup(MapMenu);
                    else if (id == "Filters") {
                        if (context.Config.QuickFilterBar) FilterBarDrawing.TogglePopover(); else context.OpenFilterSettings();
                    }
                    else activate(action);
                }
                if (id == "Lens") {
                    DrawMapMenu(activate);
                    if (ImGui.IsPopupOpen("Gratis_Show_Layers")) context.Chrome.Reveal(ImGui.GetTime());
                    ImGui.PushStyleColor(ImGuiCol.PopupBg, GratisTheme.Surface);
                    ImGui.PushStyleColor(ImGuiCol.Border, GratisTheme.Border);
                    drawLayersContextMenu();
                    ImGui.PopStyleColor(2);
                }
            }
        }
        finally { ImGui.EndChild(); ImGui.PopStyleColor(); }
    }

    private static void DrawMapMenu(Action<ToolbarAction> activate)
    {
        var context = MapDrawContext.Current;
        var layers = false;
        using var style = new ChromePopupStyle();
        using var font = context.GetChromeFont(GratisTheme.TextSize * GratisTheme.ChromeScale, ChromeFontRole.Body)?.Push();
        if (!ImGui.BeginPopup(MapMenu)) return;
        MapMenuOpen = true;
        context.Chrome.Reveal(ImGui.GetTime());
        if (ImGui.IsKeyPressed(ImGuiKey.Escape, false)) { ImGui.CloseCurrentPopup(); ImGui.EndPopup(); return; }
        ImGui.SetNextItemWidth(200 * GratisTheme.ChromeScale);
        if (LensDrawing.DrawSelector(context.Config)) context.SaveConfig();
        ImGui.Separator();
        if (ImGui.MenuItem("Locate player")) activate(ToolbarAction.CenterPlayer);
        if (ImGui.MenuItem("Centre map")) activate(ToolbarAction.CenterMap);
        if (ImGui.MenuItem("Parent map")) activate(ToolbarAction.Parent);
        if (ImGui.MenuItem("Map layers...")) layers = true;
        ImGui.EndPopup();
        if (layers) activate(ToolbarAction.Layers);
    }

    private static void DrawGlyph(ImDrawListPtr list, Vector2 center, ToolbarAction action, Vector4 color, float scale, bool following)
    {
        if (GratisIcons.Draw(list, center, GratisIcons.ForAction(action, following), color,
            Math.Clamp(MapDrawContext.Current.Config.IconSize, 12, 24) * scale)) return;
        var ink = ImGui.GetColorU32(color);
        var stroke = 1.5f * scale;
        Vector2 P(float x, float y) => center + new Vector2(x, y) * scale;
        void Line(float x0, float y0, float x1, float y1) => list.AddLine(P(x0, y0), P(x1, y1), ink, stroke);
        switch (action) {
            case ToolbarAction.Parent:
                Line(0, 7, 0, -6); Line(-5, -1, 0, -6); Line(0, -6, 5, -1); break;
            case ToolbarAction.Layers:
                list.AddQuad(P(0, -7), P(7, -3), P(0, 1), P(-7, -3), ink, stroke);
                Line(-7, 1, 0, 5); Line(0, 5, 7, 1); Line(-7, 4, 0, 8); Line(0, 8, 7, 4); break;
            case ToolbarAction.Follow:
                list.AddRect(P(-5, -1), P(5, 7), ink, 2 * scale, ImDrawFlags.None, stroke);
                list.PathArcTo(P(following ? 0 : 3, -1), 4 * scale, MathF.PI, MathF.PI * 2, 12);
                list.PathStroke(ink, ImDrawFlags.None, stroke);
                Line(0, 2, 0, 4); break;
            case ToolbarAction.CenterPlayer:
                list.AddCircle(center, 4 * scale, ink, 20, stroke);
                Line(0, -8, 0, -5); Line(0, 5, 0, 8); Line(-8, 0, -5, 0); Line(5, 0, 8, 0); break;
            case ToolbarAction.CenterMap:
                for (var row = 0; row < 2; row++)
                    for (var col = 0; col < 2; col++)
                        list.AddRect(P(-7 + col * 8, -7 + row * 8), P(-1 + col * 8, -1 + row * 8), ink, 1.5f * scale, ImDrawFlags.None, stroke);
                break;
            case ToolbarAction.Search:
                list.AddCircle(P(-2, -2), 4.5f * scale, ink, 24, stroke);
                Line(2, 2, 7, 7); break;
            case ToolbarAction.Settings:
                list.AddCircle(center, 5 * scale, ink, 24, stroke);
                list.AddCircle(center, 1.5f * scale, ink, 16, stroke);
                for (var ray = 0; ray < 8; ray++) {
                    var a = MathF.PI * ray / 4;
                    var direction = new Vector2(MathF.Cos(a), MathF.Sin(a)) * scale;
                    list.AddLine(center + direction * 5, center + direction * 8, ink, stroke);
                }
                break;
            case ToolbarAction.Close:
                Line(-5, -5, 5, 5); Line(5, -5, -5, 5); break;
        }
    }
}
