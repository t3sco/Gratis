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
    public static readonly int[] Sizes = [8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 28, 32, 40, 56, 80, 128];
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
    public static Vector2 Measure(string text, float wrap = 0) => DrawBinding.MeasureText(NativeSize, text, wrap);

    public static void Draw(ImDrawListPtr list, Vector2 position, uint colour, string text, float wrap = 0)
    {
        lastNative = NativeSize;
        lastEffective = ImGui.GetFontSize();
        DrawBinding.PushFontTexture(list);
        list.AddText(ImGui.GetFont(), NativeSize, Snap(position), colour, text, wrap);
        DrawBinding.PopTexture(list);
    }
    private static float lastNative, lastEffective;
    public static string SampleDiagnostics() => LastDraw = $"lastNative/effective={lastNative:0.##}/{lastEffective:0.##} drawScale=1";
}
