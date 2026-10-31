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

public interface IPaletteSource {
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
    PaletteImage Crest(PlaceEntry place) => default;
}

public sealed partial class PaletteOverlay(IPaletteSource source) {
    private static readonly PlaceKind[] scopes = [PlaceKind.All,
        PlaceKind.Aetheryte,
        PlaceKind.Zone,
        PlaceKind.Quest,
        PlaceKind.Marker];
    private static readonly string[] labels = ["All", "Aetherytes", "Zones", "Quests", "Markers"];
    private static readonly string[] scopeIds = ["##scope-0", "##scope-1", "##scope-2", "##scope-3", "##scope-4"];
    private static readonly QuestBucket?[] questFilters = [null, QuestBucket.Scenario, QuestBucket.SideQuests,
        QuestBucket.JobClass, QuestBucket.Chronicles, QuestBucket.AlliedSocieties, QuestBucket.Other];
    private static readonly string[] questLabels = questFilters.Select(b => b is { } bucket
        ? QuestBuckets.Label(bucket) : "All").ToArray();
    private static readonly string[] questChipIds = ["##quest-bucket-0", "##quest-bucket-1", "##quest-bucket-2",
        "##quest-bucket-3", "##quest-bucket-4", "##quest-bucket-5", "##quest-bucket-6"];
    private readonly ChipLabelCache[] questChipLabels = questFilters.Select(_ => new ChipLabelCache()).ToArray();
    private int questFilter, searchedQuestFilter = -1;
    private readonly int[] questCounts = new int[questFilters.Length];
    private readonly Vector4[] questChipBounds = new Vector4[questFilters.Length];
    public QuestBucket? SelectedQuestBucket => questFilters[questFilter];
    public IReadOnlyList<int> QuestCounts => questCounts;
    public IReadOnlyList<Vector4> QuestChipBounds => questChipBounds;
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
    public Vector4 FieldBounds { get; private set; }
    public Vector4 ClearBounds { get; private set; }
    private float inlineWidth;
    public float InlineWidth(float scale) {
        inlineWidth = UiMotion.Value(ImGui.GetID("##inline-search-width"), 12, IsOpen ? 240 : 0,
            InterfaceMotionTokens.Standard, InterfaceCurve.Spring, initial: 0);
        if (IsOpen)
            inlineWidth = MathF.Max(96, inlineWidth);
        else if (inlineWidth < 1)
            inlineWidth = 0;
        return inlineWidth * scale;
    }

    public void DrawInline(Vector2 position, Vector2 size) {
        var s = GratisTheme.ChromeScale;
        FieldBounds = new(position.X, position.Y, position.X + size.X, position.Y + size.Y);
        ClearBounds = Vector4.Zero;
        ImGui.BeginDisabled(!IsOpen);
        using var fieldMotion = UiMotion.Surface("##inline-field-enter", position, IsOpen);
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push();
        ImGui.SetCursorScreenPos(position);
        if (focusSearch) {
            ImGui.SetKeyboardFocusHere();
            focusSearch = false;
        }
        var wasEmpty = query.Length == 0;
        var field = TextField.Draw(queryInputId, ref query, new(FieldFaces.Palette, size, s));
        ClearBounds = field.ClearBounds;
        // Clear on an empty search has nothing left to clear, so it closes the search.
        if (field.Cleared && wasEmpty)
            Close();
        else if (field.Cleared)
            SetQuery("");
        Search(MapDrawContext.Current.Config);
        ImGui.EndDisabled();
    }
    private PlaceResult[] results = [];
    private readonly PaletteRowNames rowNames = new();
    private PlaceResult[] previousResults = [];
    private PaletteExpansion[] previousExpansions = [];
    private PaletteZone[] previousBrowserZones = [];
    private uint? previousExpansion;
    private bool previousHome;
    private int previousScope;
    private int viewRevision;
    private void RememberView() {
        previousResults = results;
        previousExpansions = expansions;
        previousBrowserZones = browserZones;
        previousExpansion = expansion;
        previousHome = string.IsNullOrWhiteSpace(searched);
        previousScope = Math.Max(0, searchedScope);
        viewRevision++;
    }
    private int windowPresetRevision = -1;
    private WindowPresetSettings? windowPresetSettings;
    private readonly List<string> recentQueries = [];
    private PaletteExpansion[] expansions = [];
    private PaletteZone[] browserZones = [];
    private uint? expansion;
    private int browserSelection;
    private PlaceResult?[] nearby = new PlaceResult?[NearbyCards.Length];
    public uint? BrowsedExpansion => expansion;
    public int BrowserSelection => browserSelection;
    public IReadOnlyList<PaletteExpansion> Expansions => expansions;
    public Vector2 FirstResultCenter { get; private set; }
    public Vector2 ConfirmationCenter { get; private set; }
    private bool Home => string.IsNullOrWhiteSpace(query);
    public void BrowseExpansion(uint? id) {
        RememberView();
        expansion = id;
        browserSelection = 0;
        openZones.Clear();
        zoneLimits.Clear();
        resetBrowserScroll = true;
        revision = -1;
        scrollSelection = false;
        focusSearch = true;
    }

