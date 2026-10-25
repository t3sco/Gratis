using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

internal static class DrawBinding
{
    public static unsafe bool ValidTextFont(ImFontPtr font)
    {
#if GRATIS_PREVIEW
        if (font.NativePtr == null || font.ContainerAtlas.NativePtr == null || font.ContainerAtlas.TexID == 0) return false;
#else
        if (font.Handle == null || font.ContainerAtlas.Handle == null || font.ContainerAtlas.Textures.Size == 0) return false;
        var atlas = font.ContainerAtlas;
        if (atlas.TextureIndexCommon < 0 || atlas.TextureIndexCommon >= atlas.Textures.Size || atlas.Textures[atlas.TextureIndexCommon].TexID == default) return false;
#endif
        foreach (var codepoint in "Am0?") {
            if (!TryGlyphBounds(font, codepoint, out var bounds)) return false;
#if GRATIS_PREVIEW
            var glyph = (float*)font.FindGlyphNoFallback(codepoint).NativePtr;
            var uv = new Vector4(glyph[6], glyph[7], glyph[8], glyph[9]);
#else
            var glyph = font.FindGlyphNoFallback(codepoint);
            var uv = new Vector4(glyph->U0, glyph->V0, glyph->U1, glyph->V1);
#endif
            if (!FontAtlasBudget.ValidGlyph(font.FontSize, bounds, uv)) return false;
        }
        return true;
    }

    public static void PushTexture(ImDrawListPtr list, DrawTexture texture) =>
        list.PushTextureID(texture.Handle);

    public static void PopTexture(ImDrawListPtr list) =>
        list.PopTextureID();

    public static void PushFontTexture(ImDrawListPtr list) =>
#if GRATIS_PREVIEW
        list.PushTextureID(ImGui.GetFont().ContainerAtlas.TexID);
#else
        list.PushTextureID(ImGui.GetFont().ContainerAtlas.Textures[ImGui.GetFont().ContainerAtlas.TextureIndexCommon].TexID);
#endif

    public static unsafe bool TryGlyphBounds(ImFontPtr font, char codepoint, out Vector4 bounds)
    {
#if GRATIS_PREVIEW
        var glyph = font.FindGlyphNoFallback(codepoint).NativePtr;
        if (glyph == null) { bounds = default; return false; }
        var metrics = (float*)glyph;
        bounds = new Vector4(metrics[2], metrics[3], metrics[4], metrics[5]);
#else
        var glyph = font.FindGlyphNoFallback(codepoint);
        if (glyph == null) { bounds = default; return false; }
        bounds = new Vector4(glyph->X0, glyph->Y0, glyph->X1, glyph->Y1);
#endif
        return true;
    }

    public static Vector2 MeasureText(float size, string text, float wrap = 0) =>
        ImGui.CalcTextSize(text, false, wrap > 0 ? wrap * ImGui.GetFontSize() / size : 0) * (size / ImGui.GetFontSize());

    public static Vector2 ContentRegionMax() =>
#if GRATIS_PREVIEW
        ImGui.GetCursorPos() + ImGui.GetContentRegionAvail();
#else
        ImGui.GetContentRegionMax();
#endif

    public static uint VertexIndex(ImDrawListPtr list) =>
#if GRATIS_PREVIEW
        list._VtxCurrentIdx;
#else
        list.VtxCurrentIdx;
#endif
}
