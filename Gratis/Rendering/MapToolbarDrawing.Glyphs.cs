using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static partial class MapToolbarDrawing {
    private static void DrawToolbarGlyph(ImDrawListPtr list, Vector2 center, int index, ToolbarAction action,
        Vector4 color, float scale) {
        var context = MapDrawContext.Current;
        var glyph = index == 2 ? GratisIcons.Eye : GratisIcons.ForAction(action, context.Config.FollowPlayer);
        using var font = context.GetIconFont(TextDrawing.IconPixelSize(Math.Clamp(context.Config.IconSize, 12, 24)
            * scale))?.Push();
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
                    center + new Vector2(8 * scale, 0), center + new Vector2(0, 5 * scale), ImGui.GetColorU32(color),
                    1.5f * scale);
                list.AddCircle(center, 2 * scale, ImGui.GetColorU32(color), 16, scale);
            }
            else
                DrawGlyph(list, center, action, color, scale, context.Config.FollowPlayer);
            GlyphBounds[index] = new(center.X - 8 * scale, center.Y - 8 * scale, center.X + 8 * scale,
                center.Y + 8 * scale);
        }
    }

    private static void DrawGlyph(ImDrawListPtr list, Vector2 center, ToolbarAction action, Vector4 color, float scale,
        bool following) {
        if (GratisIcons.Draw(list, center, GratisIcons.ForAction(action, following), color,
            Math.Clamp(MapDrawContext.Current.Config.IconSize, 12, 24) * scale))
            return;
        var ink = ImGui.GetColorU32(color);
        var stroke = 1.5f * scale;
        Vector2 P(float x, float y) => center + new Vector2(x, y) * scale;
        void Line(float x0, float y0, float x1, float y1) => list.AddLine(P(x0, y0), P(x1, y1), ink, stroke);
        switch (action) {
            case ToolbarAction.Parent:
                Line(0, 7, 0, -6);
                Line(-5, -1, 0, -6);
                Line(0, -6, 5, -1);
                break;
            case ToolbarAction.Layers:
                list.AddQuad(P(0, -7), P(7, -3), P(0, 1), P(-7, -3), ink, stroke);
                Line(-7, 1, 0, 5);
                Line(0, 5, 7, 1);
                Line(-7, 4, 0, 8);
                Line(0, 8, 7, 4);
                break;
            case ToolbarAction.Follow:
                list.AddRect(P(-5, -1), P(5, 7), ink, 2 * scale, ImDrawFlags.None, stroke);
                list.PathArcTo(P(following ? 0 : 3, -1), 4 * scale, MathF.PI, MathF.PI * 2, 12);
                list.PathStroke(ink, ImDrawFlags.None, stroke);
                Line(0, 2, 0, 4);
                break;
            case ToolbarAction.CenterPlayer:
                list.AddCircle(center, 4 * scale, ink, 20, stroke);
                Line(0, -8, 0, -5);
                Line(0, 5, 0, 8);
                Line(-8, 0, -5, 0);
                Line(5, 0, 8, 0);
                break;
            case ToolbarAction.CenterMap:
                for (var row = 0; row < 2; row++)
                    for (var col = 0; col < 2; col++)
                        list.AddRect(P(-7 + col * 8, -7 + row * 8), P(-1 + col * 8, -1 + row * 8), ink, 1.5f * scale,
                            ImDrawFlags.None, stroke);
                break;
            case ToolbarAction.Search:
                list.AddCircle(P(-2, -2), 4.5f * scale, ink, 24, stroke);
                Line(2, 2, 7, 7);
                break;
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
                Line(-5, -5, 5, 5);
                Line(5, -5, -5, 5);
                break;
        }
    }
}