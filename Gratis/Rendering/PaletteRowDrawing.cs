using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class PaletteRowDrawing {
    public static void Background(ImDrawListPtr list, Vector2 min, Vector2 size, Vector4 accent,
        bool active, bool hovered, float scale) => list.AddRectFilled(min, min + size,
            ImGui.GetColorU32(active || hovered
                ? accent with { W = active ? .17f : .08f } : new Vector4(1, 1, 1, .025f)),
            GratisTheme.Radius.Control * scale);

    public static void Pill(ImDrawListPtr list, Vector2 min, string label, Vector4 ink, float scale) {
        var size = TextDrawing.Measure(label) + new Vector2(12, 3) * scale;
        list.AddRectFilled(min, min + size, ImGui.GetColorU32(GratisTheme.Primary with { W = .055f }),
            GratisTheme.Radius.Pill(size));
        TextDrawing.Draw(list, min + new Vector2(6, 1) * scale, ImGui.GetColorU32(ink), label);
    }
}
