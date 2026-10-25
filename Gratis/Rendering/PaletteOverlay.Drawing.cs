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
        if (result.Place.Kind != PlaceKind.Aetheryte) return result.Place.Kind.ToString();
        if (result.Place.IsShard) return "Aethernet";
        return result.Access.Attuned ? "Attuned" : "Not yet attuned";
    }

    private uint selectionMotionKey;
    public void Draw(MapDrawingConfig config) {
        if (!config.IsGratis || !config.Palette)
            return;
        var alpha = UiMotion.Visibility(ImGui.GetID("##palette-motion"), IsOpen);
        if (alpha <= .001f && !IsOpen)
            return;
        Search(config);
        var scale = GratisTheme.ChromeScale;
        var (min, max) = ChromeLayout.SafeBounds();
        var area = max - min;
        var rows = Math.Clamp(results.Length, 2, Math.Clamp(config.PaletteMaxResults, 2, 12));
        var size = Vector2.Min(new Vector2(940, Home ? 740 : 118 + rows * 80) * scale,
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
            ImGui.GetColorU32(new Vector4(0, 0, 0, .25f)), GratisTheme.CardRadius * scale);
        ImGui.SetCursorScreenPos(PanelMin);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(GratisTheme.Inset) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(GratisTheme.Gap) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(12, 9) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, GratisTheme.ButtonRadius * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 4 * scale);
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
                MapWindowDrawing.DrawSurface(surface, PanelMin, PanelMax, GratisTheme.CardRadius * scale,
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
                        var chosen = UiMotion.Value(ImGui.GetID("##scope-" + i), 6, effective == scopes[i] ? 1 : 0);
                        ImGui.PushStyleColor(ImGuiCol.Button,
                            Vector4.Lerp(new Vector4(1, 1, 1, .035f), config.AccentColor with { W = .25f }, chosen));
                        var label = compact ? i == 0 ? "All" : "" : labels[i];
                        var chipWidth = compact ? (size.X - 2 * GratisTheme.Inset * scale - 4 * GratisTheme.Gap * scale)
                            / 5
                            : ImGui.CalcTextSize(label).X + 44 * scale;
                        if (ChromeControls.TextButton("##scope-" + i,
                            new Vector2(MathF.Max(1, chipWidth), GratisTheme.ControlHeight * scale)))
                            ChangeScope(i);
                        var chipMin = ImGui.GetItemRectMin();
                        var chipMax = ImGui.GetItemRectMax();
                        if (!compact || i > 0) {
                            var glyph = i switch {
                                0 => GratisIcons.LayoutGrid,
                                1 => GratisIcons.Sparkles,
                                2 => GratisIcons.Compass,
                                3 => GratisIcons.BookOpen,
                                _ => GratisIcons.MapPin
                            };
                            GratisIcons.Draw(surface,
                                new Vector2(compact ? (chipMin.X + chipMax.X) / 2 : chipMin.X + 14 * scale,
                                    (chipMin.Y + chipMax.Y) / 2),
                                glyph, GratisTheme.Primary, 13 * scale);
                        }
                        var textSize = TextDrawing.Measure(label);
                        TextDrawing.Draw(surface,
                            new Vector2(compact ? (chipMin.X + chipMax.X - textSize.X) / 2 : chipMin.X + 28 * scale,
                                (chipMin.Y + chipMax.Y - textSize.Y) / 2),
                            ImGui.GetColorU32(GratisTheme.Primary), label);
                        ChromeControls.Tooltip(labels[i], windowBounds: MapToolbarDrawing.WindowBounds);
                        ImGui.PopStyleColor();
                    }
                }
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
                    var currentExpansion = expansion;
                    var currentOpen = IsOpen;
                    if (outgoing) {
                        results = previousResults;
                        expansions = previousExpansions;
                        expansion = previousExpansion;
                        IsOpen = false;
                    }
                    ImGui.PushID(shown);
                    if (outgoing ? previousHome : Home)
                        DrawBrowser(config, scale);
                    else {
                        if (IsOpen)
                            NavigateResults(config, size, scale);
                        if (results.Length == 0) {
                            ImGui.Spacing();
                            ImGui.TextUnformatted(source.Building ? "Indexing places..." : "No places found");
                            ImGui.TextWrapped("Try a zone name or choose another filter.");
                        }
                        for (var i = 0; i < results.Length; i++)
                            DrawRow(results[i], i, config, scale, size.X);
                    }
                    ImGui.PopID();
                    if (outgoing) {
                        results = currentResults;
                        expansions = currentExpansions;
                        expansion = currentExpansion;
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
                        var clicked = MapAssistance.PrimaryTravelButton("Confirm teleport", GratisIcons.Compass);
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
        finally { ImGui.PopStyleColor(5); ImGui.PopStyleVar(5); }
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

    private void DrawRow(PlaceResult result, int index, MapDrawingConfig config, float scale, float width) {
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
        if (hovered && !scrollSelection && ImGui.GetIO().MouseDelta.LengthSquared() > 0)
            selected = index;
        if (scrollSelection && index == selected)
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
        if (index == selected) {
            var highlight = UiMotion.Selection(selectionMotionKey, start);
            list.AddRectFilled(highlight, highlight + new Vector2(rowWidth, rowHeight),
                ImGui.GetColorU32(config.AccentColor with { W = .12f }), GratisTheme.ButtonRadius * scale);
        }
        list.AddRectFilled(start, start + new Vector2(rowWidth, rowHeight),
            ImGui.GetColorU32(index == selected || hovered
                ? config.AccentColor with { W = index == selected ? .17f : .08f } : new Vector4(1, 1, 1, .025f)),
            GratisTheme.ButtonRadius * scale);
        var imageWidth = config.PaletteImagery && width >= 460 * scale ? 90 * scale : 0;
        if (imageWidth > 0)
            DrawArt(result.Place, start + new Vector2(6, 6) * scale, new Vector2(imageWidth, rowHeight - 12 * scale),
                config, scale);
        else
            DrawKindIcon(result.Place, list, start + new Vector2(21, 26) * scale, GratisTheme.IconSize * scale);
        var textMin = start + new Vector2(imageWidth > 0 ? imageWidth + 18 * scale : 40 * scale, 9 * scale);
        var textMax = start + new Vector2(rowWidth - buttonWidth - 8 * scale, rowHeight);
        list.PushClipRect(textMin, Vector2.Max(textMin + Vector2.One, textMax), true);
        TextDrawing.Draw(list, textMin, ImGui.GetColorU32(GratisTheme.Primary),
            ChromeControls.FitLabel(PaletteBrowser.Name(result, results),
                MathF.Max(0, textMax.X - textMin.X)));
        using (MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * scale, ChromeFontRole.Body)?.Push()) {
            var secondaryInk = new Vector4(.72f, .72f, .72f, 1);
            TextDrawing.Draw(list, textMin + new Vector2(0, 23 * scale), ImGui.GetColorU32(secondaryInk),
                ChromeControls.FitLabel(PaletteBrowser.Detail(result.Place), textMax.X - textMin.X));
            var status = PlaceStatus(result);
            var metadata = result.Distance is { } distance ? $"{distance:0}y away" : result.Place.World is { } world
                ? $"World X {world.X:0}, Z {world.Y:0}" : "";
            var pillMin = textMin + new Vector2(0, 42 * scale);
            var pillSize = TextDrawing.Measure(status) + new Vector2(12, 3) * scale;
            list.AddRectFilled(pillMin, pillMin + pillSize, ImGui.GetColorU32(GratisTheme.Primary with { W = .055f }),
                pillSize.Y / 2);
            TextDrawing.Draw(list, pillMin + new Vector2(6, 1) * scale,
                ImGui.GetColorU32(result.Place.Kind == PlaceKind.Aetheryte && result.Access.Attuned
                    ? GratisTheme.Transport : GratisTheme.Secondary),
                status);
            TextDrawing.Draw(list, pillMin + new Vector2(pillSize.X + 8 * scale, scale),
                ImGui.GetColorU32(secondaryInk),
                ChromeControls.FitLabel(metadata, MathF.Max(0, textMax.X - pillMin.X - pillSize.X - 8 * scale)));
        }
        list.PopClipRect();
        if (hovered)
            ChromeControls.Tooltip(PaletteBrowser.Name(result, results)
                + (result.Place.Kind == PlaceKind.Aetheryte && !result.Access.Attuned ? " · Not yet attuned" : ""));
        if (buttonWidth == 0 && rowWidth > 240 * scale) {
            var chevron = start + new Vector2(rowWidth - 13 * scale, rowHeight / 2);
            var colour = ImGui.GetColorU32(GratisTheme.Secondary);
            list.AddLine(chevron - new Vector2(3, 4) * scale, chevron + new Vector2(1, 0) * scale, colour, scale);
            list.AddLine(chevron + new Vector2(1, 0) * scale, chevron - new Vector2(3, -4) * scale, colour, scale);
        }
        if (buttonWidth > 0) {
            ImGui.SetCursorScreenPos(start + new Vector2(rowWidth - buttonWidth, 21 * scale));
            using (MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * scale, ChromeFontRole.Chip)?.Push())
                if (MapAssistance.ActionButton(canTravel ? "Travel here" : result.Access.Cost is { } cost
                    ? $"{cost:N0} gil" : "Teleport",
                    GratisIcons.Compass))
                    Teleport(result.Place, false);
        }
        ImGui.SetCursorScreenPos(start + new Vector2(0, rowHeight + GratisTheme.Grid * scale));
        ImGui.Dummy(Vector2.Zero);
        ImGui.PopID();
    }
}