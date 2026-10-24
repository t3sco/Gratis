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

public interface IPaletteSource
{
    IEnumerable<PlaceEntry> Entries { get; }
    int Revision { get; }
    bool Building { get; }
    PlaceSearchContext Context { get; }
    PlaceAccess Access(PlaceEntry place);
    void Opening() => Refresh();
    void Refresh();
    void Show(PlaceEntry place);
    string Teleport(PlaceEntry place, bool confirmed);
    void Used();
}

public sealed partial class PaletteOverlay(IPaletteSource source)
{
    private static readonly PlaceKind[] scopes = [PlaceKind.All, PlaceKind.Aetheryte, PlaceKind.Zone, PlaceKind.Quest, PlaceKind.Marker];
    private static readonly string[] labels = ["All", "Aetherytes", "Zones", "Quests", "Markers"];
    private readonly Dictionary<string, long> recent = new();
    private string query = "", searched = "", message = "";
    private string queryInputId = "##place-query";
    private int openSerial;
    private int scope, searchedScope = -1, revision = -1, selected;
    private bool focusSearch, scrollSelection;
    private bool swallowEscape;
    private long sequence;
    private PlaceSearchContext context;
    private bool showUnattuned;
    private PlaceEntry? confirming;
    public bool IsOpen { get; private set; }
    public bool CapturesInput { get; private set; }
    public IReadOnlyList<PlaceResult> Results => results;
    public int Selected => selected;
    public string Query => query;
    public Vector2 PanelMin { get; private set; }
    public Vector2 PanelMax { get; private set; }
    private PlaceResult[] results = [];

    public void Open(string text = "")
    {
        IsOpen = CapturesInput = true; focusSearch = true; query = text; scope = 0; selected = 0;
        queryInputId = $"##place-query-{++openSerial}";
        revision = -1; searchedScope = -1; confirming = null; message = "";
        source.Opening(); source.Used();
    }
    public void Close(bool restoreFocus = true) { IsOpen = false; confirming = null; if (restoreFocus) ImGui.SetWindowFocus(); }

    public void Prepare(MapDrawingConfig config, bool focused)
    {
        CapturesInput = IsOpen;
        if (!config.IsGratis || !config.Palette) { IsOpen = false; CapturesInput = false; return; }
        if (swallowEscape) {
            swallowEscape = ImGui.IsKeyDown(ImGuiKey.Escape);
            if (swallowEscape) { CapturesInput = true; ImGui.SetNextFrameWantCaptureKeyboard(true); }
        }
        if (IsOpen && !focused) { Close(false); CapturesInput = false; return; }
        if (focused && Shortcut(config.PaletteShortcut)) { if (IsOpen) Close(); else Open(); CapturesInput = true; }
        if (!IsOpen) return;
        CapturesInput = true;
        ImGui.SetNextFrameWantCaptureKeyboard(true);
        ImGui.SetNextFrameWantCaptureMouse(true);
        if (ImGui.IsKeyPressed(ImGuiKey.Escape, false)) { swallowEscape = true; Close(); return; }
        source.Refresh();
    }

