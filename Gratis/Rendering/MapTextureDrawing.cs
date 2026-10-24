using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif
using Gratis.Classes;

namespace Gratis.Rendering;

public static class MapTextureDrawing
{
    public static void DrawMapImage(DrawTexture texture, float scale)
    {
        var size = texture.Size * scale;
        var rounding = MapShape.Rounding(ImGui.GetWindowSize());
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
        if (visibleMax.X <= visibleMin.X || visibleMax.Y <= visibleMin.Y) return;

        var flags = ImDrawFlags.None;
        if (imageMin.X <= clipMin.X && imageMin.Y <= clipMin.Y) flags |= ImDrawFlags.RoundCornersTopLeft;
        if (imageMax.X >= clipMax.X && imageMin.Y <= clipMin.Y) flags |= ImDrawFlags.RoundCornersTopRight;
        if (imageMin.X <= clipMin.X && imageMax.Y >= clipMax.Y) flags |= ImDrawFlags.RoundCornersBottomLeft;
        if (imageMax.X >= clipMax.X && imageMax.Y >= clipMax.Y) flags |= ImDrawFlags.RoundCornersBottomRight;

        var uvMin = (visibleMin - imageMin) / size;
        var uvMax = (visibleMax - imageMin) / size;
        ImGui.GetWindowDrawList().AddImageRounded(texture.Handle, visibleMin, visibleMax, uvMin, uvMax, ImGui.GetColorU32(Vector4.One), rounding, flags);
    }

}
