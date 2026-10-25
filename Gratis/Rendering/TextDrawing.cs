using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class TextDrawing
{
    public static readonly int[] Sizes = [11, 12, 13, 14, 16, 20, 24];
    public static readonly int[] IconSizes = [10, 11, 12, 13, 14, 16, 18, 20, 24, 32, 48];
    public static int PixelSize(float requested) => Nearest(requested, Sizes);
    public static int IconPixelSize(float requested) => Nearest(requested, IconSizes);
    private static int Nearest(float requested, int[] sizes)
    {
        if (!float.IsFinite(requested)) return 14;
        var best = sizes[0];
        foreach (var size in sizes)
            if (MathF.Abs(size - requested) < MathF.Abs(best - requested)) best = size;
        return best;
    }

    public static Vector2 Snap(Vector2 point) => new(MathF.Round(point.X), MathF.Round(point.Y));
    public static float NativeSize => ImGui.GetFont().FontSize;
    public static string LastDraw { get; private set; } = "lastDraw=none";
    public static Vector2 Measure(string text, float wrap = 0, float pixels = 0, bool tabularDigits = false)
    {
        using var fallback = ContainsGlyphs(text) ? null : MapDrawContext.Current.PushDefaultTextFont();
        return tabularDigits && MapDrawContext.Current.Config.IsGratis ? Tabular(default, Vector2.Zero, 0, text, DrawSize(pixels), false) : DrawBinding.MeasureText(DrawSize(pixels), text, wrap);
    }
    private static float DrawSize(float requested) => float.IsFinite(requested) && requested > 24 && NativeSize >= 24 ? requested : NativeSize;

    public static void Draw(ImDrawListPtr list, Vector2 position, uint colour, string text, float wrap = 0, float pixels = 0, bool tabularDigits = false)
    {
        using var fallback = ContainsGlyphs(text) ? null : MapDrawContext.Current.PushDefaultTextFont();
        lastNative = NativeSize;
        lastEffective = ImGui.GetFontSize();
        lastDrawSize = DrawSize(pixels);
        DrawBinding.PushFontTexture(list);
        if (tabularDigits && MapDrawContext.Current.Config.IsGratis) Tabular(list, Snap(position), colour, text, lastDrawSize, true);
        else list.AddText(ImGui.GetFont(), lastDrawSize, Snap(position), colour, text, wrap);
        DrawBinding.PopTexture(list);
    }
    private static readonly string[] Latin = Characters(0, 256), Punctuation = Characters(0x2000, 112);
    private static string[] Characters(int first, int count)
    {
        var result = new string[count];
        for (var i = 0; i < count; i++) result[i] = ((char)(first + i)).ToString();
        return result;
    }
    private static Vector2 Tabular(ImDrawListPtr list, Vector2 origin, uint colour, string text, float size, bool draw)
    {
        if (text.Length == 0) return Vector2.Zero;
        var font = ImGui.GetFont(); var ratio = size / NativeSize;
        var cell = 0f;
        for (var digit = '0'; digit <= '9'; digit++) cell = MathF.Max(cell, DrawBinding.GlyphAdvance(font, digit) * ratio);
        cell = MathF.Ceiling(cell);
        var x = 0f; var y = 0f; var width = 0f;
        foreach (var c in text) {
            if (c == '\r') continue;
            if (c == '\n') { width = MathF.Max(width, x); x = 0; y += size; continue; }
            var advance = DrawBinding.GlyphAdvance(font, c) * ratio;
            var digit = c is >= '0' and <= '9';
            if (draw) {
                var glyph = c < 256 ? Latin[c] : c is >= '\u2000' and <= '\u206f' ? Punctuation[c - 0x2000] : c.ToString();
                list.AddText(font, size, Snap(origin + new Vector2(x + (digit ? (cell - advance) / 2 : 0), y)), colour, glyph);
            }
            x += digit ? cell : advance;
        }
        return new(MathF.Max(width, x), y + size);
    }
    private static bool ContainsGlyphs(string text)
    {
        foreach (var glyph in text)
            if (!char.IsControl(glyph) && !DrawBinding.TryGlyphBounds(ImGui.GetFont(), glyph, out _)) return false;
        return true;
    }
    private static float lastNative, lastEffective, lastDrawSize;
    public static string SampleDiagnostics() => LastDraw = $"lastNative/effective={lastNative:0.##}/{lastEffective:0.##} drawScale={(lastNative > 0 ? lastDrawSize / lastNative : 1):0.###}";
}
