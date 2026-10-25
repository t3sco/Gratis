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
    void ShowMainStory();
    string Teleport(PlaceEntry place, bool confirmed);
    void Used();
    PaletteImage Image(PlaceEntry place) => default;
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
    private int windowPresetRevision = -1;
    private WindowPresetSettings? windowPresetSettings;
    private readonly List<string> recentQueries = [];
    private PaletteExpansion[] expansions = [];
    private uint? expansion;
    private int browserSelection;
    public uint? BrowsedExpansion => expansion;
    public int BrowserSelection => browserSelection;
    public IReadOnlyList<PaletteExpansion> Expansions => expansions;
    public Vector2 FirstResultCenter { get; private set; }
    public Vector2 ConfirmationCenter { get; private set; }
    private bool Home => string.IsNullOrWhiteSpace(query) && scope == 0;
    public void BrowseExpansion(uint? id) { expansion = id; browserSelection = 0; revision = -1; scrollSelection = false; focusSearch = true; }


    public void Open(string text = "")
    {
        IsOpen = CapturesInput = true; focusSearch = true; query = text; scope = 0; selected = 0;
        queryInputId = $"##place-query-{++openSerial}";
        revision = -1; searchedScope = -1; confirming = null; message = ""; expansion = null; browserSelection = 0;
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
        var windows = MapDrawContext.Current.WindowPresets;
        if (windowPresetRevision != windows.Revision || windowPresetSettings != config.WindowPresetOptions) {
            windowPresetRevision = windows.Revision; windowPresetSettings = config.WindowPresetOptions; revision = -1;
        }
        if (searched == query && searchedScope == scope && revision == source.Revision && context == source.Context
            && showUnattuned == config.PaletteShowUnattuned) return;
        if (searched != query) browserSelection = 0;
        if (searched != query || searchedScope != scope) { selected = 0; scrollSelection = true; confirming = null; message = ""; }
        searched = query; searchedScope = scope; revision = source.Revision; context = source.Context;
        showUnattuned = config.PaletteShowUnattuned;
        var entries = NearestPlaces.Parse(query, out _) ? source.Entries.Concat(MapDrawContext.Current.MarkerTargets.Entries) : source.Entries.Append(MainStoryObjective.Action);
        if (config is Gratis.Data.SystemConfig systemConfig && !NearestPlaces.Parse(query, out _))
            entries = entries.Concat(windows.Commands(systemConfig));
        var snapshot = entries.ToArray();
        expansions = PaletteBrowser.Expansions(snapshot);
        if (Home && expansion is { } ex) snapshot = snapshot.Where(p => p.Expansion == ex && p.Kind is PlaceKind.Zone or PlaceKind.Aetheryte).ToArray();
        results = PaletteBrowser.Distinct(PlaceSearch.Find(snapshot, query, scopes[scope], context, recent, source.Access, showUnattuned));
        if (NearestPlaces.Parse(query, out _) && context.Player == null) message = "Player position is unavailable on the selected map.";
        selected = Math.Clamp(selected, 0, Math.Max(0, results.Length - 1));
    }

    private void ChangeScope(int next)
    {
        scope = next; query = PlaceSearch.Parse(query, PlaceKind.All).Query;
        queryInputId = $"##place-query-{++openSerial}"; focusSearch = true;
    }

    private static bool BeginChild(string id, Vector2 size, ImGuiWindowFlags flags) =>
#if GRATIS_PREVIEW
        ImGui.BeginChild(id, size, ImGuiChildFlags.AlwaysUseWindowPadding, flags);
#else
        ImGui.BeginChild(id, size, false, flags | ImGuiWindowFlags.AlwaysUseWindowPadding);
#endif

    private void Activate(PlaceEntry place)
    {
        if (MapDrawContext.Current.Config is Gratis.Data.SystemConfig config && MapDrawContext.Current.WindowPresets.Activate(place.Id, config)) { Close(); return; }
        if (place.Id == MainStoryObjective.Action.Id) { source.ShowMainStory(); Close(); return; }
        if (!string.IsNullOrWhiteSpace(query)) {
            recentQueries.Remove(query); recentQueries.Insert(0, query);
            if (recentQueries.Count > 6) recentQueries.RemoveAt(recentQueries.Count - 1);
        }
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
