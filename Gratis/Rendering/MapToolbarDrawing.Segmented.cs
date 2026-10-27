using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif
using Gratis.Classes;

namespace Gratis.Rendering;

public static partial class MapToolbarDrawing {
    private static void DrawSegmented(Action<ToolbarAction> activate, Action drawLayersContextMenu,
        PaletteOverlay? palette) {
        var context = MapDrawContext.Current;
        var scale = GratisTheme.ChromeScale;
        var (areaMin, areaMax) = MapShape.Bounds(Vector2.Zero, ImGui.GetWindowSize());
        var area = areaMax - areaMin;
        var (safeMin, safeMax) = ChromeLayout.ToolbarSafeBounds();
        var available = safeMax.X - safeMin.X;
        var fullCount = context.Config.UseCustomWindowChrome ? FullButtonCount : FullButtonCount - 1;
        var fullWidth = CollapsedWidth(fullCount) * scale;
        var searchWidth = palette?.InlineWidth(scale) ?? 0;
        var searching = palette?.IsOpen == true;
        var targetSearchWidth = searching ? 240 * scale : searchWidth;
        var centerAllowance = 2 * (GratisTheme.ToolbarCenterMargin - GratisTheme.Inset) * scale;
        var collapsedCompact = available < fullWidth || area.Y < 280 * scale;
        var collapsedCount = collapsedCompact ? (context.Config.UseCustomWindowChrome ? 3 : 2) : fullCount;
        if (available < CollapsedWidth(collapsedCount) * scale)
            collapsedCount = 2;
        var compact = available < fullWidth + targetSearchWidth || area.Y < 280 * scale;
        if (!ReferenceEquals(searchContext, context)) {
            searchContext = context;
            compactDuringSearch = compact;
        }
        if (searching)
            compactDuringSearch = compact;
        else if (searchWidth > 1)
            compact = compactDuringSearch;
        var count = compact ? (context.Config.UseCustomWindowChrome ? 3 : 2) : fullCount;
        if (available < CollapsedWidth(count) * scale + targetSearchWidth)
            count = 2;
        searchWidth = MathF.Min(searchWidth, MathF.Max(0, available - CollapsedWidth(count) * scale));
        var contentWidth = MathF.Max(CollapsedWidth(collapsedCount) * scale,
            CollapsedWidth(count) * scale + searchWidth);
        var anchor = context.Config.ToolbarAnchor;
        EffectiveAnchor = anchor == ChromeAnchor.TopCenter
            && available < MathF.Max(fullWidth, contentWidth) + centerAllowance
            ? ChromeAnchor.TopLeft : anchor;
        var width = context.Config.SegmentedToolbar ? contentWidth : available;
        if (!context.Config.SegmentedToolbar && EffectiveAnchor == ChromeAnchor.TopCenter)
            width -= centerAllowance;
        var height = GratisTheme.ToolbarHeight * scale;
        var bounds = ChromeLayout.Place("toolbar", new Vector2(width, height), EffectiveAnchor);
        if (bounds == Vector4.Zero)
            return;
        var surfaceList = ImGui.GetWindowDrawList();
        var surfaceOrigin = ChromeLayout.Position(bounds);
        ImGui.SetCursorScreenPos(surfaceOrigin);
        ImGui.Dummy(Vector2.Zero);
        ImGui.SetCursorScreenPos(surfaceOrigin);
        LastBounds = bounds;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        var visible = ImGui.BeginChild("toolbar_child", new Vector2(width, height));
        try {
            if (!visible)
                return;
            var origin = ImGui.GetWindowPos();
            var list = ImGui.GetWindowDrawList();
            MapWindowDrawing.DrawSurface(surfaceList, surfaceOrigin, surfaceOrigin + new Vector2(width, height),
                GratisTheme.Radius.Card * scale, surface: GlassSurface.Toolbar);
            var x = 4 * scale;
            var divider = 0;
            for (var index = 0; index < count; index++) {
                var buttonIndex = compact || count == 2 ? index == 0 ? 0 : index + 3 : index;
                var (id, action, tooltip) = Buttons[buttonIndex];
                if (id == "Filters" && FilterBarDrawing.TriggerLabel.Length > 0)
                    tooltip += " · " + FilterBarDrawing.TriggerLabel;
                var separated = index > 0 && !(buttonIndex == 2 && !compact);
                if (!context.Config.SegmentedToolbar && buttonIndex == 4)
                    x = MathF.Max(x, width - 4 * scale - (count - index) * GratisTheme.ControlHeight * scale
                        - (count - index - 1) * DividerSpace * scale - (separated ? DividerSpace * scale : 0));
                if (separated) {
                    var spacing = DividerSpace * scale;
                    var dx = origin.X + x + spacing / 2;
                    list.AddLine(new(dx, origin.Y + 13 * scale), new(dx, origin.Y + 31 * scale),
                        ImGui.GetColorU32(GratisTheme.Border), scale);
                    DividerBounds[divider++] = new(dx - scale / 2, origin.Y + 13 * scale, dx + scale / 2,
                        origin.Y + 31 * scale);
                    x += spacing;
                }
                var radius = GratisTheme.Radius.Inner(GratisTheme.Radius.Card * scale, 4 * scale);
                var min = origin + new Vector2(x, 4 * scale);
                var buttonSize = new Vector2(GratisTheme.ControlHeight * scale);
                ButtonBounds[buttonIndex] = new(min.X, min.Y, min.X + buttonSize.X, min.Y + buttonSize.Y);
                var active = action == ToolbarAction.Follow && context.Config.FollowPlayer
                    || id == "Lens" && (ImGui.IsPopupOpen(MapMenu) || ImGui.IsPopupOpen("Gratis_Show_Layers"))
                    || id == "Filters" && FilterBarDrawing.PopoverOpen;
                ImGui.SetCursorScreenPos(min);
                var buttonId = ButtonIds[buttonIndex];
                context.Trace?.Widget(ImGui.GetID(buttonId));
                var clicked = ChromeControls.Button(buttonId, new Vector2(GratisTheme.ControlHeight * scale));
                var hovered = ImGui.IsItemHovered();
                using var press = UiMotion.Control(list, min, min + buttonSize);
                var lift = UiMotion.Value(UiMotion.Item, 1, hovered ? 1 : 0, initial: 0);
                var chosen = UiMotion.Value(UiMotion.Item, 6, active ? 1 : 0);
                if (chosen > 0 || lift > 0)
                    list.AddRectFilled(min, min + new Vector2(GratisTheme.ControlHeight * scale),
                        ImGui.GetColorU32(Vector4.Lerp(new Vector4(1, 1, 1, .06f * lift),
                            context.Config.AccentColor with { W = .12f }, chosen)),
                        radius);
                ChromeControls.Feedback(list, min, min + new Vector2(GratisTheme.ControlHeight * scale), fill: false, radius: radius);
                DrawToolbarGlyph(list, min + buttonSize / 2, buttonIndex, action,
                    active ? context.Config.AccentColor : GratisTheme.Primary, scale);
                var news = UiMotion.Value(UiMotion.Item, 14,
                    id == "Settings" && TweakNews.HasUnseen(context.Config) ? 1 : 0, InterfaceMotionTokens.Standard,
                    initial: 0);
                if (news > 0)
                    Pill.Draw(list, min, "", PillFaces.SettingsDot, scale, alpha: news);
                if (id == "Filters" && FilterBarDrawing.ActiveCount > 0)
                    Pill.Draw(list, min, "", PillFaces.FilterDot, scale);
                ChromeControls.Tooltip(tooltip, windowBounds: WindowBounds);
                if (clicked) {
                    if (id == "Lens")
                        ImGui.OpenPopup(MapMenu);
                    else if (id == "Filters") {
                        FilterBarDrawing.TogglePopover();
                    }
                    else if (id == "Search" && palette?.IsOpen == true)
                        palette.Close();
                    else
                        activate(action);
                }
                x += buttonSize.X;
                if (id == "Search" && searchWidth > 1 && palette != null) {
                    if (!searching)
                        ClosingSearchBounds = new(origin.X + x, origin.Y + 4 * scale, origin.X + x + searchWidth,
                            origin.Y + 4 * scale + buttonSize.Y);
                    palette.DrawInline(origin + new Vector2(x, 4 * scale), new Vector2(searchWidth, buttonSize.Y));
                    x += searchWidth;
                }
                if (id == "Lens") {
                    DrawMapMenu(activate);
                    if (ImGui.IsPopupOpen("Gratis_Show_Layers"))
                        context.Chrome.Reveal(ImGui.GetTime());
                    using var menuStyle = PopupSurface.Push(PopupFaces.Chrome);
                    using var menuFont = context.GetChromeFont(GratisTheme.TextSize * scale,
                        ChromeFontRole.Body)?.Push();
                    drawLayersContextMenu();
                }
            }
        }
        finally { ImGui.EndChild(); ImGui.PopStyleColor(); }
    }
}