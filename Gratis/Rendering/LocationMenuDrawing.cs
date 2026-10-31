using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Gratis.Controllers;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public readonly record struct LocationMenuParent(string Name, Action Open);

public interface ILocationMenuSource {
    IPaletteSource Places { get; }
    IReadOnlyList<LocationMenuParent> Parents { get; }
}

public static class LocationMenuDrawing {
    private const string Popup = "Gratis_Location_Menu";
    private const float UpSize = 28, UpInset = GratisTheme.Gap;
    private static string upName = "", upTooltip = "";
    private static Vector4 upBounds;
    private static readonly Dictionary<string, long> Recent = new();
    private static MapDrawContext? context;
    private static int frame = -2, serial, revision = -1;
    private static uint map;
    private static bool open, focus, swallowEscape, closing;
    private static string query = "", searched = "", region = "";
    private static string queryInputId = "##location-query-0";
    private static PlaceEntry[] zones = [], siblings = [];
    private static string[] siblingLabels = [], resultLabels = [];
    private static PlaceResult[] results = [];
    private static IReadOnlyList<LocationMenuParent> parents = [];
    private static readonly List<string> parentIds = [];
    private static readonly Dictionary<string, MenuRowCache> rowCache = [];
    private static readonly List<(string Id, Vector4 Bounds)> rows = [];
    public static IReadOnlyList<(string Id, Vector4 Bounds)> Rows => rows;
    public static IReadOnlyList<PlaceResult> Results => results;
    public static string Query => query;
    public static Vector4 CardBounds { get; private set; }
    public static Vector4 UpBounds => Current ? upBounds : Vector4.Zero;
    public static Vector4 PopoverBounds { get; private set; }
    public static Vector4 SearchBounds { get; private set; }
    private static bool Current => ReferenceEquals(context, MapDrawContext.Current)
        && MapDrawContext.Current.Config.IsGratis && !MapDrawContext.Current.IsPvP
        && frame >= ImGui.GetFrameCount() - 1;
    public static bool IsOpen => Current && open;
    public static bool CapturesKeyboard => Current && (open || closing || swallowEscape && ImGui.IsKeyDown(ImGuiKey.Escape));
    public static bool InputActive => CapturesKeyboard || Current && MapDrawContext.Current.LocationMenu != null
        && ImGui.IsMouseHoveringRect(new(CardBounds.X, CardBounds.Y), new(CardBounds.Z, CardBounds.W), false);

    public static void PrepareInput() {
        if (!Current)
            return;
        if (open)
            MapDrawContext.Current.Chrome.Reveal(ImGui.GetTime());
        if (CapturesKeyboard)
            ImGui.SetNextFrameWantCaptureKeyboard(true);
    }

    public static void Draw(Vector4 card, string fullText, string overline = "", string detail = "") {
        var current = MapDrawContext.Current;
        if (!ReferenceEquals(context, current)) {
            context = current;
            open = focus = closing = swallowEscape = false;
            revision = -1;
            rowCache.Clear();
        }
        frame = ImGui.GetFrameCount();
        closing = false;
        swallowEscape &= ImGui.IsKeyDown(ImGuiKey.Escape);
        CardBounds = card;
        PopoverBounds = SearchBounds = upBounds = Vector4.Zero;
        rows.Clear();
        if (!current.Config.IsGratis || current.IsPvP || current.LocationMenu is not { } source) {
            open = false;
            ImGui.Dummy(new(card.Z - card.X, card.W - card.Y));
            if (ImGui.IsItemHovered())
                UiMotion.TooltipText(fullText);
            return;
        }
        // Submitted before the card button: ImGui gives hover to the first of two overlapping items, so the
        // up button wins its own corner and the rest of the card still opens the dropdown.
        var clickedUp = false;
        if (TryGetUp(out var up)) {
            var s = GratisTheme.ChromeScale;
            var size = new Vector2(UpSize * s);
            var origin = ImGui.GetCursorScreenPos();
            var at = origin + new Vector2(card.Z - card.X - (UpInset + UpSize) * s, (card.W - card.Y - size.Y) / 2);
            ImGui.SetCursorScreenPos(at);
            clickedUp = ChromeControls.IconButton("##location-up", GratisIcons.ArrowUp, UpTooltip(up.Name), size, s);
            upBounds = new(at.X, at.Y, at.X + size.X, at.Y + size.Y);
            ImGui.SetCursorScreenPos(origin);
        }
        var clicked = ChromeControls.Button("##location-card", new(card.Z - card.X, card.W - card.Y));
        ChromeControls.Feedback(ImGui.GetWindowDrawList(), new(card.X, card.Y), new(card.Z, card.W), IsOpen,
            radius: GratisTheme.Radius.Card * GratisTheme.ChromeScale);
        if (!open)
            ChromeControls.Tooltip(fullText, meta: string.Join("\n",
                new[] { overline, detail, "Browse this region" }.Where(line => line.Length > 0)));
        if (clicked && !open) {
            query = searched = "";
            revision = -1;
            serial++;
            queryInputId = "##location-query-" + serial;
            focus = open = true;
            source.Places.Refresh();
            parents = source.Parents;
            parentIds.Clear();
            ImGui.OpenPopup(Popup);
        }
        if (clickedUp)
            up.Open();
        DrawPopover(source, clicked && !focus || clickedUp);
        PrepareInput();
    }

