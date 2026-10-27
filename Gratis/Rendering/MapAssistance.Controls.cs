using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class MapAssistance {
    private static void CardHeading(string label, string name, string detail, string subtitle = "") {
        var context = MapDrawContext.Current;
        var s = GratisTheme.ChromeScale;
        using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push()) {
            var pos = ImGui.GetCursorScreenPos();
            var list = ImGui.GetWindowDrawList();
            var crystal = pos + new Vector2(5, 7) * s;
            list.AddQuad(crystal - new Vector2(0, 6) * s, crystal + new Vector2(4, 0) * s,
                crystal + new Vector2(0, 6) * s, crystal - new Vector2(4, 0) * s,
                ImGui.GetColorU32(GratisTheme.Crystal), s);
            ImGui.Dummy(new Vector2(18, 12) * s);
            ImGui.SameLine(0, 0);
            ImGui.TextColored(GratisTheme.Secondary, label);
            if (detail.Length > 0) {
                var x = ImGui.GetWindowWidth() - ImGui.GetStyle().WindowPadding.X - ImGui.CalcTextSize(detail).X;
                if (x > ImGui.GetCursorPosX() + 16 * s) {
                    ImGui.SameLine(x - 6 * s);
                    var chip = ImGui.GetCursorScreenPos();
                    var measured = ImGui.CalcTextSize(detail);
                    list.AddRectFilled(chip - new Vector2(4, 2) * s, chip + measured + new Vector2(4, 2) * s,
                        ImGui.GetColorU32(GratisTheme.WarmAccent with { W = .10f }),
                        GratisTheme.Radius.Pill(measured + new Vector2(8, 4) * s));
                    ImGui.TextColored(GratisTheme.Secondary, detail);
                }
            }
        }
        using (context.GetChromeFont(GratisTheme.TravelTitleSize * s, ChromeFontRole.Title)?.Push()) {
            var displayed = ChromeControls.FitLabel(name, ImGui.GetContentRegionAvail().X);
            ImGui.TextUnformatted(displayed);
            if (displayed != name)
                ChromeControls.Tooltip(name);
        }
        if (subtitle.Length > 0) {
            using var secondary = context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push();
            ImGui.TextColored(GratisTheme.Secondary,
                ChromeControls.FitLabel(subtitle, ImGui.GetContentRegionAvail().X));
        }
        ImGui.Spacing();
    }

    internal static bool QuietButton(string text) =>
        global::Gratis.Rendering.ActionButton.Draw(text, text, new(ActionFaces.Quiet));

    internal static bool ActionButton(string text, char icon) =>
        global::Gratis.Rendering.ActionButton.Draw(text, text, new(ActionFaces.Action, Icon: icon));

    internal static bool PrimaryTravelButton(string text, char icon) =>
        global::Gratis.Rendering.ActionButton.Draw(text, text, new(ActionFaces.PrimaryTravel, Icon: icon));
}
