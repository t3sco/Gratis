using System;
using System.Linq;
using System.Numerics;
using Gratis.Classes;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class PlayerDrawing {
    public static bool IsPlayerMarker(MarkerInfo marker) => MarkerIcon.Family(marker) is
        MarkerIconFamily.Party or MarkerIconFamily.Alliance or MarkerIconFamily.Player;

    public static void DrawDot(ImDrawListPtr list, Vector2 center, float radius, MarkerIconFamily family, Vector4 tint,
        float alpha = 1) {
        var context = MapDrawContext.Current;
        var scale = context.GlobalScale;
        var party = family == MarkerIconFamily.Party;
        var color = (party ? context.Config.AccentColor : family == MarkerIconFamily.Alliance
            ? context.Config.PartyMarkerColor : Vector4.One)
            * tint;
        var diameter = party ? 9f : family == MarkerIconFamily.Alliance ? 8f : 6f;
        radius = MathF.Max(2.5f * scale, radius * diameter / 14);
        list.AddCircleFilled(center, radius, ImGui.GetColorU32(color with { W = color.W * alpha }), 24);
        var outline = family == MarkerIconFamily.Player ? GratisTheme.Outline : Vector4.One;
        list.AddCircle(center, radius + .75f * scale,
            ImGui.GetColorU32(outline with { W = outline.W * tint.W * alpha }), 24, 1.5f * scale);
    }

    public static void DrawLookLine(Vector2 position, float scale, float angle) {
        var config = MapDrawContext.Current.Config;
        if (config.IsGratis)
            scale = ViewportMath.SymbolScale(scale);
        var extent = config.IsGratis && config.FlatPlayer
            ? config.SoftConeLength * (config.ScalePlayerCone ? 1 : scale) * MapDrawContext.Current.GlobalScale
            : config.ConeSize * (config.ScalePlayerCone ? 1 : scale);
        if (!MapShape.ContainsSymbol(position, new Vector2(extent)))
            return;
        if (config.IsGratis && config.FlatPlayer) {
            DrawSoftCone(position, config.SoftConeLength * (config.ScalePlayerCone ? 1 : scale)
                * MapDrawContext.Current.GlobalScale,
                angle);
            return;
        }
        var lineLength = MapDrawContext.Current.Config.ConeSize
            * (MapDrawContext.Current.Config.ScalePlayerCone ? 1.0f : scale);
        var halfConeAngle = DegreesToRadians(90.0f) / 2.0f;

        DrawAngledLineFromCenter(position, lineLength, angle - halfConeAngle);
        DrawAngledLineFromCenter(position, lineLength, angle + halfConeAngle);
        DrawLineArcFromCenter(position, lineLength, angle);

        DrawFilledSemiCircle(position, lineLength, angle);
    }

    private static void DrawAngledLineFromCenter(Vector2 center, float lineLength, float angle) {
        var lineSegment = new Vector2(lineLength * MathF.Cos(angle), lineLength * MathF.Sin(angle));
        var coneOutlineColor = ImGui.GetColorU32(MapDrawContext.Current.Config.PlayerConeOutlineColor);
        ImGui.GetWindowDrawList().AddLine(center, center + lineSegment, coneOutlineColor, 3.0f);
    }

    private static void DrawLineArcFromCenter(Vector2 center, float distance, float rotation) {
        var halfConeAngle = DegreesToRadians(90.0f) / 2.0f;
        var coneOutlineColor = ImGui.GetColorU32(MapDrawContext.Current.Config.PlayerConeOutlineColor);

        var start = rotation - halfConeAngle;
        var stop = rotation + halfConeAngle;

        ImGui.GetWindowDrawList().PathArcTo(center, distance, start, stop);
        ImGui.GetWindowDrawList().PathStroke(coneOutlineColor, ImDrawFlags.None, 3.0f);
    }

    private static void DrawFilledSemiCircle(Vector2 center, float distance, float rotation) {
        var halfConeAngle = DegreesToRadians(90.0f) / 2.0f;

        var coneColor = ImGui.GetColorU32(MapDrawContext.Current.Config.PlayerConeColor);
        var startAngle = rotation - halfConeAngle;
        var stopAngle = rotation + halfConeAngle;

        var startPosition = new Vector2(distance * MathF.Cos(rotation - halfConeAngle),
            distance * MathF.Sin(rotation - halfConeAngle));

        ImGui.GetWindowDrawList().PathArcTo(center, distance, startAngle, stopAngle);
        ImGui.GetWindowDrawList().PathLineTo(center);
        ImGui.GetWindowDrawList().PathLineTo(center + startPosition);
        ImGui.GetWindowDrawList().PathFillConvex(coneColor);
    }

    private static float DegreesToRadians(float degrees) => MathF.PI / 180.0f * degrees;

    public static void DrawPlayerIcon(Vector2 position, DrawTexture texture, float angle, float mapScale) {
        var scale = MapDrawContext.Current.Config.ScaleWithZoom ? mapScale : 1.0f;
        if (MapDrawContext.Current.Config.IsGratis)
            scale = ViewportMath.SymbolScale(scale);
        scale *= MapDrawContext.Current.Config.PlayerIconScale;

        if (MapDrawContext.Current.Config is { IsGratis: true, FlatPlayer: true } config) {
            if (!float.IsFinite(scale) || scale <= 0)
                return;
            scale *= MapDrawContext.Current.GlobalScale;
            if (!MapShape.ContainsSymbol(position, new Vector2(13 * scale)))
                return;
            LabelLayout.Reserve(position - new Vector2(13 * scale), position + new Vector2(13 * scale));
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
            list.AddLine(tip, left, outline, stroke);
            list.AddLine(left, notch, outline, stroke);
            list.AddLine(notch, right, outline, stroke);
            list.AddLine(right, tip, outline, stroke);
            if (config.PlayerInnerHighlight)
                list.AddLine(tip - forward * 3 * scale, notch + forward * 2 * scale,
                    ImGui.GetColorU32(new Vector4(1, 1, 1, .7f)), scale);
            return;
        }

        Span<Vector2> vectors = stackalloc Vector2[4];
        GetRotationVectors(vectors, angle, position, texture.Size / 2.0f * scale);
        foreach (var point in vectors)
            if (!MapShape.ContainsSymbol(point, Vector2.Zero))
                return;

        if (MapDrawContext.Current.Config.IsGratis) {
            var min = vectors[0];
            var max = min;
            foreach (var point in vectors) {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            LabelLayout.Reserve(min - Vector2.One, max + Vector2.One);
        }
        ImGui.GetWindowDrawList().AddImageQuad(texture.Handle, vectors[0], vectors[1], vectors[2], vectors[3]);
    }

    private static void GetRotationVectors(Span<Vector2> vectors, float angle, Vector2 center, Vector2 size) {
        var cosA = MathF.Cos(angle + 0.5f * MathF.PI);
        var sinA = MathF.Sin(angle + 0.5f * MathF.PI);

        vectors[0] = center + ImRotate(new Vector2(-size.X * 0.5f, -size.Y * 0.5f), cosA, sinA);
        vectors[1] = center + ImRotate(new Vector2(+size.X * 0.5f, -size.Y * 0.5f), cosA, sinA);
        vectors[2] = center + ImRotate(new Vector2(+size.X * 0.5f, +size.Y * 0.5f), cosA, sinA);
        vectors[3] = center + ImRotate(new Vector2(-size.X * 0.5f, +size.Y * 0.5f), cosA, sinA);
    }

    private static Vector2 ImRotate(Vector2 v, float cosA, float sinA) =>
        new(v.X * cosA - v.Y * sinA, v.X * sinA + v.Y * cosA);

    private const int ConeRings = 32, ConeSectors = 32, ConeStride = ConeSectors + 1;
    // Grid fractions k / 32, and the two factors of the cone's opacity falloff. The expressions are the ones each quad
    // used to evaluate for its own corners, so every corner gets the same float it always did.
    private static readonly float[] ConeFraction = Enumerable.Range(0, ConeStride).Select(k => k / (float)ConeSectors)
        .ToArray();
    private static readonly float[] ConeFalloff = ConeFraction.Select(r => 1 - r * r * (3 - 2 * r)).ToArray();
    private static readonly float[] ConeSine = ConeFraction.Select(a => MathF.Sin(a * MathF.PI)).ToArray();
    private static readonly uint[] ConeColors = new uint[(ConeRings + 1) * ConeStride];
    private static Vector4 coneAccent;
    private static float coneOpacity = float.NaN, coneStyleAlpha;

    // ImGui packs a colour with the style alpha, so the packed grid is reusable until the accent, the opacity or the
    // style alpha changes. It used to be packed four times per quad, every frame.
    private static uint[] SoftConeColors(Vector4 accent, float opacity) {
        var styleAlpha = ImGui.GetStyle().Alpha;
        if (accent == coneAccent && opacity == coneOpacity && styleAlpha == coneStyleAlpha)
            return ConeColors;
        for (var ring = 0; ring <= ConeRings; ring++)
            for (var sector = 0; sector < ConeStride; sector++)
                ConeColors[ring * ConeStride + sector] =
                    ImGui.GetColorU32(accent with { W = opacity * ConeFalloff[ring] * ConeSine[sector] });
        (coneAccent, coneOpacity, coneStyleAlpha) = (accent, opacity, styleAlpha);
        return ConeColors;
    }

    private static void DrawSoftCone(Vector2 center, float length, float angle) {
        if (length <= 0)
            return;
        const int rings = ConeRings, sectors = ConeSectors, stride = ConeStride;
        var config = MapDrawContext.Current.Config;
        var list = ImGui.GetWindowDrawList();
        var uv = ImGui.GetFontTexUvWhitePixel();
        var colors = SoftConeColors(config.AccentColor, config.SoftConeOpacity);
        // Neighbouring quads share corners: take each direction and corner position once instead of per quad.
        Span<Vector2> directions = stackalloc Vector2[stride];
        for (var sector = 0; sector < stride; sector++) {
            var theta = angle + (ConeFraction[sector] - .5f) * MathF.PI / 2;
            directions[sector] = new Vector2(MathF.Cos(theta), MathF.Sin(theta));
        }
        Span<Vector2> points = stackalloc Vector2[(rings + 1) * stride];
        for (var ring = 0; ring <= rings; ring++)
            for (var sector = 0; sector < stride; sector++)
                points[ring * stride + sector] = center + length * ConeFraction[ring] * directions[sector];
        // One reservation for the whole cone; the vertices and indices written are the same as one reserve per quad.
        list.PrimReserve(rings * sectors * 6, rings * sectors * 4);
        var first = (int)DrawBinding.VertexIndex(list);
        for (var ring = 0; ring < rings; ring++) {
            for (var sector = 0; sector < sectors; sector++) {
                var index = (ushort)(first + 4 * (ring * sectors + sector));
                var c00 = ring * stride + sector;
                var c10 = c00 + stride;
                list.PrimWriteVtx(points[c00], uv, colors[c00]);
                list.PrimWriteVtx(points[c10], uv, colors[c10]);
                list.PrimWriteVtx(points[c10 + 1], uv, colors[c10 + 1]);
                list.PrimWriteVtx(points[c00 + 1], uv, colors[c00 + 1]);
                list.PrimWriteIdx(index);
                list.PrimWriteIdx((ushort)(index + 1));
                list.PrimWriteIdx((ushort)(index + 2));
                list.PrimWriteIdx(index);
                list.PrimWriteIdx((ushort)(index + 2));
                list.PrimWriteIdx((ushort)(index + 3));
            }
        }
    }
}