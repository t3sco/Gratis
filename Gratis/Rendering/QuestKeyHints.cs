using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

internal static class QuestKeyHints {
    public static readonly PillProfile Cap = new() {
        Padding = new(10, 4), Inset = new(5, 2), Fill = GratisTheme.Primary with { W = .12f },
        Ink = GratisTheme.Primary, Rounding = PillRounding.Control,
    };

    public static void Bar(string leftKey, string leftAction, string rightKey, string rightAction, float width, float s) {
        var origin = ImGui.GetCursorScreenPos();
        var list = ImGui.GetWindowDrawList();
        var height = CapSize(leftKey, s).Y;
        var left = PairWidth(leftKey, leftAction, s);
        var right = PairWidth(rightKey, rightAction, s);
        Pair(list, origin, height, leftKey, leftAction, s);
        if (left + right + 12 * s <= width)
            Pair(list, origin + new Vector2(width - right, 0), height, rightKey, rightAction, s);
        ImGui.Dummy(new(width, height));
    }

    private static Vector2 CapSize(string key, float s) {
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push();
        return Pill.Measure(key, Cap, s);
    }

    private static float PairWidth(string key, string action, float s) =>
        CapSize(key, s).X + 6 * s + TextDrawing.Measure(action).X;

    private static void Pair(ImDrawListPtr list, Vector2 at, float height, string key, string action, float s) {
        var cap = CapSize(key, s);
        using (MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push())
            Pill.Draw(list, at, key, Cap, s);
        TextDrawing.Draw(list, at + new Vector2(cap.X + 6 * s, (height - TextDrawing.Measure(action).Y) / 2),
            ImGui.GetColorU32(GratisTheme.Secondary), action);
    }
}
