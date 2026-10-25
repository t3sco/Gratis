using System;
using System.Numerics;
using Gratis.Controllers;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class PaletteOverlay
{
    public void Draw(MapDrawingConfig config)
    {
        if (!IsOpen || !config.IsGratis || !config.Palette) return;
        Search(config);
        var scale = GratisTheme.ChromeScale;
        var (min, max) = Classes.MapShape.Bounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        var area = max - min;
        if (config.MapCircle) { var inset = area * ((1 - .70710678f) / 2); min += inset; max -= inset; area = max - min; }
        var rows = Math.Clamp(results.Length, 2, Math.Clamp(config.PaletteMaxResults, 2, 12));
        var size = Vector2.Min(new Vector2(940, Home ? 740 : 178 + rows * 80) * scale,
            Vector2.Max(Vector2.One, area - new Vector2(GratisTheme.Gap * 2 * scale)));
        PanelMin = min + (area - size) / 2; PanelMax = PanelMin + size;
        if (size.X < 150 * scale || size.Y < 112 * scale) return;
        using var font = MapDrawContext.Current.GetChromeFont((GratisTheme.TextSize + 2) * scale, ChromeFontRole.Body)?.Push();
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
            if (BeginChild("##place-palette", size, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)) {
                var surface = ImGui.GetWindowDrawList();
                surface.PushClipRect(PanelMin, PanelMax, false);
                MapWindowDrawing.DrawSurface(surface, PanelMin, PanelMax, GratisTheme.CardRadius * scale, surfaceOpacity: 1);
                surface.PopClipRect();
                if (size.Y >= 350 * scale) {
                    using var title = MapDrawContext.Current.GetChromeFont((GratisTheme.TextSize + 10) * scale, ChromeFontRole.Title)?.Push();
                    ImGui.TextUnformatted("Where will you venture?");
                }
                if (focusSearch) { ImGui.SetKeyboardFocusHere(); focusSearch = false; }
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint(queryInputId, "Search places, zones, quests...", ref query, 256);
                if (ImGui.IsKeyPressed(ImGuiKey.Tab, false)) {
                    var current = Array.IndexOf(scopes, PlaceSearch.Parse(query, scopes[scope]).Scope);
                    ChangeScope((current + (ImGui.GetIO().KeyShift ? scopes.Length - 1 : 1)) % scopes.Length);
                }
                var effective = PlaceSearch.Parse(query, scopes[scope]).Scope;
                using (MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * scale, ChromeFontRole.Chip)?.Push()) {
                    var compact = size.X < 490 * scale;
                    for (var i = 0; i < scopes.Length; i++) {
                        if (i > 0) ImGui.SameLine();
                        ImGui.PushStyleColor(ImGuiCol.Button, effective == scopes[i] ? config.AccentColor with { W = .25f } : new Vector4(1, 1, 1, .035f));
                        var label = compact ? new[] { "All", "a:", "z:", "q:", "m:" }[i] : labels[i];
                        var chipWidth = compact ? (size.X - 2 * GratisTheme.Inset * scale - 4 * GratisTheme.Gap * scale) / 5
                            : ImGui.CalcTextSize(label).X + 44 * scale;
                        if (ImGui.Button("##scope-" + i, new Vector2(MathF.Max(1, chipWidth), 30 * scale))) ChangeScope(i);
                        var chipMin = ImGui.GetItemRectMin(); var chipMax = ImGui.GetItemRectMax();
                        if (!compact) {
                            var glyph = i switch { 0 => GratisIcons.LayoutGrid, 1 => GratisIcons.Sparkles, 2 => GratisIcons.Compass, 3 => GratisIcons.BookOpen, _ => GratisIcons.MapPin };
                            GratisIcons.Draw(surface, new Vector2(chipMin.X + 14 * scale, (chipMin.Y + chipMax.Y) / 2), glyph, GratisTheme.Primary, 13 * scale);
                        }
                        var textSize = TextDrawing.Measure(label);
                        TextDrawing.Draw(surface, new Vector2(compact ? (chipMin.X + chipMax.X - textSize.X) / 2 : chipMin.X + 28 * scale,
                            (chipMin.Y + chipMax.Y - textSize.Y) / 2), ImGui.GetColorU32(GratisTheme.Primary), label);
                        if (compact && ImGui.IsItemHovered()) ImGui.SetTooltip(labels[i]);
                        ImGui.PopStyleColor();
                    }
                }
                Search(config);
                var footer = size.Y < 240 * scale ? 0 : (confirming != null ? 56 : 44) * scale;
                var available = ImGui.GetContentRegionAvail();
                if (BeginChild("##place-results", new Vector2(0, MathF.Max(1, available.Y - footer)), ImGuiWindowFlags.NoBackground)) {
                    if (Home) DrawBrowser(config, scale);
                    else {
                        NavigateResults(config, size, scale);
                        if (results.Length == 0) {
                            ImGui.Spacing(); ImGui.TextUnformatted(source.Building ? "Indexing places..." : "No places found");
                            ImGui.TextWrapped("Try a zone name or choose another filter.");
                        }
                        for (var i = 0; i < results.Length; i++) DrawRow(results[i], i, config, scale, size.X);
                    }
                    scrollSelection = false;
                }
                ImGui.EndChild();
                if (footer > 0) {
                    ImGui.Separator();
                    using var hint = MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * scale, ChromeFontRole.Body)?.Push();
                    if (confirming is { } target) {
                        var clicked = MapAssistance.PrimaryTravelButton("Confirm teleport", GratisIcons.Compass);
                        ConfirmationCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
                        if (clicked) Teleport(target, true);
                        if (size.X > 470 * scale) { ImGui.SameLine(); ImGui.TextDisabled(message); }
                    }
                    else if (message.Length > 0) ImGui.TextUnformatted(ChromeControls.FitLabel(message, ImGui.GetContentRegionAvail().X));
                    else if (size.X < 440 * scale) ChromeControls.Hints(("Enter", "Show"), ("Esc", "Close"), ("Tab", "Filter"));
                    else if (Home) ChromeControls.Hints(("Up/Down", "Browse"), ("Enter", "Select"), ("Tab", "Filter"), ("Esc", "Close"));
                    else if (config.PaletteTeleportButton) ChromeControls.Hints(("Enter", "Show"), ("Alt+Enter", "Teleport"), ("Tab", "Filter"), ("Esc", "Close"));
                    else ChromeControls.Hints(("Up/Down", "Browse"), ("Enter", "Show"), ("Tab", "Filter"), ("Esc", "Close"));
                }
            }
            ImGui.EndChild();
        }
        finally { ImGui.PopStyleColor(5); ImGui.PopStyleVar(5); }
    }

    private void NavigateResults(MapDrawingConfig config, Vector2 size, float scale)
    {
        var page = Math.Max(1, (int)((size.Y / scale - 178) / 80));
        var previous = selected;
        if (ImGui.IsKeyPressed(ImGuiKey.DownArrow)) selected++;
        if (ImGui.IsKeyPressed(ImGuiKey.UpArrow)) selected--;
        if (ImGui.IsKeyPressed(ImGuiKey.PageDown)) selected += page;
        if (ImGui.IsKeyPressed(ImGuiKey.PageUp)) selected -= page;
        if (ImGui.IsKeyPressed(ImGuiKey.Home)) selected = 0;
        if (ImGui.IsKeyPressed(ImGuiKey.End)) selected = results.Length - 1;
        selected = Math.Clamp(selected, 0, Math.Max(0, results.Length - 1));
        if (previous != selected) { scrollSelection = true; confirming = null; message = ""; }
        if (results.Length > 0 && ImGui.IsKeyPressed(ImGuiKey.Enter, false)) {
            if (ImGui.GetIO().KeyAlt && config.PaletteTeleportButton) Teleport(results[selected].Place, false);
            else Activate(results[selected].Place);
        }
    }

    private void DrawRow(PlaceResult result, int index, MapDrawingConfig config, float scale, float width)
    {
        ImGui.PushID(result.Place.Id);
        var start = ImGui.GetCursorScreenPos(); var rowWidth = ImGui.GetContentRegionAvail().X;
        var rowHeight = 76 * scale;
        if (index == 0) FirstResultCenter = start + new Vector2(rowWidth / 2, rowHeight / 2);
        var canTravel = MapDrawContext.Current.Assistance?.CanOffer(result.Place) == true;
        var canTeleport = canTravel || config.PaletteTeleportButton && result.Place.Kind == PlaceKind.Aetheryte && !result.Place.IsShard && result.Access.Attuned;
        var buttonWidth = canTeleport && width >= 600 * scale ? 108 * scale : 0;
        if (ImGui.InvisibleButton("##show", new Vector2(MathF.Max(1, rowWidth - buttonWidth), rowHeight))) Activate(result.Place);
        var hovered = ImGui.IsItemHovered();
        if (hovered && ImGui.GetIO().MouseDelta.LengthSquared() > 0) selected = index;
        if (scrollSelection && index == selected) ImGui.SetScrollHereY();
        if (!ImGui.IsItemVisible()) {
            ImGui.SetCursorScreenPos(start + new Vector2(0, rowHeight + GratisTheme.Grid * scale));
            ImGui.Dummy(Vector2.Zero); ImGui.PopID(); return;
        }
        var list = ImGui.GetWindowDrawList();
        list.AddRectFilled(start, start + new Vector2(rowWidth, rowHeight), ImGui.GetColorU32(index == selected || hovered
            ? config.AccentColor with { W = index == selected ? .17f : .08f } : new Vector4(1, 1, 1, .025f)), GratisTheme.ButtonRadius * scale);
        var imageWidth = config.PaletteImagery && width >= 460 * scale ? 90 * scale : 0;
        if (imageWidth > 0) DrawArt(result.Place, start + new Vector2(6, 6) * scale, new Vector2(imageWidth, rowHeight - 12 * scale), config, scale);
        else DrawKindIcon(result.Place, list, start + new Vector2(21, 26) * scale, GratisTheme.IconSize * scale);
        var textMin = start + new Vector2(imageWidth > 0 ? imageWidth + 18 * scale : 40 * scale, 9 * scale);
        var textMax = start + new Vector2(rowWidth - buttonWidth - 8 * scale, rowHeight);
        var selectedCap = index == selected && width >= 600 * scale ? ChromeControls.KeycapSize("Enter") : Vector2.Zero;
        list.PushClipRect(textMin, Vector2.Max(textMin + Vector2.One, textMax), true);
        TextDrawing.Draw(list, textMin, ImGui.GetColorU32(GratisTheme.Primary), ChromeControls.FitLabel(PaletteBrowser.Name(result, results), MathF.Max(0, textMax.X - textMin.X - (selectedCap.X > 0 ? selectedCap.X + 8 * scale : 0))));
        using (MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * scale, ChromeFontRole.Body)?.Push()) {
            var secondaryInk = new Vector4(.72f, .72f, .72f, 1);
            TextDrawing.Draw(list, textMin + new Vector2(0, 23 * scale), ImGui.GetColorU32(secondaryInk), ChromeControls.FitLabel(PaletteBrowser.Detail(result.Place), textMax.X - textMin.X));
            var status = result.Place.Kind == PlaceKind.Aetheryte ? result.Place.IsShard ? "Aethernet" : result.Access.Attuned ? "Attuned" : "Not yet attuned" : result.Place.Kind.ToString();
            var metadata = result.Distance is { } distance ? $"{distance:0}y away"
                : result.Place.World is { } world ? $"World X {world.X:0}, Z {world.Y:0}" : "";
            var pillMin = textMin + new Vector2(0, 42 * scale);
            var pillSize = TextDrawing.Measure(status) + new Vector2(12, 3) * scale;
            list.AddRectFilled(pillMin, pillMin + pillSize, ImGui.GetColorU32(GratisTheme.Primary with { W = .055f }), pillSize.Y / 2);
            TextDrawing.Draw(list, pillMin + new Vector2(6, 1) * scale, ImGui.GetColorU32(result.Place.Kind == PlaceKind.Aetheryte && result.Access.Attuned ? GratisTheme.Transport : GratisTheme.Secondary), status);
            TextDrawing.Draw(list, pillMin + new Vector2(pillSize.X + 8 * scale, scale), ImGui.GetColorU32(secondaryInk), ChromeControls.FitLabel(metadata, MathF.Max(0, textMax.X - pillMin.X - pillSize.X - 8 * scale)));
        }
        list.PopClipRect();
        if (selectedCap.X > 0) ChromeControls.DrawKeycap(list, new(textMax.X - selectedCap.X, textMin.Y), "Enter");
        if (hovered) ChromeControls.Tooltip(PaletteBrowser.Name(result, results) +
            (result.Place.Kind == PlaceKind.Aetheryte && !result.Access.Attuned ? " · Not yet attuned" : ""), "Enter");
        if (buttonWidth == 0 && rowWidth > 240 * scale) {
            var chevron = start + new Vector2(rowWidth - 13 * scale, rowHeight / 2);
            var colour = ImGui.GetColorU32(GratisTheme.Secondary);
            list.AddLine(chevron - new Vector2(3, 4) * scale, chevron + new Vector2(1, 0) * scale, colour, scale);
            list.AddLine(chevron + new Vector2(1, 0) * scale, chevron - new Vector2(3, -4) * scale, colour, scale);
        }
        if (buttonWidth > 0) {
            ImGui.SetCursorScreenPos(start + new Vector2(rowWidth - buttonWidth, 21 * scale));
            using (MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * scale, ChromeFontRole.Chip)?.Push())
                if (MapAssistance.ActionButton(canTravel ? "Travel here" : result.Access.Cost is { } cost ? $"{cost:N0} gil" : "Teleport", GratisIcons.Compass)) Teleport(result.Place, false);
        }
        ImGui.SetCursorScreenPos(start + new Vector2(0, rowHeight + GratisTheme.Grid * scale));
        ImGui.Dummy(Vector2.Zero); ImGui.PopID();
    }
}