    public void Open(string text = "") {
        if (MapDrawContext.Current.IsPvP) {
            Close(false);
            return;
        }
        IsOpen = CapturesInput = true;
        focusSearch = true;
        query = text;
        scope = 0;
        questFilter = 0;
        selected = 0;
        queryInputId = $"##place-query-{++openSerial}";
        revision = -1;
        searchedScope = -1;
        confirming = null;
        message = "";
        expansion = null;
        browserSelection = 0;
        openZones.Clear();
        zoneLimits.Clear();
        resetBrowserScroll = true;
        source.Opening();
        source.Used();
    }

    public void Close(bool restoreFocus = true) {
        IsOpen = false;
        confirming = null;
        if (restoreFocus)
            ImGui.SetWindowFocus();
    }

    public void Prepare(MapDrawingConfig config, bool focused) {
        if (MapDrawContext.Current.IsPvP) {
            Close(false);
            CapturesInput = false;
            return;
        }
        CapturesInput = IsOpen;
        if (!config.IsGratis || !config.Palette) {
            IsOpen = false;
            CapturesInput = false;
            return;
        }
        if (swallowEscape) {
            swallowEscape = ImGui.IsKeyDown(ImGuiKey.Escape);
            if (swallowEscape) {
                CapturesInput = true;
                ImGui.SetNextFrameWantCaptureKeyboard(true);
            }
        }
        if (IsOpen && !focused) {
            Close(false);
            CapturesInput = false;
            return;
        }
        if (!IsOpen)
            return;
        if ((ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            && !Contains(FieldBounds, ImGui.GetMousePos())
            && !Contains(new(PanelMin.X, PanelMin.Y, PanelMax.X, PanelMax.Y), ImGui.GetMousePos())
            && !Contains(MapToolbarDrawing.ButtonBounds[0], ImGui.GetMousePos())) {
            Close();
            CapturesInput = false;
            ImGui.SetNextFrameWantCaptureKeyboard(false);
            ImGui.SetNextFrameWantCaptureMouse(false);
            return;
        }
        CapturesInput = true;
        ImGui.SetNextFrameWantCaptureKeyboard(true);
        ImGui.SetNextFrameWantCaptureMouse(true);
        if (ImGui.IsKeyPressed(ImGuiKey.Escape, false)) {
            swallowEscape = true;
            if (Home && expansion != null)
                BrowseExpansion(null);
            else
                Close();
            return;
        }
        source.Refresh();
    }

    private static bool Contains(Vector4 rect, Vector2 p) => rect.Z > rect.X && rect.W > rect.Y && p.X >= rect.X
        && p.X <= rect.Z && p.Y >= rect.Y && p.Y <= rect.W;

    private void Search(MapDrawingConfig config) {
#if GRATIS_PREVIEW
        using var benchmark = BenchmarkProbe.Measure(BenchmarkSection.Search);
#endif
        var windows = MapDrawContext.Current.WindowPresets;
        if (windowPresetRevision != windows.Revision || windowPresetSettings != config.WindowPresetOptions) {
            windowPresetRevision = windows.Revision;
            windowPresetSettings = config.WindowPresetOptions;
            revision = -1;
        }
        if (searched == query && searchedScope == scope && searchedQuestFilter == questFilter
            && revision == source.Revision && context == source.Context
            && showUnattuned == config.PaletteShowUnattuned)
            return;
        var changed = searched != query || searchedScope != scope || searchedQuestFilter != questFilter;
        if (changed)
            RememberView();
        if (changed)
            browserSelection = 0;
        if (searchedScope != scope) {
            expansion = null;
            openZones.Clear();
            zoneLimits.Clear();
            resetBrowserScroll = true;
        }
        if (changed) {
            selected = 0;
            scrollSelection = true;
            if (Home)
                resetBrowserScroll = true;
            confirming = null;
            message = "";
        }
        searched = query;
        searchedScope = scope;
        searchedQuestFilter = questFilter;
        revision = source.Revision;
        context = source.Context;
        showUnattuned = config.PaletteShowUnattuned;
        var entries = Home ? source.Entries : NearestPlaces.Parse(query, out _)
            ? source.Entries.Concat(MapDrawContext.Current.MarkerTargets.Entries)
            : source.Entries.Append(MainStoryObjective.Action with { QuestBucket = QuestBucket.Scenario });
        if (!Home && config is Gratis.Data.SystemConfig systemConfig && !NearestPlaces.Parse(query, out _))
            entries = entries.Concat(windows.Commands(systemConfig));
        var snapshot = entries.ToArray();
        var searchable = Home && scope == 0
            ? snapshot.Where(p => p.Kind is PlaceKind.Zone or PlaceKind.Aetheryte) : snapshot;
        if (PlaceSearch.Parse(query, scopes[scope]).Scope == PlaceKind.Quest)
            searchable = searchable.Where(p => p.Kind == PlaceKind.Quest);
        results = PaletteBrowser.Distinct(PlaceSearch.Find(searchable, query, scopes[scope], context, recent,
            source.Access, showUnattuned, currentMapMarkersOnly: !Home));
        Array.Clear(questCounts);
        if (PlaceSearch.Parse(query, scopes[scope]).Scope == PlaceKind.Quest) {
            foreach (var result in results) {
                questCounts[0]++;
                var index = Array.IndexOf(questFilters, (QuestBucket?)result.Place.QuestBucket);
                questCounts[index < 0 ? questFilters.Length - 1 : index]++;
            }
            if (questFilters[questFilter] is { } bucket)
                results = results.Where(r => r.Place.QuestBucket == bucket).ToArray();
        }
        expansions = PaletteBrowser.Expansions(snapshot, Home ? results.Select(r => r.Place) : null);
        if (Home && expansion is { } ex)
            results = results.Where(r => r.Place.Expansion == ex).ToArray();
        browserZones = Home && expansion != null ? PaletteBrowser.Zones(results, snapshot) : [];
        if (Home && expansion == null && scope == 0)
            nearby = FindNearby();
        if (NearestPlaces.Parse(query, out _) && context.Player == null)
            message = "Player position is unavailable on the selected map.";
        selected = Math.Clamp(selected, 0, Math.Max(0, results.Length - 1));
    }

    private void ChangeScope(int next) {
        scope = next;
        questFilter = 0;
        query = PlaceSearch.Parse(query, PlaceKind.All).Query;
        queryInputId = $"##place-query-{++openSerial}";
        focusSearch = true;
    }

    private static bool BeginChild(string id, Vector2 size, ImGuiWindowFlags flags) {
        var bar = UiMotion.ScrollbarBegin(id, flags);
#if GRATIS_PREVIEW
        var visible = ImGui.BeginChild(id, size, ImGuiChildFlags.AlwaysUseWindowPadding, flags);
#else
        var visible = ImGui.BeginChild(id, size, false, flags | ImGuiWindowFlags.AlwaysUseWindowPadding);
#endif
        UiMotion.ScrollbarEnd(bar, visible);
        return visible;
    }

    private void Activate(PlaceEntry place) {
        if (MapDrawContext.Current.Config is Gratis.Data.SystemConfig config
            && MapDrawContext.Current.WindowPresets.Activate(place.Id, config)) {
            Close();
            return;
        }
        if (place.Id == MainStoryObjective.Action.Id) {
            source.ShowMainStory();
            Close();
            return;
        }
        if (!string.IsNullOrWhiteSpace(query)) {
            recentQueries.Remove(query);
            recentQueries.Insert(0, query);
            if (recentQueries.Count > 6)
                recentQueries.RemoveAt(recentQueries.Count - 1);
        }
        recent[place.Id] = ++sequence;
        if (recent.Count > 128) {
            string? oldest = null;
            long rank = long.MaxValue;
            foreach (var item in recent)
                if (item.Value < rank) {
                    rank = item.Value;
                    oldest = item.Key;
                }
            if (oldest != null)
                recent.Remove(oldest);
        }
        source.Show(place);
        Close();
    }

    private void Teleport(PlaceEntry place, bool confirmed) {
        message = MapDrawContext.Current.Assistance?.CanOffer(place) == true
            ? MapDrawContext.Current.Assistance.Request(place, confirmed) : source.Teleport(place, confirmed);
        confirming = message.StartsWith("Confirm ", StringComparison.Ordinal) ? place : null;
        if (message.Length == 0 || message == "Teleport requested.")
            Close();
    }
}
