using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static partial class MapToolbarDrawing {
    // The narrowest text region worth drawing: about four letters and an ellipsis. Narrower and the location hides.
    private const float MinLocationText = 48;
    private const string Hyphen = " ";
    // The widest the toolbar band grows to when it carries the location text.
    internal const float MaxLocationBandWidth = 760;
    private static int locationFrame = -1;
    private static MapDrawContext? locationContext;
    private static Vector4 locationBounds;
    private static string locationShown = "", locationFull = "";
    private static bool locationCut;
    private static readonly ActionLabelCache locationLabel = new();

    /// <summary>The title the window host passes in each frame; the Classic toolbar splits it into zone and place.</summary>
    internal static string LocationTitle { get; set; } = "";

    /// <summary>True when the location text belongs in the toolbar row rather than in its own card or title.</summary>
    /// <remarks>
    /// A circular map has no straight toolbar row to hold it, so it keeps the floating card. Classic only draws the
    /// title on the map when the window frame is hidden, so the same holds for the toolbar.
    /// </remarks>
    public static bool LocationInToolbar(MapDrawingConfig config) => config.LocationPlacement == LocationPlacement.InToolbar
        && config.ShowTitleOnMap && !config.MapCircle && (config.IsGratis || config.HideWindowFrame);

    private static bool LocationCurrent => locationFrame == ImGui.GetFrameCount()
        && ReferenceEquals(locationContext, MapDrawContext.Current);

    /// <summary>Screen bounds of the location region this frame, or zero when it is hidden or not in the toolbar.</summary>
    public static Vector4 LocationBounds => LocationCurrent ? locationBounds : Vector4.Zero;

    /// <summary>The text actually drawn this frame (zone, a separator and the place, or just the place).</summary>
    public static string LocationShown => LocationCurrent ? locationShown : "";

    /// <summary>The full, uncut text, which the tooltip carries when the drawn text is cut.</summary>
    public static string LocationFull => LocationCurrent ? locationFull : "";

    public static bool LocationCut => LocationCurrent && locationCut;

    private static void BeginLocation() {
        locationFrame = -1;
        locationBounds = Vector4.Zero;
        locationShown = locationFull = "";
        locationCut = false;
    }

    private static void Located(Vector4 bounds, string shown, string full, bool cut) {
        locationFrame = ImGui.GetFrameCount();
        locationContext = MapDrawContext.Current;
        locationBounds = bounds;
        locationShown = shown;
        locationFull = full;
        locationCut = cut;
    }

    // Picks what fits: zone and place, then the place alone, then the place cut with an ellipsis. Returns false when
    // not even a cut place fits, so the location hides instead of overlapping a button.
    private static bool FitLocation(float lead, float separator, float place, float available, string primary,
        out bool showLead, out string fitted) {
        showLead = false;
        fitted = primary;
        if (lead > 0 && lead + separator + place <= available + .5f) {
            showLead = true;
            return true;
        }
        if (place <= available + .5f)
            return true;
        fitted = locationLabel.Fit(primary, available);
        return fitted.Length > 1;
    }

    // Gratis: one line in the toolbar band, the zone small and quiet and the place emphasised, like the floating card
    // folded onto a single row. `left` and `right` are screen x positions of the free gap between the button groups.
    private static void DrawGratisLocation(ImDrawListPtr list, MapDrawContext context, float scale, Vector2 origin,
        float left, float right) {
        var (region, secondary, primary) = BottomCardsDrawing.ToolbarLines(context.Config);
        if (primary.Length == 0)
            return;
        var pad = 8 * scale;
        var available = right - left - pad - MathF.Max(pad, LocationMenuDrawing.UpReserve(scale));
        if (available < MinLocationText * scale)
            return;
        var lead = region.Length > 0 ? secondary.Length > 0 ? region + " - " + secondary : region : secondary;
        float leadWidth = 0, placeWidth, bodySize, separator;
        // Zone and place share one type size so the line reads as a single piece of text; only the colour differs.
        using (context.GetChromeFont(GratisTheme.TextSize * scale, ChromeFontRole.Body)?.Push()) {
            if (lead.Length > 0)
                leadWidth = ImGui.CalcTextSize(lead).X;
            placeWidth = ImGui.CalcTextSize(primary).X;
            separator = 10 * scale;
            bodySize = ImGui.GetFontSize();
        }
        bool showLead;
        string fitted;
        using (context.GetChromeFont(GratisTheme.TextSize * scale, ChromeFontRole.Body)?.Push()) {
            if (!FitLocation(leadWidth, separator, placeWidth, available, primary, out showLead, out fitted))
                return;
        }
        var top = origin.Y + 4 * scale;
        var height = GratisTheme.ControlHeight * scale;
        var centre = top + height / 2;
        var x = left + pad;
        using (context.GetChromeFont(GratisTheme.TextSize * scale, ChromeFontRole.Body)?.Push()) {
            var y = MathF.Round(centre - bodySize / 2);
            if (showLead) {
                list.AddText(new(x, y), ImGui.GetColorU32(GratisTheme.Secondary), lead);
                x += leadWidth + separator;
            }
            list.AddText(new(x, y), ImGui.GetColorU32(GratisTheme.Primary), fitted);
        }
        var cut = !showLead && lead.Length > 0 || fitted != primary;
        var full = lead.Length > 0 ? lead + Hyphen + primary : primary;
        var card = new Vector4(left, top, right, top + height);
        Located(card, showLead ? lead + Hyphen + fitted : fitted, full, cut);
        // The menu and its tooltip live on the card exactly as they do for the floating location: the tooltip appears
        // only when text is cut, or when there is weather or aether progress to add.
        ImGui.SetCursorScreenPos(new(left, top));
        LocationMenuDrawing.Draw(card, primary, region, secondary, cut);
    }

    // Classic: the same single line, drawn with the title colours and font the floating title uses.
    private static void DrawClassicLocation(MapDrawContext context, float scale, float top, float height, float left,
        float right) {
        var config = context.Config;
        var (secondary, primary) = MapWindowDrawing.TitleParts(LocationTitle, config);
        if (primary.Length == 0)
            return;
        var pad = 6 * scale;
        var available = right - left - pad * 2;
        if (available < MinLocationText * scale)
            return;
        var baseSize = ImGui.GetFontSize();
        var handle = context.GetTitleFont(config.TitleFont, baseSize);
        float leadWidth = 0, placeWidth, fontSize;
        float separator;
        bool showLead;
        string fitted;
        using (handle?.Push()) {
            separator = 10 * scale;
            if (secondary.Length > 0)
                leadWidth = ImGui.CalcTextSize(secondary).X;
            placeWidth = ImGui.CalcTextSize(primary).X;
            fontSize = ImGui.GetFontSize();
            if (!FitLocation(leadWidth, separator, placeWidth, available, primary, out showLead, out fitted))
                return;
        }
        var list = ImGui.GetWindowDrawList();
        var centre = top + height / 2;
        var x = left + pad;
        var bold = new Vector2(config.TitleBold, 0);
        void Text(string text, Vector4 colour) {
            var at = new Vector2(x, MathF.Round(centre - fontSize / 2));
            var ink = ImGui.ColorConvertFloat4ToU32(colour);
            list.AddText(at, ink, text);
            if (config.TitleBold > 0)
                list.AddText(at + bold, ink, text);
        }
        using (handle?.Push()) {
            if (showLead) {
                Text(secondary, config.TitleSecondaryColor);
                x += leadWidth + separator;
            }
            Text(fitted, config.TitleColor);
        }
        var cut = !showLead && secondary.Length > 0 || fitted != primary;
        var full = secondary.Length > 0 ? secondary + Hyphen + primary : primary;
        Located(new(left, top, right, top + height), showLead ? secondary + Hyphen + fitted : fitted, full, cut);
        var restore = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new(left, top));
        ImGui.Dummy(new(right - left, height));
        if (cut && ImGui.IsItemHovered())
            ImGui.SetTooltip(full);
        ImGui.SetCursorScreenPos(restore);
    }
}