    private static bool Shortcut(string shortcut)
    {
        var parts = (shortcut ?? "Ctrl+K").Split('+', StringSplitOptions.TrimEntries);
        if (!Enum.TryParse<ImGuiKey>(parts[^1], true, out var key) || key < ImGuiKey.A || key > ImGuiKey.Z) return false;
        var io = ImGui.GetIO();
        return io.KeyCtrl == Array.Exists(parts, p => p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
            && io.KeyShift == Array.Exists(parts, p => p.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            && io.KeyAlt == Array.Exists(parts, p => p.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            && ImGui.IsKeyPressed(key, false);
    }

    private void Search(MapDrawingConfig config)
    {
        if (searched == query && searchedScope == scope && revision == source.Revision && context == source.Context
            && showUnattuned == config.PaletteShowUnattuned) return;
        if (searched != query || searchedScope != scope) { selected = 0; scrollSelection = true; confirming = null; message = ""; }
        searched = query; searchedScope = scope; revision = source.Revision; context = source.Context;
        showUnattuned = config.PaletteShowUnattuned;
        var entries = NearestPlaces.Parse(query, out _) ? source.Entries.Concat(MapDrawContext.Current.MarkerTargets.Entries) : source.Entries;
        results = PlaceSearch.Find(entries, query, scopes[scope], context, recent, source.Access, showUnattuned);
        if (NearestPlaces.Parse(query, out _) && context.Player == null) message = "Player position is unavailable on the selected map.";
        selected = Math.Clamp(selected, 0, Math.Max(0, results.Length - 1));
    }

    public void Draw(MapDrawingConfig config)
    {
        if (!IsOpen || !config.IsGratis || !config.Palette) return;
        Search(config);
        var scale = MapDrawContext.Current.GlobalScale;
        var (min, max) = Classes.MapShape.Bounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        var area = max - min;
        if (config.MapCircle) { var inset = area * ((1 - .70710678f) / 2); min += inset; max -= inset; area = max - min; }
        var rows = results.Length == 0 ? 2 : Math.Min(results.Length, Math.Clamp(config.PaletteMaxResults, 1, 12));
        var size = new Vector2(MathF.Min(580 * scale, MathF.Max(1, area.X - 24 * scale)),
            MathF.Min((116 + rows * 54 + (query.Length == 0 ? 28 : 0)) * scale, MathF.Max(1, area.Y - 24 * scale)));
        PanelMin = min + (area - size) / 2; PanelMax = PanelMin + size;
        if (size.X < 150 * scale || size.Y < 112 * scale) return;
        using var font = MapDrawContext.Current.GetChromeFont(14 * scale, ChromeFontRole.Body)?.Push();
        var list = ImGui.GetWindowDrawList();
        list.AddRectFilled(PanelMin + new Vector2(0, 7 * scale), PanelMax + new Vector2(0, 7 * scale),
            ImGui.GetColorU32(new Vector4(0, 0, 0, .25f)), 12 * scale);
        ImGui.SetCursorScreenPos(PanelMin);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 10) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6, 6) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 6) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 3 * scale);
        ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Primary);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, new Vector4(.67f, .69f, .73f, 1));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(1, 1, 1, .045f));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1, 1, 1, .045f));
        try {
            if (BeginChild("##place-palette", size, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)) {
                var surface = ImGui.GetWindowDrawList();
                surface.PushClipRect(PanelMin, PanelMax, false);
                MapWindowDrawing.DrawSurface(surface, PanelMin, PanelMax, 12 * scale, surfaceOpacity: 1);
                surface.PopClipRect();
                if (focusSearch) { ImGui.SetKeyboardFocusHere(); focusSearch = false; }
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint(queryInputId, "Search places, even before attuning...", ref query, 256);
                if (ImGui.IsKeyPressed(ImGuiKey.Tab, false)) {
                    var current = Array.IndexOf(scopes, PlaceSearch.Parse(query, scopes[scope]).Scope);
                    ChangeScope((current + (ImGui.GetIO().KeyShift ? scopes.Length - 1 : 1)) % scopes.Length);
                }
                var effective = PlaceSearch.Parse(query, scopes[scope]).Scope;
                using (MapDrawContext.Current.GetChromeFont(11 * scale, ChromeFontRole.Chip)?.Push()) {
                    var compact = size.X < 380 * scale;
                    for (var i = 0; i < scopes.Length; i++) {
                        if (i > 0) ImGui.SameLine();
                        ImGui.PushStyleColor(ImGuiCol.Button, effective == scopes[i] ? config.AccentColor with { W = .28f } : new Vector4(1, 1, 1, .035f));
                        if (ImGui.SmallButton(compact ? new[] { "All", "a:", "z:", "q:", "m:" }[i] : labels[i])) {
                            ChangeScope(i);
                        }
                        ImGui.PopStyleColor();
                    }
                }
                DrawNearestChips(size.X, scale);
                Search(config);
                var page = Math.Max(1, (int)((size.Y / scale - 116) / 54));
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
                    else { Activate(results[selected].Place); }
                }
                var footer = 29 * scale;
                var available = ImGui.GetContentRegionAvail();
                if (BeginChild("##place-results", new Vector2(0, MathF.Max(1, available.Y - footer)), ImGuiWindowFlags.NoBackground)) {
                    if (results.Length == 0) {
                        ImGui.Spacing();
                        ImGui.TextUnformatted(source.Building ? "Indexing places..." : "No places found");
                        ImGui.TextDisabled("Try a shorter name or another scope.");
                        ImGui.TextDisabled("a: teleports   z: zones   q: quests");
                    }
                    for (var i = 0; i < results.Length; i++) DrawRow(results[i], i, config, scale, size.X);
                    scrollSelection = false;
                }
                ImGui.EndChild();
                ImGui.Separator();
                using (MapDrawContext.Current.GetChromeFont(11 * scale, ChromeFontRole.Body)?.Push()) {
                    if (confirming is { } target) {
                        if (ImGui.SmallButton("Confirm teleport")) Teleport(target, true);
                        ImGui.SameLine(); ImGui.TextDisabled(message);
                    }
                    else ImGui.TextDisabled(message.Length > 0 ? message : size.X < 350 * scale
                        ? "Enter show  ·  Esc close  ·  Tab scope"
                        : query.Length == 0 ? "Type a place  ·  Up/Down navigate  ·  Enter show  ·  Esc close"
                        : "Up/Down  ·  Enter show  ·  Alt+Enter teleport  ·  Tab scope");
                }
            }
            ImGui.EndChild();
        }
        finally { ImGui.PopStyleColor(5); ImGui.PopStyleVar(5); }
    }

    private void ChangeScope(int next)
    {
        scope = next; query = PlaceSearch.Parse(query, PlaceKind.All).Query;
        queryInputId = $"##place-query-{++openSerial}"; focusSearch = true;
    }

    private void DrawRow(PlaceResult result, int index, MapDrawingConfig config, float scale, float width)
    {
        ImGui.PushID(result.Place.Id);
        var start = ImGui.GetCursorScreenPos(); var rowWidth = ImGui.GetContentRegionAvail().X;
        var rowHeight = 48 * scale;
        var canTravel = MapDrawContext.Current.Assistance?.CanOffer(result.Place) == true;
        var canTeleport = canTravel || config.PaletteTeleportButton && result.Place.Kind == PlaceKind.Aetheryte
            && !result.Place.IsShard && result.Access.Attuned;
        var buttonWidth = canTeleport && width >= 380 * scale ? 104 * scale : 0;
        if (ImGui.InvisibleButton("##show", new Vector2(MathF.Max(1, rowWidth - buttonWidth), rowHeight))) Activate(result.Place);
        var hovered = ImGui.IsItemHovered();
        if (hovered && ImGui.GetIO().MouseDelta.LengthSquared() > 0) selected = index;
        if (scrollSelection && index == selected) ImGui.SetScrollHereY();
        var list = ImGui.GetWindowDrawList();
        if (index == selected || hovered) list.AddRectFilled(start, start + new Vector2(rowWidth, rowHeight),
            ImGui.GetColorU32(config.AccentColor with { W = index == selected ? .17f : .08f }), 6 * scale);
        var icon = result.Place.Kind switch {
            PlaceKind.Aetheryte => result.Place.IsShard ? GratisIcons.Repeat : GratisIcons.Sparkles,
            PlaceKind.Zone => GratisIcons.Compass, PlaceKind.Quest => GratisIcons.BookOpen,
            PlaceKind.Flag => GratisIcons.Flag, _ => GratisIcons.MapPin,
        };
        GratisIcons.Draw(list, start + new Vector2(15, 24) * scale, icon,
            result.Place.Kind == PlaceKind.Aetheryte ? GratisTheme.Transport : GratisTheme.Secondary, 17 * scale);
        var textMin = start + new Vector2(34, 7) * scale;
        var textRight = start.X + rowWidth - buttonWidth - 8 * scale;
        var statusWidth = 0f;
        if (result.Place.Kind == PlaceKind.Aetheryte) {
            using var chipFont = MapDrawContext.Current.GetChromeFont(11 * scale, ChromeFontRole.Chip)?.Push();
            var chip = result.Place.IsShard ? "Aethernet" : result.Access.Attuned ? "Attuned" : "Not attuned";
            var chipSize = ImGui.CalcTextSize(chip);
            statusWidth = chipSize.X + 18 * scale;
            var chipMin = new Vector2(textRight - statusWidth + 4 * scale, textMin.Y - 1 * scale);
            list.AddRectFilled(chipMin, chipMin + chipSize + new Vector2(10, 4) * scale,
                ImGui.GetColorU32(new Vector4(1, 1, 1, result.Access.Attuned ? .07f : .035f)), 4 * scale);
            list.AddText(chipMin + new Vector2(5, 2) * scale,
                ImGui.GetColorU32(new Vector4(result.Access.Attuned ? new Vector3(.76f, .83f, .88f) : new Vector3(.59f), 1)), chip);
        }
        var textMax = new Vector2(MathF.Max(textMin.X + 1, textRight - statusWidth), start.Y + rowHeight);
        list.PushClipRect(textMin, textMax, true);
        list.AddText(textMin, ImGui.GetColorU32(GratisTheme.Primary), result.Place.Name);
        list.PopClipRect();
        list.PushClipRect(textMin, new Vector2(MathF.Max(textMin.X + 1, textRight), start.Y + rowHeight), true);
        using (MapDrawContext.Current.GetChromeFont(11 * scale, ChromeFontRole.Body)?.Push()) {
            var detail = result.Place.Subtitle;
            if (result.Distance is { } distance) detail = context.Player is { } player && result.Place.World is { } world && NearestPlaces.Parse(query, out _)
                ? $"{NearestPlaces.Direction(player, world)} {distance:0}y  ·  " + detail : detail + $"  ·  {distance:0}y";
            list.AddText(textMin + new Vector2(0, 20 * scale), ImGui.GetColorU32(new Vector4(.67f, .69f, .73f, 1)), detail);
        }
        list.PopClipRect();
        if (buttonWidth > 0) {
            ImGui.SetCursorScreenPos(start + new Vector2(rowWidth - buttonWidth, 13 * scale));
            using (MapDrawContext.Current.GetChromeFont(11 * scale, ChromeFontRole.Chip)?.Push())
                if (MapAssistance.ActionButton(canTravel ? "Travel here" : result.Access.Cost is { } cost ? $"{cost:N0} gil" : "Teleport", GratisIcons.Compass)) Teleport(result.Place, false);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Teleport (Alt+Enter)");
        }
        else if (index == selected && width >= 480 * scale && result.Place.Kind != PlaceKind.Aetheryte) {
            using var hintFont = MapDrawContext.Current.GetChromeFont(11 * scale, ChromeFontRole.Chip)?.Push();
            list.AddText(start + new Vector2(rowWidth - 32 * scale, 9 * scale), ImGui.GetColorU32(GratisTheme.Secondary), "Enter");
        }
        if (hovered) {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(result.Place.Name + "\n" + result.Place.Subtitle +
                (result.Place.Kind == PlaceKind.Aetheryte && !result.Access.Attuned ? "\nNot attuned — show its location to plan your journey." : "\nEnter: show on map"));
            ImGui.EndTooltip();
        }
        ImGui.SetCursorScreenPos(start + new Vector2(0, rowHeight));
        ImGui.Dummy(Vector2.Zero);
        ImGui.PopID();
    }

    private static bool BeginChild(string id, Vector2 size, ImGuiWindowFlags flags) =>
