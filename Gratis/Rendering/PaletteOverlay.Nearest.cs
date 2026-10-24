#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class PaletteOverlay
{
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
