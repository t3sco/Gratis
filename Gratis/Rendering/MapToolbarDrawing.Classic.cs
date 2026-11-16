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
    private static void DrawClassic(MapDrawContext context, Action<ToolbarAction> activate,
        Action drawLayersContextMenu) {
        var borderPadding = 2.5f;
        var toolbarSize = new Vector2(DrawBinding.ContentRegionMax().X, 33.0f * context.GlobalScale);

        var surfaceList = ImGui.GetWindowDrawList();
        // The caller's zero-size Dummy advances the cursor by ItemSpacing.y, which left a strip of map above the band.
        // The band is the top of the map view, so it starts at the window origin whatever the style spacing is.
        ImGui.SetCursorPos(Vector2.Zero);
        var surfaceOrigin = ImGui.GetCursorScreenPos();
        LastBounds = new(surfaceOrigin.X, surfaceOrigin.Y, surfaceOrigin.X + toolbarSize.X,
            surfaceOrigin.Y + toolbarSize.Y);
        ImGui.PushStyleColor(ImGuiCol.ChildBg,
            context.Config.IsGratis ? Vector4.Zero : Vector4.Zero with { W = context.Config.ToolbarFade });
        var visible = ImGui.BeginChild("toolbar_child", toolbarSize);
        try {
            if (!visible)
                return;

            if (context.Config.IsGratis)
                MapWindowDrawing.DrawSurface(surfaceList, surfaceOrigin, surfaceOrigin + toolbarSize,
                    GratisTheme.CardRadius * context.GlobalScale, surface: GlassSurface.Toolbar);

            var cornerInset = MapShape.Current * 0.29f;
            ImGui.SetCursorPos(new Vector2(borderPadding, borderPadding) * context.GlobalScale
                + new Vector2(cornerInset));

            if (context.IconButton(ToolbarAction.Parent, "up", "Open Parent Map")) {
                activate(ToolbarAction.Parent);
            }

            ImGui.SameLine();

            if (context.IconButton(ToolbarAction.Layers, "layers", "Show Map Layers")) {
                activate(ToolbarAction.Layers);
            }

            if (context.Config.IsGratis && ImGui.IsPopupOpen("Gratis_Show_Layers"))
                context.Chrome.Reveal(ImGui.GetTime());
            drawLayersContextMenu();

            ImGui.SameLine();

            var following = context.Config.FollowPlayer;
            if (following)
                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]);
            if (context.IconButton(ToolbarAction.Follow, "follow", "Toggle Follow Player")) {
                activate(ToolbarAction.Follow);
            }
            if (following)
                ImGui.PopStyleColor();

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

            var searchEnd = ImGui.GetItemRectMax().X;
            var buttonWidth = context.SettingsButtonWidth;

            var buttonCount = context.Config.HideWindowFrame ? 2 : 1;
            var rightWidth = buttonWidth * buttonCount + ImGui.GetStyle().ItemSpacing.X * (buttonCount - 1);
            ImGui.SameLine();
            ImGui.SetCursorPosX(DrawBinding.ContentRegionMax().X - rightWidth - context.GlobalScale * borderPadding
                - cornerInset);

            var settingsStart = ImGui.GetCursorScreenPos().X;
            if (context.IconButton(ToolbarAction.Settings, "settings", "Open Settings")) {
                activate(ToolbarAction.Settings);
            }

            if (!context.Config.HideWindowFrame)
                return;

            ImGui.SameLine();

            if (context.IconButton(ToolbarAction.Close, "closeMap", "Close Map")) {
                activate(ToolbarAction.Close);
            }

            var gap = ImGui.GetStyle().ItemSpacing.X;
            if (LocationInToolbar(context.Config))
                DrawClassicLocation(context, context.GlobalScale, surfaceOrigin.Y, toolbarSize.Y, searchEnd + gap,
                    settingsStart - gap);
        }
        finally {
            ImGui.EndChild();
            ImGui.PopStyleColor();
        }
    }
}