#if GRATIS_PREVIEW
        ImGui.BeginChild(id, size, ImGuiChildFlags.AlwaysUseWindowPadding, flags);
#else
        ImGui.BeginChild(id, size, false, flags | ImGuiWindowFlags.AlwaysUseWindowPadding);
#endif

    private void Activate(PlaceEntry place)
    {
        recent[place.Id] = ++sequence;
        if (recent.Count > 128) { string? oldest = null; long rank = long.MaxValue; foreach (var item in recent) if (item.Value < rank) { rank = item.Value; oldest = item.Key; } if (oldest != null) recent.Remove(oldest); }
        source.Show(place); Close();
    }

    private void Teleport(PlaceEntry place, bool confirmed)
    {
        message = MapDrawContext.Current.Assistance?.CanOffer(place) == true
            ? MapDrawContext.Current.Assistance.Request(place, confirmed) : source.Teleport(place, confirmed);
        confirming = message.StartsWith("Confirm ", StringComparison.Ordinal) ? place : null;
    }

    public void DrawHint(MapDrawingConfig config)
    {
        if (IsOpen || !config.IsGratis || !config.Palette || config.PaletteHintSeen) return;
        var scale = MapDrawContext.Current.GlobalScale;
        if (ImGui.GetWindowWidth() < 220 * scale || ImGui.GetWindowHeight() < 180 * scale) return;
        using var font = MapDrawContext.Current.GetChromeFont(11 * scale, ChromeFontRole.Chip)?.Push();
        var position = new Vector2(10, 49) * scale;
        if (config.MapCircle) {
            var (min, max) = Classes.MapShape.Bounds(Vector2.Zero, ImGui.GetWindowSize());
            var radius = (max.X - min.X) / 2;
            var half = (ImGui.CalcTextSize($"Search places ({config.PaletteShortcut})").X + ImGui.GetStyle().FramePadding.X * 2) / 2;
            if (half + 8 * scale >= radius) return;
            position = (min + max) / 2 - new Vector2(half, MathF.Sqrt(radius * radius - half * half) - 49 * scale);
        }
        ImGui.SetCursorPos(position);
        if (ImGui.SmallButton($"Search places ({config.PaletteShortcut})")) Open();
    }
}