    // Space the card reserves on its right for the up button: its inset, size and a gap to the text.
    public static float UpReserve(float scale) => TryGetUp(out _) ? (UpInset + UpSize + 4) * scale : 0;

    private static bool TryGetUp(out LocationMenuParent up) {
        var current = MapDrawContext.Current;
        if (current.Config.IsGratis && !current.IsPvP && current.LocationMenu is { } source
            && source.Parents is { Count: > 0 } list) {
            up = list[0];
            return true;
        }
        up = default;
        return false;
    }

    internal static string UpTooltip(string name) {
        if (!string.Equals(name, upName, StringComparison.Ordinal)) {
            upName = name;
            upTooltip = "Up to " + name;
        }
        return upTooltip;
    }

    private static void Search(IPaletteSource source) {
        source.Refresh();
        if (revision != source.Revision || map != source.Context.MapId) {
            revision = source.Revision;
            map = source.Context.MapId;
            zones = source.Entries.Where(p => p.Kind == PlaceKind.Zone).GroupBy(p => p.MapId)
                .Select(g => g.First()).ToArray();
            var current = zones.FirstOrDefault(p => p.MapId == map);
            region = current == null ? "" : Region(current);
            // Several maps can share a name (floors, story phases); list each distinct name and layer once.
            siblings = current == null || region.Length == 0 ? [] : zones.Where(p => p.MapId != map
                && p.Expansion == current.Expansion && Region(p) == region
                && (p.Name != current.Name || p.Layer != current.Layer))
                .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(p => p.MapId)
                .DistinctBy(p => (p.Name, p.Layer)).ToArray();
            siblingLabels = Labels(siblings);
            searched = "\0";
        }
        if (searched == query)
            return;
        searched = query;
        rowCache.Clear();
        results = string.IsNullOrWhiteSpace(query) ? []
            : PlaceSearch.Find(zones, query, PlaceKind.Zone, source.Context, Recent, source.Access)
                .DistinctBy(r => (r.Place.Name, r.Place.Layer)).Take(6).ToArray();
        resultLabels = Labels(results.Select(r => r.Place).ToArray());
    }

    // The layer is only shown where it is what tells two rows apart.
    private static string[] Labels(PlaceEntry[] places) => places.Select(p => p.Layer.Length > 0
        && places.Count(o => o.Name == p.Name) > 1 ? p.Name + " · " + p.Layer : p.Name).ToArray();

    private static string Region(PlaceEntry place) {
        var parts = place.Subtitle.Split('>', StringSplitOptions.TrimEntries);
        var index = parts.Length > 1 && parts[0] == place.ExpansionName ? 1 : 0;
        return parts.Length > index + 1 ? parts[index] : "";
    }

