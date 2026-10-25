using System;
using System.Linq;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
using SeString = Dalamud.Game.Text.SeStringHandling.SeString;
#endif
using Gratis.Rendering;

namespace Gratis.Classes;

public static partial class DrawHelpers {
#if !GRATIS_PREVIEW
    public static void DrawText(MarkerInfo marker, SeString text) => DrawText(marker, text.ToString());
#endif

    public static void DrawText(MarkerInfo marker, string text) {
        if (MapDrawContext.Current.Config.IsGratis) {
            DrawCrispText(marker, text);
            return;
        }
        using var largeFont = MapDrawContext.Current.PushLargeFont();
        ImGui.SetWindowFontScale(marker.Scale);

        var textSize = ImGui.CalcTextSize(text);
        var drawPosition = marker.Position + marker.Offset + ImGui.GetWindowPos() - textSize / 2.0f;

        drawPosition = new Vector2(MathF.Round(drawPosition.X), MathF.Round(drawPosition.Y));
        if (!MapShape.ContainsSymbol(drawPosition + textSize / 2, textSize / 2 + Vector2.One)) {
            ImGui.SetWindowFontScale(1.0f);
            return;
        }

        if (MapDrawContext.Current.Config.DebugMode) {
            ImGui.GetWindowDrawList().AddCircleFilled(marker.Position + marker.Offset + ImGui.GetWindowPos(), 5.0f,
                ImGui.GetColorU32(new Vector4(1, 0, 0, 1)));
            ImGui.GetWindowDrawList().AddRect(drawPosition, drawPosition + textSize,
                ImGui.GetColorU32(new Vector4(0, 128 / 255.0f, 0, 1)), 3.0f);
        }

        foreach (var x in Enumerable.Range(-1, 3)) {
            foreach (var y in Enumerable.Range(-1, 3)) {
                if (x is 0 && y is 0)
                    continue;

                ImGui.SetCursorScreenPos(drawPosition + new Vector2(x, y));
                ImGui.TextColored(new Vector4(0, 0, 0, 1), text);
            }
        }

        ImGui.SetCursorScreenPos(drawPosition);
        ImGui.TextColored(Vector4.One, text);

        ImGui.SetWindowFontScale(1.0f);
    }

    private static void DrawCrispText(MarkerInfo marker, string text) {
        var pixels = 96 * marker.Scale;
        using var font = MapDrawContext.Current.GetChromeFont(pixels, ChromeFontRole.Body)?.Push();
        var size = TextDrawing.Measure(text, pixels: pixels);
        var position = TextDrawing.Snap(marker.Position + marker.Offset + ImGui.GetWindowPos() - size / 2);
        if (!MapShape.ContainsSymbol(position + size / 2, size / 2 + Vector2.One))
            return;
        var list = ImGui.GetWindowDrawList();
        for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
                if (x != 0 || y != 0)
                    TextDrawing.Draw(list, position + new Vector2(x, y), ImGui.GetColorU32(new Vector4(0, 0, 0, .9f)),
                        text, pixels: pixels);
        TextDrawing.Draw(list, position, ImGui.GetColorU32(Vector4.One), text, pixels: pixels);
    }
}