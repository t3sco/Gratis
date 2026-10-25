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
    public static Vector4 ClosingSearchBounds { get; private set; }
    public static bool SearchExitPointerOver => ClosingSearchBounds != Vector4.Zero &&
        ImGui.IsMouseHoveringRect(new(ClosingSearchBounds.X, ClosingSearchBounds.Y), new(ClosingSearchBounds.Z, ClosingSearchBounds.W), false);
    private static bool compactDuringSearch;
    private static MapDrawContext? searchContext;
    public static Vector4 WindowBounds { get; private set; }
    public static Vector4[] ButtonBounds { get; } = new Vector4[FullButtonCount];
    public static Vector4[] GlyphBounds { get; } = new Vector4[FullButtonCount];
    public static Vector4[] DividerBounds { get; } = new Vector4[4];
    public const float DividerSpace = 8;
    public static float CollapsedWidth(int count) => count * GratisTheme.ControlHeight + 8 + Math.Max(0, count - (count <= 3 ? 1 : 2)) * DividerSpace;
    public static void Draw(Action<ToolbarAction> activate, Action drawLayersContextMenu, PaletteOverlay? palette = null) {
        LastBounds = Vector4.Zero;
        ClosingSearchBounds = Vector4.Zero;
        Array.Clear(ButtonBounds); Array.Clear(GlyphBounds); Array.Clear(DividerBounds);
        var windowPos = ImGui.GetWindowPos(); var windowSize = ImGui.GetWindowSize();
        WindowBounds = new(windowPos.X, windowPos.Y, windowPos.X + windowSize.X, windowPos.Y + windowSize.Y);
        MapMenuOpen = false;
        var context = MapDrawContext.Current;
        using var chromeAlpha = new ChromeVisibility.AlphaScope(context.Config.IsGratis, palette?.IsOpen == true ? 1 : context.Chrome.ControlsAlpha(context.Config));
        if (!chromeAlpha.Interactive) return;
        if (context.Config.IsGratis) {
            DrawSegmented(activate, drawLayersContextMenu, palette);
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
    private static readonly string[] ButtonIds = ["##gratis-Search", "##gratis-Lens", "##gratis-Filters", "##gratis-Follow", "##gratis-Settings", "##gratis-Close"];
    private static readonly (string Id, ToolbarAction Action, string Tooltip)[] Buttons = [
        ("Search", ToolbarAction.Search, "Search places"), ("Lens", ToolbarAction.Layers, "Lenses & map layers"),
        ("Filters", ToolbarAction.CenterMap, "Filters"), ("Follow", ToolbarAction.Follow, "Follow player"),
        ("Settings", ToolbarAction.Settings, "Settings"), ("Close", ToolbarAction.Close, "Close map"),
    ];

    private static void DrawSegmented(Action<ToolbarAction> activate, Action drawLayersContextMenu, PaletteOverlay? palette)
    {
        var context = MapDrawContext.Current;
        var scale = GratisTheme.ChromeScale;
        var (areaMin, areaMax) = MapShape.Bounds(Vector2.Zero, ImGui.GetWindowSize());
        var area = areaMax - areaMin;
        var (safeMin, safeMax) = ChromeLayout.SafeBounds();
        var available = safeMax.X - safeMin.X;
        var fullCount = context.Config.UseCustomWindowChrome ? FullButtonCount : FullButtonCount - 1;
        var fullWidth = CollapsedWidth(fullCount) * scale;
        var searchWidth = palette?.InlineWidth(scale) ?? 0;
        var searching = palette?.IsOpen == true;
        var targetSearchWidth = searching ? 240 * scale : searchWidth;
        var centerAllowance = 2 * (GratisTheme.ToolbarCenterMargin - GratisTheme.Inset) * scale;
        var collapsedCompact = available < fullWidth || area.Y < 280 * scale;
        var collapsedCount = collapsedCompact ? (context.Config.UseCustomWindowChrome ? 3 : 2) : fullCount;
        if (available < CollapsedWidth(collapsedCount) * scale) collapsedCount = 2;
        var compact = available < fullWidth + targetSearchWidth || area.Y < 280 * scale;
        if (!ReferenceEquals(searchContext, context)) { searchContext = context; compactDuringSearch = compact; }
        if (searching) compactDuringSearch = compact;
        else if (searchWidth > 1) compact = compactDuringSearch;
        var count = compact ? (context.Config.UseCustomWindowChrome ? 3 : 2) : fullCount;
        if (available < CollapsedWidth(count) * scale + targetSearchWidth) count = 2;
        searchWidth = MathF.Min(searchWidth, MathF.Max(0, available - CollapsedWidth(count) * scale));
        var contentWidth = MathF.Max(CollapsedWidth(collapsedCount) * scale, CollapsedWidth(count) * scale + searchWidth);
        EffectiveAnchor = context.Config.ToolbarAnchor == ChromeAnchor.TopCenter && available < MathF.Max(fullWidth, contentWidth) + centerAllowance
            ? ChromeAnchor.TopLeft : context.Config.ToolbarAnchor;
        var width = context.Config.SegmentedToolbar ? contentWidth : available;
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
            var x = 4 * scale; var divider = 0;
            for (var index = 0; index < count; index++) {
                var buttonIndex = compact || count == 2 ? index == 0 ? 0 : index + 3 : index;
                var (id, action, tooltip) = Buttons[buttonIndex];
                if (id == "Filters" && FilterBarDrawing.TriggerLabel.Length > 0) tooltip += " · " + FilterBarDrawing.TriggerLabel;
                var separated = index > 0 && !(buttonIndex == 2 && !compact);
                if (!context.Config.SegmentedToolbar && buttonIndex == 4)
                    x = MathF.Max(x, width - 4 * scale - (count - index) * GratisTheme.ControlHeight * scale
                        - (count - index - 1) * DividerSpace * scale - (separated ? DividerSpace * scale : 0));
                if (separated) {
                    var spacing = DividerSpace * scale;
                    var dx = origin.X + x + spacing / 2;
                    list.AddLine(new(dx, origin.Y + 13 * scale), new(dx, origin.Y + 31 * scale), ImGui.GetColorU32(GratisTheme.Border), scale);
                    DividerBounds[divider++] = new(dx - scale / 2, origin.Y + 13 * scale, dx + scale / 2, origin.Y + 31 * scale);
                    x += spacing;
                }
                var min = origin + new Vector2(x, 4 * scale);
                var buttonSize = new Vector2(GratisTheme.ControlHeight * scale);
                ButtonBounds[buttonIndex] = new(min.X, min.Y, min.X + buttonSize.X, min.Y + buttonSize.Y);
                var active = action == ToolbarAction.Follow && context.Config.FollowPlayer || id == "Lens" &&
                    (ImGui.IsPopupOpen(MapMenu) || ImGui.IsPopupOpen("Gratis_Show_Layers")) || id == "Filters" && FilterBarDrawing.PopoverOpen;
                ImGui.SetCursorScreenPos(min);
                var buttonId = ButtonIds[buttonIndex];
                context.Trace?.Widget(ImGui.GetID(buttonId));
                var clicked = ChromeControls.Button(buttonId, new Vector2(GratisTheme.ControlHeight * scale));
                var hovered = ImGui.IsItemHovered();
                using var press = UiMotion.Control(list, min, min + buttonSize);
                var lift = UiMotion.Value(UiMotion.Item, 1, hovered ? 1 : 0, initial: 0);
                var chosen = UiMotion.Value(UiMotion.Item, 6, active ? 1 : 0);
                if (chosen > 0 || lift > 0) list.AddRectFilled(min, min + new Vector2(GratisTheme.ControlHeight * scale),
                    ImGui.GetColorU32(Vector4.Lerp(new Vector4(1, 1, 1, .06f * lift), context.Config.AccentColor with { W = .12f }, chosen)), GratisTheme.ButtonRadius * scale);
                ChromeControls.Feedback(list, min, min + new Vector2(GratisTheme.ControlHeight * scale), fill: false);
                DrawToolbarGlyph(list, min + buttonSize / 2, buttonIndex, action, active ? context.Config.AccentColor : GratisTheme.Primary, scale);
                var news = UiMotion.Value(UiMotion.Item, 14, id == "Settings" && TweakNews.HasUnseen(context.Config) ? 1 : 0, InterfaceMotionTokens.Standard, initial: 0);
                if (news > 0)
                    list.AddCircleFilled(min + new Vector2(28, 8) * scale, 2.5f * scale, ImGui.GetColorU32(GratisTheme.WarmAccent with { W = news }));
                if (id == "Filters" && FilterBarDrawing.ActiveCount > 0)
                    list.AddCircleFilled(min + new Vector2(28, 8) * scale, 2.5f * scale, ImGui.GetColorU32(GratisTheme.WarmAccent));
                ChromeControls.Tooltip(tooltip, windowBounds: WindowBounds);
                if (clicked) {
                    if (id == "Lens") ImGui.OpenPopup(MapMenu);
                    else if (id == "Filters") {
                        FilterBarDrawing.TogglePopover();
                    }
                    else if (id == "Search" && palette?.IsOpen == true) palette.Close();
                    else activate(action);
                }
                x += buttonSize.X;
                if (id == "Search" && searchWidth > 1 && palette != null) {
                    if (!searching) ClosingSearchBounds = new(origin.X + x, origin.Y + 4 * scale,
                        origin.X + x + searchWidth, origin.Y + 4 * scale + buttonSize.Y);
                    palette.DrawInline(origin + new Vector2(x, 4 * scale), new Vector2(searchWidth, buttonSize.Y));
                    x += searchWidth;
                }
                if (id == "Lens") {
                    DrawMapMenu(activate);
                    if (ImGui.IsPopupOpen("Gratis_Show_Layers")) context.Chrome.Reveal(ImGui.GetTime());
                    using var menuStyle = new ChromePopupStyle();
                    using var menuFont = context.GetChromeFont(GratisTheme.TextSize * scale, ChromeFontRole.Body)?.Push();
                    drawLayersContextMenu();
                }
            }
        }
        finally { ImGui.EndChild(); ImGui.PopStyleColor(); }
    }

    private static void DrawToolbarGlyph(ImDrawListPtr list, Vector2 center, int index, ToolbarAction action, Vector4 color, float scale)
    {
        var context = MapDrawContext.Current;
        var glyph = index == 2 ? GratisIcons.Eye : GratisIcons.ForAction(action, context.Config.FollowPlayer);
        using var font = context.GetIconFont(TextDrawing.IconPixelSize(Math.Clamp(context.Config.IconSize, 12, 24) * scale))?.Push();
        if (font != null && context.TryIconGlyphBounds(glyph, out var ink)) {
            var pos = center - new Vector2(ink.X + ink.Z, ink.Y + ink.W) / 2;
            DrawBinding.PushFontTexture(list);
            list.AddText(ImGui.GetFont(), ImGui.GetFont().FontSize, pos, ImGui.GetColorU32(color), glyph.ToString());
            DrawBinding.PopTexture(list);
            GlyphBounds[index] = new(pos.X + ink.X, pos.Y + ink.Y, pos.X + ink.Z, pos.Y + ink.W);
        }
        else {
            if (index == 2) {
                list.AddQuad(center - new Vector2(8 * scale, 0), center - new Vector2(0, 5 * scale),
                    center + new Vector2(8 * scale, 0), center + new Vector2(0, 5 * scale), ImGui.GetColorU32(color), 1.5f * scale);
                list.AddCircle(center, 2 * scale, ImGui.GetColorU32(color), 16, scale);
            }
            else DrawGlyph(list, center, action, color, scale, context.Config.FollowPlayer);
            GlyphBounds[index] = new(center.X - 8 * scale, center.Y - 8 * scale, center.X + 8 * scale, center.Y + 8 * scale);
        }
    }

    private static void DrawMapMenu(Action<ToolbarAction> activate)
    {
        var context = MapDrawContext.Current;
        var layers = false;
        using var style = new ChromePopupStyle();
        using var font = context.GetChromeFont(GratisTheme.TextSize * GratisTheme.ChromeScale, ChromeFontRole.Body)?.Push();
        var s = GratisTheme.ChromeScale; var (min, max) = ChromeLayout.SafeBounds();
        var size = Vector2.Min(new Vector2(240, 240) * s, max - min);
        var b = ChromePopoverMath.Place(ButtonBounds[1], size, min, max, GratisTheme.Gap * s);
        var popup = UiMotion.Popup(MapMenu, !ImGui.IsKeyPressed(ImGuiKey.Escape, false), new(b.X, b.Y), size,
            ImGuiWindowFlags.NoScrollWithMouse);
        if (!popup.Visible) return;
        try {
        UiMotion.Scroll();
        MapMenuOpen = popup.Interactive;
        context.Chrome.Reveal(ImGui.GetTime());
        ImGui.SetNextItemWidth(200 * GratisTheme.ChromeScale);
        if (LensDrawing.DrawSelector(context.Config)) context.SaveConfig();
        ImGui.Separator();
        if (NavigationItem("Locate player", GratisIcons.LocateFixed)) { activate(ToolbarAction.CenterPlayer); ImGui.CloseCurrentPopup(); }
        if (NavigationItem("Centre map", GratisIcons.LayoutGrid)) { activate(ToolbarAction.CenterMap); ImGui.CloseCurrentPopup(); }
        if (NavigationItem("Parent map", GratisIcons.ArrowUp)) { activate(ToolbarAction.Parent); ImGui.CloseCurrentPopup(); }
        if (NavigationItem("Map layers", GratisIcons.Layers)) { layers = true; ImGui.CloseCurrentPopup(); }
        } finally { popup.Dispose(); }
        if (layers) activate(ToolbarAction.Layers);
    }

    private static bool NavigationItem(string label, char glyph)
    {
        var s = GratisTheme.ChromeScale; var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(MathF.Max(200 * s, ImGui.GetContentRegionAvail().X), GratisTheme.ControlHeight * s);
        var clicked = ChromeControls.Button("##navigation-" + label, size);
        var list = ImGui.GetWindowDrawList(); ChromeControls.Feedback(list, pos, pos + size);
        GratisIcons.Draw(list, pos + new Vector2(16 * s, size.Y / 2), glyph, GratisTheme.Secondary, GratisTheme.IconSize * s);
        list.AddText(pos + new Vector2(36 * s, (size.Y - ImGui.GetFontSize()) / 2), ImGui.GetColorU32(GratisTheme.Primary), label);
        return clicked;
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
