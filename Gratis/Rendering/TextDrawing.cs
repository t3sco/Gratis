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
    public static readonly int[] Sizes = [10, 11, 12, 13, 14, 16, 18, 20, 24, 32, 48];
    public static int PixelSize(float requested)
    {
        if (!float.IsFinite(requested)) return 14;
        var best = Sizes[0];
        foreach (var size in Sizes)
            if (MathF.Abs(size - requested) < MathF.Abs(best - requested)) best = size;
        return best;
    }

    public static Vector2 Snap(Vector2 point) => new(MathF.Round(point.X), MathF.Round(point.Y));
    public static float NativeSize => ImGui.GetFont().FontSize;
    public static string LastDraw { get; private set; } = "lastDraw=none";
    public static Vector2 Measure(string text, float wrap = 0, float pixels = 0)
    {
        using var fallback = ContainsGlyphs(text) ? null : MapDrawContext.Current.PushDefaultTextFont();
        return DrawBinding.MeasureText(DrawSize(pixels), text, wrap);
    }
    private static float DrawSize(float requested) => float.IsFinite(requested) && requested > 48 && NativeSize >= 48 ? requested : NativeSize;

    public static void Draw(ImDrawListPtr list, Vector2 position, uint colour, string text, float wrap = 0, float pixels = 0)
    {
        using var fallback = ContainsGlyphs(text) ? null : MapDrawContext.Current.PushDefaultTextFont();
        lastNative = NativeSize;
        lastEffective = ImGui.GetFontSize();
        lastDrawSize = DrawSize(pixels);
        DrawBinding.PushFontTexture(list);
        list.AddText(ImGui.GetFont(), lastDrawSize, Snap(position), colour, text, wrap);
        DrawBinding.PopTexture(list);
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
