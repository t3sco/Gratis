using System;
using System.Globalization;
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
        var pressed = TeleportAction(card, width);
        TravelButtonCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
        return pressed && card.Enabled;
    }

    private readonly ActionLabelCache travelLabel = new();
    private uint? travelCost;
    private bool travelFree;
    private CultureInfo? travelCulture;
    private string travelCostLabel = "", travelConfirmLabel = "";

    private bool TeleportAction(TeleportCardModel card, float width) {
        var culture = CultureInfo.CurrentCulture;
        if (travelCost != card.Cost || travelFree != card.Free || !ReferenceEquals(travelCulture, culture)
            || !culture.IsReadOnly || !culture.NumberFormat.IsReadOnly) {
            travelCost = card.Cost;
            travelFree = card.Free;
            travelCulture = culture;
            travelCostLabel = CostLabel(card);
            travelConfirmLabel = $"Pay {card.Cost ?? 0:N0} gil and teleport";
        }
        return global::Gratis.Rendering.ActionButton.Draw("##travel-action", card.Armed ? travelConfirmLabel : "Teleport",
            new(card.Armed ? ActionFaces.ConfirmTravel : ActionFaces.Travel,
                TrailingText: card.Armed ? "" : travelCostLabel, Enabled: card.Enabled,
                Size: new(width, GratisTheme.ControlHeight * GratisTheme.ChromeScale), LabelCache: travelLabel));
    }
}
