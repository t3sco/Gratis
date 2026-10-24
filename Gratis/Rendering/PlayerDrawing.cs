using System;
using System.Numerics;
using Gratis.Classes;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class PlayerDrawing
{
    public static bool IsPlayerMarker(MarkerInfo marker) => MarkerIcon.Family(marker) is
        MarkerIconFamily.Party or MarkerIconFamily.Alliance or MarkerIconFamily.Player;

    public static void DrawDot(ImDrawListPtr list, Vector2 center, float radius, MarkerIconFamily family, Vector4 tint, float alpha = 1)
    {
        var context = MapDrawContext.Current;
        var scale = context.GlobalScale;
        var party = family == MarkerIconFamily.Party;
        var color = (party ? context.Config.AccentColor : family == MarkerIconFamily.Alliance
            ? context.Config.PartyMarkerColor : Vector4.One) * tint;
        var diameter = party ? 9f : family == MarkerIconFamily.Alliance ? 8f : 6f;
        radius = MathF.Max(2.5f * scale, radius * diameter / 14);
        list.AddCircleFilled(center, radius, ImGui.GetColorU32(color with { W = color.W * alpha }), 24);
        var outline = family == MarkerIconFamily.Player ? GratisTheme.Outline : Vector4.One;
        list.AddCircle(center, radius + .75f * scale,
            ImGui.GetColorU32(outline with { W = outline.W * tint.W * alpha }), 24, 1.5f * scale);
    }

    public static void DrawLookLine(Vector2 position, float scale, float angle)
    {
        var config = MapDrawContext.Current.Config;
        var extent = config.IsGratis && config.FlatPlayer
            ? config.SoftConeLength * (config.ScalePlayerCone ? 1 : scale) * MapDrawContext.Current.GlobalScale
            : config.ConeSize * (config.ScalePlayerCone ? 1 : scale);
        if (!MapShape.ContainsSymbol(position, new Vector2(extent))) return;
        if (config.IsGratis && config.FlatPlayer) {
            DrawSoftCone(position, config.SoftConeLength * (config.ScalePlayerCone ? 1 : scale) * MapDrawContext.Current.GlobalScale, angle);
            return;
        }
        var lineLength = MapDrawContext.Current.Config.ConeSize * (MapDrawContext.Current.Config.ScalePlayerCone ? 1.0f : scale);
        var halfConeAngle = DegreesToRadians(90.0f) / 2.0f;

        DrawAngledLineFromCenter(position, lineLength, angle - halfConeAngle);
        DrawAngledLineFromCenter(position, lineLength, angle + halfConeAngle);
        DrawLineArcFromCenter(position, lineLength, angle);

        DrawFilledSemiCircle(position, lineLength, angle);
    }

    private static void DrawAngledLineFromCenter(Vector2 center, float lineLength, float angle)
    {
        var lineSegment = new Vector2(lineLength * MathF.Cos(angle), lineLength * MathF.Sin(angle));
        var coneOutlineColor = ImGui.GetColorU32(MapDrawContext.Current.Config.PlayerConeOutlineColor);
        ImGui.GetWindowDrawList().AddLine(center, center + lineSegment, coneOutlineColor, 3.0f);
    }

    private static void DrawLineArcFromCenter(Vector2 center, float distance, float rotation)
    {
        var halfConeAngle = DegreesToRadians(90.0f) / 2.0f;
        var coneOutlineColor = ImGui.GetColorU32(MapDrawContext.Current.Config.PlayerConeOutlineColor);

        var start = rotation - halfConeAngle;
        var stop = rotation + halfConeAngle;

        ImGui.GetWindowDrawList().PathArcTo(center, distance, start, stop);
        ImGui.GetWindowDrawList().PathStroke(coneOutlineColor, ImDrawFlags.None, 3.0f);
    }

    private static void DrawFilledSemiCircle(Vector2 center, float distance, float rotation)
    {
        var halfConeAngle = DegreesToRadians(90.0f) / 2.0f;

        var coneColor = ImGui.GetColorU32(MapDrawContext.Current.Config.PlayerConeColor);
        var startAngle = rotation - halfConeAngle;
        var stopAngle = rotation + halfConeAngle;

        var startPosition = new Vector2(distance * MathF.Cos(rotation - halfConeAngle), distance * MathF.Sin(rotation - halfConeAngle));

        ImGui.GetWindowDrawList().PathArcTo(center, distance, startAngle, stopAngle);
        ImGui.GetWindowDrawList().PathLineTo(center);
        ImGui.GetWindowDrawList().PathLineTo(center + startPosition);
        ImGui.GetWindowDrawList().PathFillConvex(coneColor);
    }

    private static float DegreesToRadians(float degrees) => MathF.PI / 180.0f * degrees;

    public static void DrawPlayerIcon(Vector2 position, DrawTexture texture, float angle, float mapScale)
    {
        var scale = MapDrawContext.Current.Config.ScaleWithZoom ? mapScale : 1.0f;
        scale *= MapDrawContext.Current.Config.PlayerIconScale;

        if (MapDrawContext.Current.Config is { IsGratis: true, FlatPlayer: true } config) {
            if (!float.IsFinite(scale) || scale <= 0) return;
            scale *= MapDrawContext.Current.GlobalScale;
            if (!MapShape.ContainsSymbol(position, new Vector2(13 * scale))) return;
            var forward = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var side = new Vector2(-forward.Y, forward.X);
            var tip = position + forward * 11 * scale;
            var left = position - forward * 7 * scale - side * 7 * scale;
            var right = position - forward * 7 * scale + side * 7 * scale;
            var notch = position - forward * 2 * scale;
            var list = ImGui.GetWindowDrawList();
            var color = ImGui.GetColorU32(config.AccentColor);
            list.AddTriangleFilled(tip, left, notch, color);
            list.AddTriangleFilled(tip, notch, right, color);
            var outline = ImGui.GetColorU32(Vector4.One);
            var stroke = 1.5f * MapDrawContext.Current.GlobalScale * MathF.Min(1, MathF.Max(0, config.PlayerIconScale));
            list.AddLine(tip, left, outline, stroke); list.AddLine(left, notch, outline, stroke);
            list.AddLine(notch, right, outline, stroke); list.AddLine(right, tip, outline, stroke);
            if (config.PlayerInnerHighlight) list.AddLine(tip - forward * 3 * scale, notch + forward * 2 * scale, ImGui.GetColorU32(new Vector4(1, 1, 1, .7f)), scale);
            return;
        }

        var vectors = GetRotationVectors(angle, position, texture.Size / 2.0f * scale);
        foreach (var point in vectors) if (!MapShape.ContainsSymbol(point, Vector2.Zero)) return;

        ImGui.GetWindowDrawList().AddImageQuad(texture.Handle, vectors[0], vectors[1], vectors[2], vectors[3]);
    }

    private static Vector2[] GetRotationVectors(float angle, Vector2 center, Vector2 size)
    {
        var cosA = MathF.Cos(angle + 0.5f * MathF.PI);
        var sinA = MathF.Sin(angle + 0.5f * MathF.PI);

        Vector2[] vectors =
        [
            center + ImRotate(new Vector2(-size.X * 0.5f, -size.Y * 0.5f), cosA, sinA),
            center + ImRotate(new Vector2(+size.X * 0.5f, -size.Y * 0.5f), cosA, sinA),
            center + ImRotate(new Vector2(+size.X * 0.5f, +size.Y * 0.5f), cosA, sinA),
            center + ImRotate(new Vector2(-size.X * 0.5f, +size.Y * 0.5f), cosA, sinA),
        ];
        return vectors;
    }

    private static Vector2 ImRotate(Vector2 v, float cosA, float sinA) => new(v.X * cosA - v.Y * sinA, v.X * sinA + v.Y * cosA);

    private static void DrawSoftCone(Vector2 center, float length, float angle)
    {
        if (length <= 0) return;
        const int rings = 32, sectors = 32;
        var config = MapDrawContext.Current.Config;
        var list = ImGui.GetWindowDrawList();
        var uv = ImGui.GetFontTexUvWhitePixel();
        for (var ring = 0; ring < rings; ring++) {
            var r0 = ring / (float)rings; var r1 = (ring + 1f) / rings;
            for (var sector = 0; sector < sectors; sector++) {
                var a0 = sector / (float)sectors; var a1 = (sector + 1f) / sectors;
                Vector2 Point(float r, float a) => center + length * r * new Vector2(MathF.Cos(angle + (a - .5f) * MathF.PI / 2), MathF.Sin(angle + (a - .5f) * MathF.PI / 2));
                uint Color(float r, float a) => ImGui.GetColorU32(config.AccentColor with { W = config.SoftConeOpacity * (1 - r * r * (3 - 2 * r)) * MathF.Sin(a * MathF.PI) });
                list.PrimReserve(6, 4);
                var index = (ushort)DrawBinding.VertexIndex(list);
                list.PrimWriteVtx(Point(r0, a0), uv, Color(r0, a0));
                list.PrimWriteVtx(Point(r1, a0), uv, Color(r1, a0));
                list.PrimWriteVtx(Point(r1, a1), uv, Color(r1, a1));
                list.PrimWriteVtx(Point(r0, a1), uv, Color(r0, a1));
                list.PrimWriteIdx(index); list.PrimWriteIdx((ushort)(index + 1)); list.PrimWriteIdx((ushort)(index + 2));
                list.PrimWriteIdx(index); list.PrimWriteIdx((ushort)(index + 2)); list.PrimWriteIdx((ushort)(index + 3));
            }
        }
    }
}
