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
                        ImGui.GetColorU32(GratisTheme.WarmAccent with { W = .10f }), GratisTheme.ButtonRadius * s);
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

    internal static bool QuietButton(string text) {
        var s = GratisTheme.ChromeScale;
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(ImGui.CalcTextSize(text).X + 16 * s, GratisTheme.ControlHeight * s);
        var clicked = ChromeControls.Button(text, size);
        var list = ImGui.GetWindowDrawList();
        using var motion = UiMotion.Control(list, pos, pos + size);
        ChromeControls.Feedback(list, pos, pos + size);
        list.AddText(pos + new Vector2(8 * s, (size.Y - ImGui.GetFontSize()) / 2),
            ImGui.GetColorU32(GratisTheme.Secondary), text);
        return clicked;
    }

    internal static bool ActionButton(string text, char icon) {
        var s = MapDrawContext.Current.GlobalScale;
        var min = ImGui.GetCursorScreenPos();
        var textSize = ImGui.CalcTextSize(text);
        var size = new Vector2(textSize.X + 38 * s, GratisTheme.ControlHeight * s);
        var clicked = ChromeControls.Button(text, size);
        var list = ImGui.GetWindowDrawList();
        using var motion = UiMotion.Control(list, min, min + size);
        list.AddRectFilled(min, min + size,
            ImGui.GetColorU32(new Vector4(.25f, .48f, .56f, ImGui.IsItemHovered() ? .5f : .27f)), 5 * s);
        GratisIcons.Draw(list, min + new Vector2(13 * s, size.Y / 2), icon, GratisTheme.Transport, 14 * s);
        ChromeControls.Feedback(list, min, min + size, fill: false);
        list.AddText(min + new Vector2(27 * s, (size.Y - ImGui.GetFontSize()) / 2),
            ImGui.GetColorU32(GratisTheme.Primary), text);
        return clicked;
    }

    internal static bool PrimaryTravelButton(string text, char icon) {
        var s = GratisTheme.ChromeScale;
        var min = ImGui.GetCursorScreenPos();
        var size = new Vector2(ImGui.CalcTextSize(text).X + 40 * s, GratisTheme.ControlHeight * s);
        var clicked = ChromeControls.Button(text, size);
        var list = ImGui.GetWindowDrawList();
        using var motion = UiMotion.Control(list, min, min + size);
        var ink = GratisTheme.Surface with { W = 1 };
        list.AddRectFilled(min, min + size,
            ImGui.GetColorU32(GratisTheme.Crystal with { W = ImGui.IsItemHovered() ? 1 : .94f }),
            GratisTheme.ButtonRadius * s);
        ChromeControls.Feedback(list, min, min + size, fill: false);
        GratisIcons.Draw(list, min + new Vector2(15 * s, size.Y / 2), icon, ink, GratisTheme.IconSize * s);
        list.AddText(min + new Vector2(30 * s, (size.Y - ImGui.GetFontSize()) / 2), ImGui.GetColorU32(ink), text);
        return clicked;
    }
}