    private static void DrawPopover(ILocationMenuSource source, bool close) {
        var current = MapDrawContext.Current;
        var s = GratisTheme.ChromeScale;
        using var style = PopupSurface.Push(PopupFaces.Chrome);
        using var font = current.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push();
        if (open)
            Search(source.Places);
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var stride = GratisTheme.ControlHeight * s + spacing;
        var padding = ImGui.GetStyle().WindowPadding.Y;
        var header = ImGui.GetTextLineHeight() + spacing;
        var searchHeight = results.Length * stride;
        if (!string.IsNullOrWhiteSpace(query) && results.Length == 0)
            searchHeight = header;
        var parentHeight = parents.Count == 0 ? 0 : header + parents.Count * stride;
        var regionHeight = siblings.Length == 0 ? 0 : header + Math.Min(8, siblings.Length) * stride - spacing;
        var height = 2 * padding + ImGui.GetFrameHeight() + searchHeight
            + (parentHeight > 0 ? spacing + parentHeight : 0) + (regionHeight > 0 ? spacing + regionHeight : 0);
        var (min, max) = ChromeLayout.PopoverBounds();
        min.X = MathF.Min(min.X, CardBounds.X);
        var bounds = ChromePopoverMath.Place(CardBounds, new(288 * s, MathF.Ceiling(height)), min, max,
            GratisTheme.Gap * s, false);
        var size = new Vector2(bounds.Z - bounds.X, bounds.W - bounds.Y);
        var escape = open && ImGui.IsKeyPressed(ImGuiKey.Escape, false);
        using var popup = UiMotion.Popup(Popup, !close && !escape, new(bounds.X, bounds.Y), size,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        open = popup.Interactive;
        if (escape) {
            swallowEscape = closing = true;
            ImGui.SetNextFrameWantCaptureKeyboard(true);
        }
        if (!popup.Visible)
            return;
        PopoverBounds = bounds;
        if (popup.Interactive) {
            current.Chrome.Reveal(ImGui.GetTime());
            ImGui.SetNextFrameWantCaptureKeyboard(true);
        }
        UiMotion.Scroll();
        if (focus && popup.Interactive) {
            ImGui.SetKeyboardFocusHere();
            focus = false;
        }
        var enter = TextField.Draw(queryInputId, ref query, new(FieldFaces.Location)).Changed;
        SearchBounds = Bounds();
        Search(source.Places);
        if (enter && results.Length > 0 && popup.Interactive) {
            source.Places.Show(results[0].Place);
            Close();
        }
        var remaining = ImGui.GetContentRegionAvail().Y;
        if (remaining <= 0)
            return;
        var constrained = size.Y + .5f < height;
        if (constrained)
            BeginList("##location-lists", new(0, remaining));
        try {
            for (var i = 0; i < results.Length; i++) {
                var result = results[i];
                if (Row(resultLabels[i], result.Place.Id)) {
                    source.Places.Show(result.Place);
                    Close();
                }
            }
            if (!string.IsNullOrWhiteSpace(query) && results.Length == 0)
                EmptyState.Draw("No zones found", EmptyFaces.Location);
            if (parents.Count > 0) {
                ImGui.TextDisabled("Up a level");
                for (var i = 0; i < parents.Count; i++) {
                    if (i == parentIds.Count) parentIds.Add("parent:" + i);
                    if (Row(parents[i].Name, parentIds[i])) {
                        parents[i].Open();
                        Close();
                    }
                }
            }
            if (siblings.Length > 0) {
                ImGui.TextDisabled(ChromeControls.FitLabel("In " + region, ImGui.GetContentRegionAvail().X));
                var listHeight = MathF.Min(Math.Min(8, siblings.Length) * stride - spacing,
                    MathF.Max(stride, ImGui.GetContentRegionAvail().Y));
                BeginList("##location-region", new(0, listHeight));
                try {
                    for (var i = 0; i < siblings.Length; i++)
                        if (Row(siblingLabels[i], siblings[i].Id)) {
                            source.Places.Show(siblings[i]);
                            Close();
                        }
                }
                finally { ImGui.EndChild(); }
            }
        }
        finally {
            if (constrained)
                ImGui.EndChild();
        }
    }

    private static void BeginList(string id, Vector2 size) {
        var bar = UiMotion.ScrollbarBegin(id);
#if GRATIS_PREVIEW
        var visible = ImGui.BeginChild(id, size, ImGuiChildFlags.None);
#else
        var visible = ImGui.BeginChild(id, size, false);
#endif
        UiMotion.ScrollbarEnd(bar, visible);
    }

    private static Vector4 Bounds() {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        return new(min.X, min.Y, max.X, max.Y);
    }

    private static bool Row(string label, string id) {
        if (!rowCache.TryGetValue(id, out var cached)) {
            cached = new("##location-" + id);
            rowCache.Add(id, cached);
        }
        var row = new MenuRow(cached, new(RowFaces.Location, GratisTheme.ChromeScale));
        if (ImGui.IsItemVisible())
            rows.Add((id, Bounds()));
        row.Draw(label);
        return row.Clicked;
    }

    private static void Close() {
        open = false;
        closing = true;
        ImGui.CloseCurrentPopup();
    }
}
