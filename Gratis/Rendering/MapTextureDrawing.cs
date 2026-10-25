using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif
using Gratis.Classes;

namespace Gratis.Rendering;

public static class MapTextureDrawing {
    public static void DrawMapImage(DrawTexture texture, float scale) {
        var size = texture.Size * scale;
        var rounding = MapShape.Rounding(ImGui.GetWindowSize());
        if (MapShape.FeatherWidth > 0 || MapDrawContext.Current.Config.IsGratis && rounding > 0) {
            var origin = ImGui.GetCursorScreenPos();
            ImGui.Dummy(size);
            FeatheredMapDrawing.Draw(ImGui.GetWindowDrawList(), texture, origin, size, ImGui.GetWindowPos(),
                ImGui.GetWindowPos() + ImGui.GetWindowSize(), rounding, MathF.Max(.5f, MapShape.FeatherWidth));
            return;
        }
        if (rounding <= 0.0f) {
            ImGui.Image(texture.Handle, size);
            return;
        }

        var imageMin = ImGui.GetCursorScreenPos();
        var imageMax = imageMin + size;
        ImGui.Dummy(size);

        var clipMin = ImGui.GetWindowPos();
        var clipMax = clipMin + ImGui.GetWindowSize();
        var visibleMin = Vector2.Max(imageMin, clipMin);
        var visibleMax = Vector2.Min(imageMax, clipMax);
        if (visibleMax.X <= visibleMin.X || visibleMax.Y <= visibleMin.Y)
            return;

        if (MapDrawContext.Current.Config.IsGratis && MapDrawContext.Current.Config.MapCircle) {
            DrawCircularImage(ImGui.GetWindowDrawList(), texture, imageMin, size, clipMin, clipMax, visibleMin,
                visibleMax);
            return;
        }

        var flags = ImDrawFlags.None;
        if (imageMin.X <= clipMin.X && imageMin.Y <= clipMin.Y)
            flags |= ImDrawFlags.RoundCornersTopLeft;
        if (imageMax.X >= clipMax.X && imageMin.Y <= clipMin.Y)
            flags |= ImDrawFlags.RoundCornersTopRight;
        if (imageMin.X <= clipMin.X && imageMax.Y >= clipMax.Y)
            flags |= ImDrawFlags.RoundCornersBottomLeft;
        if (imageMax.X >= clipMax.X && imageMax.Y >= clipMax.Y)
            flags |= ImDrawFlags.RoundCornersBottomRight;

        var uvMin = (visibleMin - imageMin) / size;
        var uvMax = (visibleMax - imageMin) / size;
        ImGui.GetWindowDrawList().AddImageRounded(texture.Handle, visibleMin, visibleMax, uvMin, uvMax,
            ImGui.GetColorU32(Vector4.One), rounding, flags);
    }

    private static void DrawCircularImage(ImDrawListPtr list, DrawTexture texture, Vector2 imageMin, Vector2 imageSize,
        Vector2 min, Vector2 max, Vector2 visibleMin, Vector2 visibleMax) {
        var center = (min + max) / 2;
        var radius = MathF.Min(max.X - min.X, max.Y - min.Y) / 2;
        var segments = FrameMaterialDrawing.ArcSegments(radius, MathF.Tau);
        var white = ImGui.GetColorU32(Vector4.One);
        var clear = white & 0x00FFFFFFu;
        list.PushClipRect(visibleMin, visibleMax, true);
        DrawBinding.PushTexture(list, texture);
        list.PrimReserve(segments * 9, 1 + segments * 2);
        var first = DrawBinding.VertexIndex(list);
        list.PrimWriteVtx(center, (center - imageMin) / imageSize, white);
        for (var index = 0; index < segments; index++) {
            var angle = index * MathF.Tau / segments;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var inner = center + direction * (radius - .5f);
            var outer = center + direction * (radius + .5f);
            list.PrimWriteVtx(inner, (inner - imageMin) / imageSize, white);
            list.PrimWriteVtx(outer, (outer - imageMin) / imageSize, clear);
        }
        for (var index = 0; index < segments; index++) {
            var a = (ushort)(first + 1 + index * 2);
            var b = (ushort)(first + 1 + (index + 1) % segments * 2);
            list.PrimWriteIdx((ushort)first);
            list.PrimWriteIdx(a);
            list.PrimWriteIdx(b);
            list.PrimWriteIdx(a);
            list.PrimWriteIdx((ushort)(a + 1));
            list.PrimWriteIdx((ushort)(b + 1));
            list.PrimWriteIdx(a);
            list.PrimWriteIdx((ushort)(b + 1));
            list.PrimWriteIdx(b);
        }
        DrawBinding.PopTexture(list);
        list.PopClipRect();
    }

}