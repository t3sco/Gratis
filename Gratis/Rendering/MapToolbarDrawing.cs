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
    public static void Draw(Action<ToolbarAction> activate, Action drawLayersContextMenu) {
        var context = MapDrawContext.Current;
        if (context.Config.IsGratis && context.Config.SegmentedToolbar) {
            DrawSegmented(activate, drawLayersContextMenu);
            return;
        }
        var borderPadding = 2.5f;
        var toolbarSize = new Vector2(DrawBinding.ContentRegionMax().X, 33.0f * context.GlobalScale);

        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero with { W = context.Config.ToolbarFade });
        var visible = ImGui.BeginChild("toolbar_child", toolbarSize);
        try {
            if (!visible) return;

            var cornerInset = MapShape.Current * 0.29f;
            ImGui.SetCursorPos(new Vector2(borderPadding, borderPadding) * context.GlobalScale + new Vector2(cornerInset));

            if (context.IconButton(ToolbarAction.Parent, "up", "Open Parent Map")) {
                activate(ToolbarAction.Parent);
            }

            ImGui.SameLine();

            if (context.IconButton(ToolbarAction.Layers, "layers", "Show Map Layers")) {
                activate(ToolbarAction.Layers);
            }

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

    private static readonly (ToolbarAction Action, string Tooltip)[] Buttons = [
        (ToolbarAction.Parent, "Open Parent Map"), (ToolbarAction.Layers, "Show Map Layers"),
        (ToolbarAction.Follow, "Toggle Follow Player"), (ToolbarAction.CenterPlayer, "Center on Player"),
        (ToolbarAction.CenterMap, "Center on Map"), (ToolbarAction.Search, "Search for Map"),
        (ToolbarAction.Settings, "Open Settings"), (ToolbarAction.Close, "Close Map"),
    ];

    private static void DrawSegmented(Action<ToolbarAction> activate, Action drawLayersContextMenu)
    {
        var context = MapDrawContext.Current;
        var scale = context.GlobalScale;
        var count = context.Config.UseCustomWindowChrome ? 8 : 7;
        var width = (count * 28 + 8) * scale;
        var offset = MapWindowDrawing.ToolbarOffset(count);
        ImGui.SetCursorPos(offset);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        var visible = ImGui.BeginChild("toolbar_child", new Vector2(width, 36 * scale));
        try {
            if (!visible) return;
            var origin = ImGui.GetWindowPos();
            var list = ImGui.GetWindowDrawList();
            MapWindowDrawing.DrawSurface(list, origin, origin + new Vector2(width, 36 * scale), 18 * scale);
            for (var index = 0; index < count; index++) {
                var (action, tooltip) = Buttons[index];
                var min = origin + new Vector2(4 + index * 28, 4) * scale;
                var active = action == ToolbarAction.Follow && context.Config.FollowPlayer || action == ToolbarAction.Layers && ImGui.IsPopupOpen("Gratis_Show_Layers");
                ImGui.SetCursorScreenPos(min);
                var clicked = ImGui.InvisibleButton($"##gratis-{action}", new Vector2(28 * scale));
                var hovered = ImGui.IsItemHovered();
                var lift = HoverAnimation.Progress($"toolbar/{ImGui.GetID(action.ToString())}", hovered);
                if (active || lift > 0) list.AddRectFilled(min, min + new Vector2(28 * scale),
                    ImGui.GetColorU32(active ? context.Config.AccentColor with { W = .12f } : new Vector4(1, 1, 1, .06f * lift)), GratisTheme.ButtonRadius * scale);
                DrawGlyph(list, min + new Vector2(14 * scale), action, active ? context.Config.AccentColor : GratisTheme.Primary, scale, context.Config.FollowPlayer);
                if (hovered) {
                    if (context.Config.HoverCardStyle == 0) HoverCardDrawing.Draw($"toolbar/{action}", tooltip);
                    else ImGui.SetTooltip(tooltip);
                }
                if (clicked) activate(action);
                if (action == ToolbarAction.Layers) {
                    ImGui.PushStyleColor(ImGuiCol.PopupBg, GratisTheme.Surface);
                    ImGui.PushStyleColor(ImGuiCol.Border, GratisTheme.Border);
                    drawLayersContextMenu();
                    ImGui.PopStyleColor(2);
                }
            }
        }
        finally { ImGui.EndChild(); ImGui.PopStyleColor(); }
    }

    private static void DrawGlyph(ImDrawListPtr list, Vector2 center, ToolbarAction action, Vector4 color, float scale, bool following)
    {
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
