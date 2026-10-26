using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class MapAssistance {
    private const float TeleportCardWidth = 280, CloseSize = 28, ConfirmDelay = .4f;

    public Vector2 TravelCloseCenter { get; private set; }

    private readonly record struct TeleportCardModel(string Name, string Meta, string Note, uint? Cost, bool Free,
        bool Armed, bool Enabled);

    internal static string TravelMeta(string subtitle, string name, string prefix = "") {
        var parts = subtitle.Split('>', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var start = parts.Length > 2 ? 1 : 0;
        var end = parts.Length > start && parts[^1] == name ? parts.Length - 1 : parts.Length;
        var region = string.Join(" · ", parts[start..end]);
        return prefix.Length == 0 ? region : region.Length == 0 ? prefix : prefix + " · " + region;
    }

    private static float TitleWrap(float width, float s) => width - 24 * s - 18 * s - CloseSize * s - 8 * s;

    private static Vector2 TeleportCardSize(string name, float maxWidth) {
        var s = GratisTheme.ChromeScale;
        var width = MathF.Min(TeleportCardWidth * s, maxWidth);
        var context = MapDrawContext.Current;
        float title, meta;
        using (context.GetChromeFont(GratisTheme.TravelTitleSize * s, ChromeFontRole.Title)?.Push())
            title = ImGui.CalcTextSize(name, false, TitleWrap(width, s)).Y;
        using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push())
            meta = ImGui.GetTextLineHeight();
        var height = 24 * s + MathF.Max(title, CloseSize * s) + 4 * s + meta + 12 * s + GratisTheme.ControlHeight * s;
        return new(width, MathF.Ceiling(height));
    }

    private static string CostLabel(TeleportCardModel card) => card.Free ? "Free"
        : card.Cost is { } gil ? $"{gil:N0} gil" : "";

    private bool DrawTeleportCard(TeleportCardModel card, out bool close) {
        var context = MapDrawContext.Current;
        var s = GratisTheme.ChromeScale;
        var list = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        float lineHeight;
        using (context.GetChromeFont(GratisTheme.TravelTitleSize * s, ChromeFontRole.Title)?.Push()) {
            lineHeight = ImGui.GetTextLineHeight();
            var crystal = start + new Vector2(5, lineHeight / 2) * s;
            list.AddQuad(crystal - new Vector2(0, 6) * s, crystal + new Vector2(4, 0) * s,
                crystal + new Vector2(0, 6) * s, crystal - new Vector2(4, 0) * s,
                ImGui.GetColorU32(GratisTheme.Crystal), s);
            ImGui.SetCursorScreenPos(start + new Vector2(18 * s, 0));
            ImGui.PushTextWrapPos(ImGui.GetCursorPos().X + TitleWrap(width + 24 * s, s));
            ImGui.TextUnformatted(card.Name);
            ImGui.PopTextWrapPos();
        }
        var titleBottom = ImGui.GetCursorScreenPos().Y;
        ImGui.SetCursorScreenPos(start + new Vector2(width - CloseSize * s, 0));
        close = ChromeControls.IconButton("##travel-close", GratisIcons.X, "Close", new Vector2(CloseSize * s));
        TravelCloseCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
        var rowBottom = MathF.Max(titleBottom, start.Y + CloseSize * s);
        ImGui.SetCursorScreenPos(new(start.X, rowBottom + 4 * s));
        using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push()) {
            var note = card.Note.Length > 0;
            var text = ChromeControls.FitLabel(note ? card.Note : card.Meta, width);
            ImGui.TextColored(note ? GratisTheme.Primary : GratisTheme.Secondary, text);
            ImGui.SetCursorScreenPos(new(start.X, ImGui.GetCursorScreenPos().Y + 12 * s));
        }
        ImGui.BeginDisabled(!card.Enabled);
        var pressed = TeleportAction(card, width);
        ImGui.EndDisabled();
        TravelButtonCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
        return pressed && card.Enabled;
    }

    private static bool TeleportAction(TeleportCardModel card, float width) {
        var s = GratisTheme.ChromeScale;
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(width, GratisTheme.ControlHeight * s);
        var clicked = ChromeControls.Button("##travel-action", size);
        var list = ImGui.GetWindowDrawList();
        using var motion = UiMotion.Control(list, pos, pos + size);
        var tint = card.Armed ? GratisTheme.WarmAccent : GratisTheme.Crystal;
        var hovered = ImGui.IsItemHovered();
        list.AddRectFilled(pos, pos + size, ImGui.GetColorU32(tint with { W = hovered ? .30f : .20f }),
            GratisTheme.ButtonRadius * s);
        list.AddRect(pos, pos + size, ImGui.GetColorU32(tint with { W = .45f }), GratisTheme.ButtonRadius * s,
            ImDrawFlags.None, s);
        ChromeControls.Feedback(list, pos, pos + size, fill: false);
        var label = card.Armed ? $"Pay {card.Cost ?? 0:N0} gil and teleport" : "Teleport";
        var cost = card.Armed ? "" : CostLabel(card);
        var costWidth = cost.Length > 0 ? ImGui.CalcTextSize(cost).X + 12 * s : 0;
        var y = (size.Y - ImGui.GetFontSize()) / 2;
        list.AddText(pos + new Vector2(14 * s, y),
            ImGui.GetColorU32(card.Armed ? GratisTheme.WarmAccent : GratisTheme.Primary),
            ChromeControls.FitLabel(label, size.X - 28 * s - costWidth));
        if (cost.Length > 0)
            list.AddText(pos + new Vector2(size.X - 14 * s - ImGui.CalcTextSize(cost).X, y),
                ImGui.GetColorU32(GratisTheme.WarmAccent), cost);
        return clicked;
    }
}
