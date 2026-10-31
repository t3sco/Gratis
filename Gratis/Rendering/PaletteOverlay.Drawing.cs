using System;
using System.Numerics;
using Gratis.Controllers;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class PaletteOverlay {
    private static string PlaceStatus(PlaceResult result) {
        if (result.Place.Kind != PlaceKind.Aetheryte) return result.Place.Kind switch {
            PlaceKind.Zone => "Zone", PlaceKind.Quest => "Quest", PlaceKind.Marker => "Marker",
            PlaceKind.Flag => "Flag", PlaceKind.Pin => "Pin", PlaceKind.Action => "Action", _ => "Place",
        };
        if (result.Place.IsShard) return "Aethernet";
        return result.Access.Attuned ? "Attuned" : "Not yet attuned";
    }

    private readonly PlaceTooltip placeTooltip = new();
    private uint selectionMotionKey;
    public void Draw(MapDrawingConfig config) {
#if GRATIS_PREVIEW
        using var benchmark = BenchmarkProbe.Measure(BenchmarkSection.Palette);
#endif
        if (!config.IsGratis || !config.Palette)
            return;
        var alpha = UiMotion.Visibility(ImGui.GetID("##palette-motion"), IsOpen);
        if (alpha <= .001f && !IsOpen)
            return;
        Search(config);
        var scale = GratisTheme.ChromeScale;
        var (min, max) = ChromeLayout.SafeBounds();
        // The location card can be taller than the toolbar beside it: keep the panel below it instead of over it.
        var card = LocationMenuDrawing.CardBounds;
        var anchorBottom = FieldBounds.Z > FieldBounds.X ? FieldBounds.W : MapToolbarDrawing.LastBounds.W;
        if (card.Z > card.X && card.Y < anchorBottom && card.W > min.Y)
            min.Y = MathF.Min(max.Y, MathF.Max(min.Y, card.W + GratisTheme.Gap * GratisTheme.ChromeScale));
        var area = max - min;
        var rows = Math.Clamp(results.Length, 2, Math.Clamp(config.PaletteMaxResults, 2, 12));
        var questMaxHeight = area.Y / 3;
        var questHeight = PlaceSearch.Parse(query, scopes[scope]).Scope == PlaceKind.Quest
            ? QuestChips(config, scale, MathF.Min(940 * scale, area.X - 2 * GratisTheme.Gap * scale)
                - 2 * GratisTheme.Inset * scale, false, questMaxHeight) + GratisTheme.Gap * scale : 0;
        var size = Vector2.Min(new Vector2(940 * scale, (Home ? 740 : 118 + rows * 80) * scale + questHeight),
            Vector2.Max(Vector2.One, area - new Vector2(GratisTheme.Gap * 2 * scale)));
        var anchor = FieldBounds.Z > FieldBounds.X ? FieldBounds : MapToolbarDrawing.LastBounds;
        var bounds = ChromePopoverMath.Place(anchor, size, min, max, GratisTheme.Gap * scale);
        PanelMin = new(bounds.X, bounds.Y);
        PanelMax = new(bounds.Z, bounds.W);
        size = PanelMax - PanelMin;
        if (size.X < 100 * scale || size.Y < 64 * scale)
            return;
        using var font = MapDrawContext.Current.GetChromeFont((GratisTheme.TextSize + 2) * scale,
            ChromeFontRole.Body)?.Push();
        var list = ImGui.GetWindowDrawList();
        list.AddRectFilled(PanelMin + new Vector2(0, 7 * scale), PanelMax + new Vector2(0, 7 * scale),
            ImGui.GetColorU32(new Vector4(0, 0, 0, .25f)), GratisTheme.Radius.Popup * scale);
        ImGui.SetCursorScreenPos(PanelMin);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(GratisTheme.Inset) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(GratisTheme.Gap) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(12, 9) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, GratisTheme.Radius.Control * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 4 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, GratisTheme.Radius.Pill(new Vector2(4 * scale)));
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, GratisTheme.Radius.Inner(GratisTheme.Radius.Control * scale, 2 * scale));
        ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Primary);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, GratisTheme.Secondary);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(1, 1, 1, .055f));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1, 1, 1, .055f));
        try {
            if (BeginChild("##place-palette", size, ImGuiWindowFlags.NoBackground
                    | ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.NoScrollWithMouse
                    | (IsOpen ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoInputs))) {
                var surface = ImGui.GetWindowDrawList();
                using var reveal = UiMotion.RevealHeight(surface, PanelMin, PanelMax, alpha);
                using var panelMotion = new UiMotion.VisualScope(surface, PanelMin, alpha,
                    UiMotion.Full ? new Vector2(0, (1 - alpha) * 6 * scale) : Vector2.Zero,
                    UiMotion.Full ? .97f + .03f * alpha : 1);
                ImGui.BeginDisabled(!IsOpen);
                surface.PushClipRect(PanelMin, PanelMax, false);
                MapWindowDrawing.DrawSurface(surface, PanelMin, PanelMax, GratisTheme.Radius.Popup * scale,
                    surfaceOpacity: 1);
                surface.PopClipRect();
                if (IsOpen && ImGui.IsKeyPressed(ImGuiKey.Tab, false)) {
                    var current = Array.IndexOf(scopes, PlaceSearch.Parse(query, scopes[scope]).Scope);
                    ChangeScope((current + (ImGui.GetIO().KeyShift ? scopes.Length - 1 : 1)) % scopes.Length);
                }
                var effective = PlaceSearch.Parse(query, scopes[scope]).Scope;
                using (MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * scale,
                    ChromeFontRole.Chip)?.Push()) {
                    var compact = size.X < 490 * scale;
                    for (var i = 0; i < scopes.Length; i++) {
                        if (i > 0)
                            ImGui.SameLine();
                        var label = compact ? i == 0 ? "All" : "" : labels[i];
                        var chipWidth = compact ? (size.X - 2 * GratisTheme.Inset * scale - 4 * GratisTheme.Gap * scale)
                            / 5
                            : ImGui.CalcTextSize(label).X + ChipFaces.Regular.WidthPadding * scale;
                        var glyph = compact && i == 0 ? '\0' : i switch {
                            0 => GratisIcons.LayoutGrid,
                            1 => GratisIcons.Sparkles,
                            2 => GratisIcons.Compass,
                            3 => GratisIcons.BookOpen,
                            _ => GratisIcons.MapPin
                        };
                        if (SegmentedChip.Draw(scopeIds[i], label, effective == scopes[i],
                            new(ChipFaces.Regular, config.AccentColor,
                                new(MathF.Max(1, chipWidth), ChipFaces.Regular.Height * scale), scale,
                                Icon: glyph, Centered: compact, Tooltip: labels[i])))
                            ChangeScope(i);
                    }
                }
                Search(config);
                Array.Clear(questChipBounds);
                if (PlaceSearch.Parse(query, scopes[scope]).Scope == PlaceKind.Quest)
                    QuestChips(config, scale, ImGui.GetContentRegionAvail().X, true,
                        MathF.Min(questMaxHeight, ImGui.GetContentRegionAvail().Y / 2));
                Search(config);
                var footer = size.Y < 240 * scale || confirming == null && message.Length == 0 ? 0
                    : (confirming != null ? 56 : 44) * scale;
                var available = ImGui.GetContentRegionAvail();
                var shown = UiMotion.Page("##palette-page", viewRevision, out var viewAlpha, out var viewInput);
                var outgoing = shown != viewRevision;
                if (BeginChild("##place-results", new Vector2(0, MathF.Max(1, available.Y - footer)),
                    ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollWithMouse | (IsOpen && viewInput
                        ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoInputs))) {
                    UiMotion.Scroll();
                    using var revealRows = UiMotion.RevealHeight(ImGui.GetWindowDrawList(), PanelMin, PanelMax, alpha);
                    selectionMotionKey = ImGui.GetID("##palette-selection");
                    using var resultsMotion = UiMotion.Surface("##results-motion", ImGui.GetWindowPos(), IsOpen);
                    using var pageMotion = new UiMotion.VisualScope(ImGui.GetWindowDrawList(), ImGui.GetWindowPos(),
                        viewAlpha, UiMotion.Full ? new Vector2((1 - viewAlpha) * 6 * scale, 0) : Vector2.Zero);
                    var currentResults = results;
                    var currentExpansions = expansions;
                    var currentBrowserZones = browserZones;
                    var currentExpansion = expansion;
                    var currentScope = scope;
                    var currentOpen = IsOpen;
                    if (outgoing) {
                        results = previousResults;
                        expansions = previousExpansions;
                        browserZones = previousBrowserZones;
                        expansion = previousExpansion;
                        scope = previousScope;
                        IsOpen = false;
                    }
                    ImGui.PushID(shown);
                    if (outgoing ? previousHome : Home)
                        DrawBrowser(config, scale);
                    else {
                        if (IsOpen)
                            NavigateResults(config, size, scale);
                        if (results.Length == 0) {
                            EmptyState.Draw(source.Building ? "Indexing places..." : "No places found",
                                EmptyFaces.PaletteSearch);
                        }
                        for (var i = 0; i < results.Length; i++)
                            DrawRow(results[i], i, config, scale, size.X);
                    }
                    ImGui.PopID();
                    if (outgoing) {
                        results = currentResults;
                        expansions = currentExpansions;
                        browserZones = currentBrowserZones;
                        expansion = currentExpansion;
                        scope = currentScope;
                        IsOpen = currentOpen;
                    }
                    scrollSelection = false;
                }
                ImGui.EndChild();
                if (footer > 0) {
                    ImGui.Separator();
                    using var hint = MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * scale,
                        ChromeFontRole.Body)?.Push();
                    if (confirming is { } target) {
                        var clicked = ActionButton.Draw("Confirm teleport", "Confirm teleport",
                            new(ActionFaces.PrimaryTravel, Icon: GratisIcons.Compass));
                        ConfirmationCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
                        if (clicked)
                            Teleport(target, true);
                        if (size.X > 470 * scale) {
                            ImGui.SameLine();
                            ImGui.TextDisabled(message);
                        }
                    }
                    else
                        ImGui.TextUnformatted(ChromeControls.FitLabel(message, ImGui.GetContentRegionAvail().X));
                }
                ImGui.EndDisabled();
            }
            ImGui.EndChild();
        }
        finally { ImGui.PopStyleColor(5); ImGui.PopStyleVar(7); }
    }

    private float QuestChips(MapDrawingConfig config, float scale, float width, bool draw,
        float maxHeight = float.MaxValue) {
        if (draw && QuestChips(config, scale, width, false) > maxHeight) {
            var clipStart = ImGui.GetCursorScreenPos();
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            if (BeginChild("##quest-buckets", new(width, MathF.Max(1, maxHeight)), ImGuiWindowFlags.NoBackground))
                QuestChips(config, scale, ImGui.GetContentRegionAvail().X, true);
            ImGui.EndChild();
            ImGui.PopStyleVar();
            for (var i = 0; i < questChipBounds.Length; i++) {
                var bounds = questChipBounds[i];
                bounds = new(MathF.Max(bounds.X, clipStart.X), MathF.Max(bounds.Y, clipStart.Y),
                    MathF.Min(bounds.Z, clipStart.X + width), MathF.Min(bounds.W, clipStart.Y + maxHeight));
                questChipBounds[i] = bounds.Z > bounds.X && bounds.W > bounds.Y ? bounds : Vector4.Zero;
            }
            return maxHeight;
        }
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * scale,
            ChromeFontRole.Chip)?.Push();
        var start = draw ? ImGui.GetCursorScreenPos() : Vector2.Zero;
        var gap = GratisTheme.Grid * scale;
        var height = ChipFaces.Compact.Height * scale;
        width = MathF.Max(1, width);
        float x = 0, y = 0;
        for (var i = 0; i < questFilters.Length; i++) {
            if (i > 0 && questCounts[i] == 0)
                continue;
            var label = questChipLabels[i].Label(questLabels[i], questCounts[i]);
            var chipWidth = MathF.Min(width, TextDrawing.Measure(label).X + ChipFaces.Compact.WidthPadding * scale);
            if (x > 0 && x + chipWidth > width) {
                x = 0;
                y += height + gap;
            }
            if (draw) {
                ImGui.SetCursorScreenPos(start + new Vector2(x, y));
                if (SegmentedChip.Draw(questChipIds[i], label, questFilter == i,
                    new(ChipFaces.Compact, config.AccentColor, new(chipWidth, height), scale,
                        LabelCache: questChipLabels[i]))) {
                    questFilter = i;
                    focusSearch = true;
                }
                var chipMin = ImGui.GetItemRectMin();
                var chipMax = ImGui.GetItemRectMax();
                questChipBounds[i] = new(chipMin.X, chipMin.Y, chipMax.X, chipMax.Y);
            }
            x += chipWidth + gap;
        }
        if (draw) {
            ImGui.SetCursorScreenPos(start + new Vector2(0, y + height));
            ImGui.Dummy(Vector2.Zero);
        }
        return MathF.Min(maxHeight, y + height);
    }

    private void NavigateResults(MapDrawingConfig config, Vector2 size, float scale) {
        var page = Math.Max(1, (int)((size.Y / scale - 178) / 80));
        var previous = selected;
        if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
            selected++;
        if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
            selected--;
        if (ImGui.IsKeyPressed(ImGuiKey.PageDown))
            selected += page;
        if (ImGui.IsKeyPressed(ImGuiKey.PageUp))
            selected -= page;
        if (ImGui.IsKeyPressed(ImGuiKey.Home))
            selected = 0;
        if (ImGui.IsKeyPressed(ImGuiKey.End))
            selected = results.Length - 1;
        selected = Math.Clamp(selected, 0, Math.Max(0, results.Length - 1));
        if (previous != selected) {
            scrollSelection = true;
            confirming = null;
            message = "";
        }
        if (results.Length > 0 && ImGui.IsKeyPressed(ImGuiKey.Enter, false)) {
            if (ImGui.GetIO().KeyAlt && config.PaletteTeleportButton)
                Teleport(results[selected].Place, false);
            else
                Activate(results[selected].Place);
        }
    }

    private const float RowActionTop = 21;

    private void DrawRow(PlaceResult result, int index, MapDrawingConfig config, float scale, float width,
        bool browse = false) {
        ImGui.PushID(result.Place.Id);
        var start = ImGui.GetCursorScreenPos();
        var rowWidth = ImGui.GetContentRegionAvail().X;
        var rowHeight = 76 * scale;
        if (index == 0)
            FirstResultCenter = start + new Vector2(rowWidth / 2, rowHeight / 2);
        var canTravel = MapDrawContext.Current.Assistance?.CanOffer(result.Place) == true;
        var canTeleport = canTravel
            || config.PaletteTeleportButton && result.Place.Kind == PlaceKind.Aetheryte && !result.Place.IsShard
                && result.Access.Attuned;
        var buttonWidth = canTeleport && width >= 600 * scale ? 108 * scale : 0;
        if (ImGui.InvisibleButton("##show", new Vector2(MathF.Max(1, rowWidth - buttonWidth), rowHeight)))
            Activate(result.Place);
        var hovered = ImGui.IsItemHovered();
        if (hovered && !scrollSelection && ImGui.GetIO().MouseDelta.LengthSquared() > 0) {
            if (browse)
                browserSelection = index;
            else
                selected = index;
        }
        var active = index == (browse ? browserSelection : selected);
        if (scrollSelection && active)
            UiMotion.ScrollToItem();
        if (!ImGui.IsItemVisible()) {
            ImGui.SetCursorScreenPos(start + new Vector2(0, rowHeight + GratisTheme.Grid * scale));
            ImGui.Dummy(Vector2.Zero);
            ImGui.PopID();
            return;
        }
        var list = ImGui.GetWindowDrawList();
        UiMotion.ItemId("##show");
        using var rowMotion = UiMotion.Surface("##row-enter", start, IsOpen, index < 8 ? index * .02f : 0);
        using var control = UiMotion.Control(list, start, start + new Vector2(rowWidth, rowHeight));
        if (active) {
            var highlight = UiMotion.Selection(selectionMotionKey, start);
            list.AddRectFilled(highlight, highlight + new Vector2(rowWidth, rowHeight),
                ImGui.GetColorU32(config.AccentColor with { W = .12f }), GratisTheme.Radius.Control * scale);
        }
        PaletteRowDrawing.Background(list, start, new(rowWidth, rowHeight), config.AccentColor, active, hovered, scale);
        var imageWidth = config.PaletteImagery && width >= 460 * scale ? 90 * scale : 0;
        if (imageWidth > 0)
            DrawArt(result.Place, start + new Vector2(6, 6) * scale, new Vector2(imageWidth, rowHeight - 12 * scale),
                config, scale, GratisTheme.Radius.Inner(GratisTheme.Radius.Control * scale, 6 * scale));
        else
            DrawKindIcon(result.Place, list, start + new Vector2(21, 26) * scale, GratisTheme.IconSize * scale);
        var textMin = start + new Vector2(imageWidth > 0 ? imageWidth + 18 * scale : 40 * scale, 9 * scale);
        // Aetheryte rows carry no expansion name or chevron: the status pill alone sits on the action's centre line.
        var aetheryte = result.Place.Kind == PlaceKind.Aetheryte;
        var textMax = start + new Vector2(rowWidth - buttonWidth
            - (buttonWidth == 0 ? aetheryte ? 14 : 24 : 8) * scale, rowHeight);
        var text = rowNames.Text(result, results);
        var textWidth = MathF.Max(0, textMax.X - textMin.X);
        list.PushClipRect(textMin, Vector2.Max(textMin + Vector2.One, textMax), true);
        PaletteRowDrawing.TextPair(list, textMin, textWidth, text.Name,
            text.Distance.Length > 0 ? text.Distance : text.Identifier, scale, rowNames);
        PaletteRowDrawing.TextPair(list, textMin + new Vector2(0, 23 * scale), textWidth,
            text.Detail == text.Name ? "" : text.Detail, aetheryte ? "" : text.Expansion, scale, rowNames,
            secondary: true);
        using (MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * scale, ChromeFontRole.Body)?.Push()) {
            var status = PlaceStatus(result);
            var pillSize = Pill.Measure(status, PillFaces.PaletteRow, scale);
            var pillY = aetheryte ? start.Y + (RowActionTop + GratisTheme.ControlHeight / 2) * scale - pillSize.Y / 2
                : textMin.Y + 42 * scale;
            var pillMin = new Vector2(MathF.Max(textMin.X, textMax.X - pillSize.X), pillY);
            PaletteRowDrawing.Pill(list, pillMin, status,
                result.Place.Kind == PlaceKind.Aetheryte && result.Access.Attuned
                    ? GratisTheme.Transport : GratisTheme.Secondary, scale);
            TextDrawing.Draw(list, textMin + new Vector2(0, 43 * scale), ImGui.GetColorU32(GratisTheme.Secondary),
                rowNames.Fit(text.Coordinates, MathF.Max(0, pillMin.X - textMin.X - 12 * scale), secondary: true));
        }
        list.PopClipRect();
        if (hovered)
            placeTooltip.Show(text.Name, text.Detail, result.Place.Kind == PlaceKind.Aetheryte && !result.Access.Attuned
                ? "Not yet attuned" : text.Coordinates);
        if (buttonWidth == 0 && !aetheryte && rowWidth > 240 * scale) {
            var chevron = start + new Vector2(rowWidth - 13 * scale, rowHeight / 2);
            var colour = ImGui.GetColorU32(GratisTheme.Secondary);
            list.AddLine(chevron - new Vector2(3, 4) * scale, chevron + new Vector2(1, 0) * scale, colour, scale);
            list.AddLine(chevron + new Vector2(1, 0) * scale, chevron - new Vector2(3, -4) * scale, colour, scale);
        }
        if (buttonWidth > 0) {
            ImGui.SetCursorScreenPos(start + new Vector2(rowWidth - buttonWidth, RowActionTop * scale));
            using (MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * scale, ChromeFontRole.Chip)?.Push())
                if (DrawTravelAction(result, canTravel))
                    Teleport(result.Place, false);
        }
        ImGui.SetCursorScreenPos(start + new Vector2(0, rowHeight + GratisTheme.Grid * scale));
        ImGui.Dummy(Vector2.Zero);
        ImGui.PopID();
    }

    private bool DrawTravelAction(PlaceResult result, bool canTravel) {
        var label = canTravel ? "Travel here" : rowNames.ActionLabel(result, results);
        return ActionButton.Draw(label, label, new(ActionFaces.Action, Icon: GratisIcons.Compass));
    }
}
