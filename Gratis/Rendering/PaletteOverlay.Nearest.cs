using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class PaletteOverlay
{
    private static void DrawNearestDirection(ImDrawListPtr list, Vector2 center, Vector2 from, Vector2 to, float scale)
    {
        var delta = to - from;
        var colour = ImGui.GetColorU32(GratisTheme.Secondary);
        if (delta.LengthSquared() < .01f) { list.AddCircleFilled(center, 2 * scale, colour, 12); return; }
        var direction = Vector2.Normalize(delta); var side = new Vector2(-direction.Y, direction.X);
        var tip = center + direction * 5 * scale;
        list.AddLine(center - direction * 4 * scale, tip, colour, 1.3f * scale);
        list.AddLine(tip, center + direction * scale + side * 3 * scale, colour, 1.3f * scale);
        list.AddLine(tip, center + direction * scale - side * 3 * scale, colour, 1.3f * scale);
    }
    private void DrawNearestChips(float width, float scale)
    {
        if (query.Length != 0 || width < 270 * scale) return;
        using var font = MapDrawContext.Current.GetChromeFont(11 * scale, ChromeFontRole.Chip)?.Push();
        ImGui.TextDisabled("Nearest:");
        foreach (var thing in new[] { "inn", "market board", "aetheryte" }) {
            if (thing == "aetheryte" && width < 380 * scale) continue;
            ImGui.SameLine();
            if (ImGui.SmallButton(thing)) {
                query = "nearest " + thing;
                queryInputId = $"##place-query-{++openSerial}"; focusSearch = true;
            }
        }
    }
}
