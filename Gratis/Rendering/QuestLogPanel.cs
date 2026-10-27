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

public sealed partial class QuestLogPanel {
    private readonly HashSet<QuestBucket> collapsed = [];
    private readonly List<(uint Id, Vector4 Bounds)> rows = [];
    private readonly Dictionary<QuestBucket, Vector4> sections = [];
    private IQuestLogSource? source;
    private QuestLogSnapshot snapshot = QuestLogSnapshot.Loading;
    private QuestLogQuest[] filtered = [];
    private string query = "";
    private readonly QuestLogPresentation presentation = new();
    private bool focusSearch, keyboard, gesture, swallowEscape, suspended, initialized;
    private ulong character;
    private uint selected;
    private bool searchOpen, rowKeyboard;
    private float searchReveal;
    public bool SearchOpen => searchOpen;
    public Vector4 SearchToggleBounds { get; private set; }
    private int motionEpoch;
    private int escapeFrame = -1;
    private bool requestedOpen, repeatableOnly;
    public QuestLogTab Tab { get; private set; }
    public Vector4[] TabBounds { get; } = new Vector4[3];
    public Vector4 CloseBounds { get; private set; }
    public event Action? Closed;
    public void OpenFromJournal() {
        requestedOpen = true;
        IsOpen = true;
    }
    public void CloseFromJournal() {
        if (!MapDrawContext.Current.Config.QuestLogPinned) CloseLog(false);
    }
    private void CloseLog(bool notify = true) {
        var wasOpen = IsOpen;
        IsOpen = keyboard = rowKeyboard = focusSearch = false;
        requestedOpen = false;
        HideSearch();
        CloseDetail();
        if (wasOpen && notify) Closed?.Invoke();
    }
    public bool IsOpen { get; private set; }
    public bool PointerOver { get; private set; }
    public bool InputActive { get; private set; }
    public bool CapturesKeyboard => MapDrawContext.Current.Config.IsGratis
        && !MapDrawContext.Current.IsPvP && (keyboard || swallowEscape);
    public QuestLogLayout Layout { get; private set; }
    public Vector4 Bounds => suspended ? Vector4.Zero : Layout.Panel;
    public bool ReservesSpace => !suspended && (IsOpen || Opacity > .001f);
    public Vector4 SearchBounds { get; private set; }
    public Vector4 ListBounds { get; private set; }
    public IReadOnlyList<(uint Id, Vector4 Bounds)> Rows => rows;
    public IReadOnlyDictionary<QuestBucket, Vector4> Sections => sections;
    public string Query => query;
    public IReadOnlyList<QuestLogQuest> Results => filtered;
    public float Opacity { get; private set; }
    public uint HoveredQuestId { get; private set; }
    public Vector4 ProgressBounds { get; private set; }
    public uint ScenarioRimQuestId { get; private set; }
    public float ScenarioRimOpacity { get; private set; }
    public uint CurrentScenarioQuestId => presentation.ScenarioId;

    public void OnOpen(MapDrawingConfig config) {
        initialized = true;
        IsOpen = config.IsGratis && (requestedOpen || config.QuestLogPinned || config.QuestLogOpenWithMap);
        requestedOpen = false;
        keyboard = gesture = swallowEscape = focusSearch = false;
        searchOpen = false;
        query = "";
        CloseDetail();
    }

    public void OnClose() {
        var wasOpen = IsOpen;
        requestedOpen = false;
        if (IsOpen || Opacity > .001f)
            motionEpoch++;
        IsOpen = keyboard = rowKeyboard = gesture = swallowEscape = focusSearch = InputActive = PointerOver = false;
        Opacity = 0;
        detailOpacity = 0;
        detailQuest = null;
        CloseDetail();
        searchOpen = false;
        query = "";
        HoveredQuestId = 0;
        Layout = default;
        rows.Clear();
        sections.Clear();
        if (wasOpen) Closed?.Invoke();
    }

    public void Toggle() {
        if (MapDrawContext.Current.Config.QuestLogPinned)
            return;
        if (IsOpen) CloseLog();
        else IsOpen = true;
    }

    private static bool Over(Vector4 b) => b.Z > b.X && b.W > b.Y
        && ImGui.IsMouseHoveringRect(new(b.X, b.Y), new(b.Z, b.W), false);

    public void Prepare(IMapWindowHost host, Vector2 position, Vector2 size) {
#if GRATIS_PREVIEW
        using var benchmark = BenchmarkProbe.Measure(BenchmarkSection.QuestPrepare);
#endif
        var context = MapDrawContext.Current;
        if (!initialized)
            OnOpen(host.Config);
        if (!host.Config.IsGratis || context.IsPvP || context.QuestLogSource == null) {
            OnClose();
            return;
        }
        if (!ReferenceEquals(source, context.QuestLogSource)) {
            source = context.QuestLogSource;
        }
        snapshot = source.Snapshot;
        if (character != snapshot.Character) {
            if (character != 0)
                OnClose();
            character = snapshot.Character;
            query = "";
            collapsed.Clear();
            selected = 0;
        }
        presentation.Update(snapshot);
        if (host.Config.QuestLogPinned)
            IsOpen = true;
        if (detailQuest != null) {
            if (Tab == QuestLogTab.Unaccepted && !detailQuest.ClientKnown && !host.Config.QuestLogShowLocked) {
                detailQuest = null;
                detailOpacity = 0;
                CloseDetail();
            }
            else {
                detailQuest = presentation.Find(detailQuest.RowId);
                if (detailQuest == null) CloseDetail();
            }
        }
        suspended = host.Palette?.IsOpen == true;
        var (min, max) = ChromeLayout.RawSafeBounds(position, size);
        var s = GratisTheme.ChromeScale;
        var top = min.Y;
        if (host.Config.MoveHandle) {
            var (shapeMin, shapeMax) = Gratis.Classes.MapShape.Bounds(position, size);
            var handle = MoveHandleMath.Geometry(shapeMin, shapeMax, host.Config.MapCircle, context.GlobalScale);
            top = MathF.Max(top, MathF.Ceiling(handle.HitMax.Y + GratisTheme.Gap * s));
        }
        Layout = QuestLogLayout.Resolve(min, max, top, host.Config.EffectiveQuestLogWidth, s);
        swallowEscape &= ImGui.IsKeyDown(ImGuiKey.Escape);
        var popup = ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel);
        if (!suspended && !popup && IsOpen && (host.MapFocused || keyboard || PointerOver)
            && escapeFrame != ImGui.GetFrameCount() && ImGui.IsKeyPressed(ImGuiKey.Escape, false)) {
            escapeFrame = ImGui.GetFrameCount();
            if (searchOpen)
                HideSearch();
            else if (DetailOpen)
                CloseDetail();
            else if (!host.Config.QuestLogPinned)
                CloseLog();
            swallowEscape = true;
            keyboard = IsOpen;
        }
        if (!suspended && !popup && IsOpen && (host.MapFocused || keyboard) && ImGui.GetIO().KeyCtrl
            && ImGui.IsKeyPressed(ImGuiKey.F, false))
            ShowSearch();
        PointerOver = !suspended && !popup && (Over(Layout.Toggle) || Over(Layout.Pin)
            || IsOpen && (Over(Layout.Panel) || DetailOpen && Over(Layout.Detail)));
        gesture = ImGui.IsAnyMouseDown() && (gesture || PointerOver);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)
            && (!PointerOver || !rows.Any(r => Over(r.Bounds))))
            rowKeyboard = false;
        if (!host.MapFocused)
            rowKeyboard = false;
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !PointerOver)
            keyboard = false;
        if (suspended || popup)
            keyboard = rowKeyboard = false;
        InputActive = PointerOver || gesture || CapturesKeyboard;
        HoveredQuestId = IsOpen && !suspended && !popup
            ? rows.FirstOrDefault(r => Over(r.Bounds)).Id : 0;
        if (PointerOver || IsOpen && !suspended)
            context.Chrome.Reveal(host.Now);
        if (CapturesKeyboard || PointerOver)
            ImGui.SetNextFrameWantCaptureKeyboard(true);
    }

    public void Draw(IMapWindowHost host) {
        var context = MapDrawContext.Current;
        rows.Clear();
        sections.Clear();
        SearchBounds = ListBounds = ProgressBounds = Vector4.Zero;
        ScenarioRimQuestId = 0;
        ScenarioRimOpacity = 0;
        if (!host.Config.IsGratis || context.IsPvP || source == null || suspended)
            return;
        var s = GratisTheme.ChromeScale;
        Layout = Layout.AvoidChrome(MapToolbarDrawing.LastBounds, MapWindowDrawing.TitleBounds,
            MapWindowDrawing.CoordinateBounds, s);
        var toggle = Layout.Toggle;
        var parent = ImGui.GetWindowDrawList();
        var alpha = context.Chrome.ControlsAlpha(host.Config);
        if (alpha * ImGui.GetStyle().Alpha >= .05f) {
            using var fade = FrameScopes.PushStyle(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * alpha, true);
            var min = new Vector2(toggle.X, toggle.Y);
            var size = new Vector2(toggle.Z - toggle.X, toggle.W - toggle.Y);
            MapWindowDrawing.DrawSurface(parent, min, min + size, GratisTheme.Radius.Card * s);
            ImGui.SetCursorScreenPos(min);
            if (ChromeControls.Button("##quest-log-toggle", size))
                Toggle();
            ChromeControls.Feedback(parent, min, min + size, IsOpen, radius: GratisTheme.Radius.Card * s);
            GratisIcons.Draw(parent, min + size / 2, GratisIcons.BookOpen,
                IsOpen ? GratisTheme.WarmAccent : GratisTheme.Secondary, GratisTheme.IconSize * s);
            ChromeControls.Tooltip(host.Config.QuestLogPinned ? "Quest Log · pinned open" : "Quest Log");
            min = new(Layout.Pin.X, Layout.Pin.Y);
            MapWindowDrawing.DrawSurface(parent, min, min + size, GratisTheme.Radius.Card * s);
            ImGui.SetCursorScreenPos(min);
            if (ChromeControls.Button("##quest-log-pin", size)) {
                host.Config.QuestLogPinned = !host.Config.QuestLogPinned;
                if (host.Config.QuestLogPinned)
                    IsOpen = true;
            }
            ChromeControls.Feedback(parent, min, min + size, host.Config.QuestLogPinned,
                radius: GratisTheme.Radius.Card * s);
            GratisIcons.Draw(parent, min + size / 2, GratisIcons.Pin,
                host.Config.QuestLogPinned ? GratisTheme.WarmAccent : GratisTheme.Secondary, GratisTheme.IconSize * s);
            ChromeControls.Tooltip(host.Config.QuestLogPinned ? "Unpin Quest Log" : "Pin Quest Log open");
        }
        Opacity = UiMotion.Visibility(ImGui.GetID("##quest-log-visibility-" + motionEpoch), IsOpen);
        DrawDetail(host, s, parent);
        if (Layout.DetailOverlay && (DetailOpen || detailOpacity > .001f))
            return;
        var bounds = Layout.Panel;
        if (Opacity <= .001f || bounds.W - bounds.Y < 40 * s)
            return;
        var origin = new Vector2(bounds.X, bounds.Y);
        var extent = new Vector2(bounds.Z - bounds.X, bounds.W - bounds.Y);
        using var font = context.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push();
        var slide = UiMotion.Full ? new Vector2((1 - Opacity) * 18 * s, 0) : Vector2.Zero;
        using (var surface = new UiMotion.VisualScope(parent, origin, Opacity, slide))
            MapWindowDrawing.DrawSurface(parent, origin, origin + extent, GratisTheme.Radius.Popup * s,
                surfaceOpacity: 1);
        ImGui.SetCursorScreenPos(origin);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 6) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, GratisTheme.Radius.Control * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 8) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 4 * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, GratisTheme.Radius.Pill(new Vector2(4 * s)));
        ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Primary);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, GratisTheme.Secondary);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(1, 1, 1, .055f));
        var visible = BeginPanel(extent, ImGuiWindowFlags.NoBackground
            | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse
            | (!IsOpen ? ImGuiWindowFlags.NoInputs : ImGuiWindowFlags.None));
        try {
            if (!visible)
                return;
            using var motion = new UiMotion.VisualScope(ImGui.GetWindowDrawList(), origin, Opacity, slide);
            ImGui.BeginDisabled(!IsOpen);
            try {
                DrawContent(s);
                keyboard = IsOpen && (rowKeyboard || detailKeyboard || ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows));
                if (keyboard || PointerOver) {
                    context.Chrome.Reveal(host.Now);
                    ImGui.SetNextFrameWantCaptureKeyboard(true);
                }
            }
            finally { ImGui.EndDisabled(); }
        }
        finally { ImGui.EndChild(); ImGui.PopStyleVar(6); ImGui.PopStyleColor(4); }
    }

    private void DrawContent(float s) {
#if GRATIS_PREVIEW
        using var benchmark = BenchmarkProbe.Measure(BenchmarkSection.QuestList);
#endif
        var context = MapDrawContext.Current;
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var list = ImGui.GetWindowDrawList();
        GratisIcons.Draw(list, start + new Vector2(10, 15) * s, GratisIcons.BookOpen,
            GratisTheme.WarmAccent, 18 * s);
        using (context.GetChromeFont(16 * s, ChromeFontRole.Title)?.Push())
            TextDrawing.Draw(list, start + new Vector2(28, 4) * s, ImGui.GetColorU32(GratisTheme.Primary), "Quest Log");
        ImGui.SetCursorScreenPos(start + new Vector2(MathF.Max(0, width - 64 * s), 0));
        var searchButtonStart = ImGui.GetCursorScreenPos();
        if (ChromeControls.IconButton("##quest-log-search-toggle", GratisIcons.Search,
            searchOpen ? "Hide search · Esc" : "Search quests · Ctrl+F", new(30 * s))) {
            if (searchOpen) HideSearch(); else ShowSearch();
        }
        SearchToggleBounds = ItemBounds();
        ChromeControls.Feedback(list, searchButtonStart, searchButtonStart + new Vector2(30 * s), searchOpen);
        ImGui.SetCursorScreenPos(start + new Vector2(MathF.Max(0, width - 30 * s), 0));
        var close = ChromeControls.IconButton("##quest-log-close", GratisIcons.X,
            context.Config.QuestLogPinned ? "Unpin to close Quest Log" : "Close Quest Log", new(30 * s));
        CloseBounds = ItemBounds();
        if (close && !context.Config.QuestLogPinned) {
            CloseLog();
            gesture = ImGui.IsAnyMouseDown();
        }
        ImGui.SetCursorScreenPos(start + new Vector2(0, 38 * s));
        if (ImGui.GetContentRegionAvail().Y < 56 * s) {
            EmptyState.Draw("Enlarge the map to read quests.", EmptyFaces.QuestHelper);
            return;
        }
        DrawTabs(s, width);
        var shortPanel = ImGui.GetContentRegionAvail().Y < 180 * s;
        if (!shortPanel && Tab == QuestLogTab.Accepted)
            DrawScenario(s, width);
        searchReveal = UiMotion.Visibility(ImGui.GetID("##quest-log-search-reveal"), searchOpen);
        if (searchOpen || searchReveal > .001f) {
            var searchStart = ImGui.GetCursorScreenPos();
            var searchHeight = ImGui.GetFrameHeight() + 6 * s;
            list.PushClipRect(searchStart, searchStart + new Vector2(width, searchHeight * searchReveal), true);
            using (var reveal = new UiMotion.VisualScope(list, searchStart, searchReveal, Vector2.Zero)) {
                if (focusSearch && searchOpen) {
                    ImGui.SetKeyboardFocusHere();
                    focusSearch = false;
                }
                ImGui.BeginDisabled(!searchOpen);
                var fieldQuery = query;
                TextField.Draw("##quest-log-search", ref fieldQuery, new(FieldFaces.Quest));
                if (searchOpen)
                    query = fieldQuery;
                ImGui.EndDisabled();
                SearchBounds = ItemBounds();
            }
            list.PopClipRect();
            ImGui.SetCursorScreenPos(searchStart + new Vector2(0, searchHeight * searchReveal));
        }
        presentation.Filter(query, Tab, context.Config.QuestLogShowLocked, repeatableOnly);
        filtered = presentation.Filtered;
        if (!shortPanel) {
            using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push())
                ImGui.TextColored(GratisTheme.Secondary, $"{QuestLogText.TabLabel(Tab)} quests · {filtered.Length}");
        }
        if (Tab == QuestLogTab.Unaccepted)
            EmptyState.Draw(QuestLogText.NearbySubtitle, EmptyFaces.QuestHelper);
        if (Tab == QuestLogTab.Completed && snapshot.HistoryLoading)
            EmptyState.Draw("Checking completion history...", EmptyFaces.QuestHelper);
        if (Tab == QuestLogTab.Completed && snapshot.HistoryCapped || Tab == QuestLogTab.Unaccepted && snapshot.UnacceptedCapped)
            EmptyState.Draw("Showing the first 500 quests by name.", EmptyFaces.QuestHelper);
        if (ImGui.GetContentRegionAvail().Y > 100 * s)
            using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push())
                ImGui.TextColored(GratisTheme.Secondary, "Click: summary · Right-click: map");
        var remaining = ImGui.GetContentRegionAvail();
        if (remaining.Y < 8 * s)
            return;
        remaining.X = MathF.Max(1, remaining.X - 4 * s);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        UiMotion.Child("##quest-log-rows", remaining, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollWithMouse
            | (!IsOpen ? ImGuiWindowFlags.NoInputs : ImGuiWindowFlags.None));
        try {
            using var motion = new UiMotion.VisualScope(ImGui.GetWindowDrawList(), ImGui.GetWindowPos(), Opacity,
                UiMotion.Full ? new Vector2((1 - Opacity) * 18 * s, 0) : Vector2.Zero);
            var min = ImGui.GetWindowPos();
            var max = min + ImGui.GetWindowSize();
            ListBounds = new(min.X, min.Y, max.X, max.Y);
            UiMotion.Scroll();
            if (snapshot.Status != QuestLogStatus.Ready) {
                EmptyState.Draw(snapshot.Status == QuestLogStatus.Loading ? "Loading accepted quests..."
                    : "Quest data is unavailable. Try again when the character has loaded.", EmptyFaces.Quest);
                return;
            }
            if (filtered.Length == 0) {
                EmptyState.Draw(query.Length > 0 ? "No quests match your search." : QuestLogText.EmptyTab(Tab), EmptyFaces.Quest);
                return;
            }
            var top = ImGui.GetCursorScreenPos().Y + ImGui.GetScrollY();
            foreach (var group in presentation.Groups)
                DrawSection(group.Bucket, group.Quests, top, s);
        }
        finally { ImGui.EndChild(); ImGui.PopStyleVar(2); }
    }

    private readonly string[] tabIds = ["##quest-tab-0", "##quest-tab-1", "##quest-tab-2"];

    private void DrawTabs(float s, float width) {
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push();
        for (var i = 0; i < 3; i++) {
            if (i > 0) ImGui.SameLine(0, TabFaces.QuestLog.Gap * s);
            var tab = new TabStrip(tabIds[i], TabFaces.QuestLog, width, s);
            if (tab.Clicked) {
                Tab = (QuestLogTab)i;
                selected = 0;
                CloseDetail();
            }
            TabBounds[i] = ItemBounds();
            tab.Draw(QuestLogText.TabLabel((QuestLogTab)i), Tab == (QuestLogTab)i);
        }
        if (ImGui.GetContentRegionAvail().Y < 140 * s) return;
        var start = ImGui.GetCursorScreenPos();
        if (ChromeControls.Button("##repeatable-filter", new(TextDrawing.Measure("Repeatable").X + 16 * s, 24 * s))) {
            repeatableOnly = !repeatableOnly;
        }
        PaletteRowDrawing.Pill(ImGui.GetWindowDrawList(), start + new Vector2(2 * s), "Repeatable",
            repeatableOnly ? GratisTheme.WarmAccent : GratisTheme.Secondary, s);
    }

    private void DrawScenario(float s, float width) {
        if (snapshot.Progress is not { Total: > 0 } progress)
            return;
        var context = MapDrawContext.Current;
        var list = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var accent = PaletteOverlay.ExpansionAccent(progress.Expansion);
        var crest = context.GetExpansionCrest(progress.Icon);
        if (crest.Handle != default) {
            var size = crest.Size * (24 * s / MathF.Max(1, MathF.Max(crest.Size.X, crest.Size.Y)));
            var iconStart = start + new Vector2(12 * s) - size / 2;
            list.AddImage(crest.Handle, iconStart, iconStart + size);
        }
        else
            GratisIcons.Draw(list, start + new Vector2(12 * s), GratisIcons.BookOpen, accent, 18 * s);
        using (context.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push()) {
            var percent = $"{progress.Percentage}%";
            var textWidth = TextDrawing.Measure(percent).X;
            TextDrawing.Draw(list, start + new Vector2(32 * s, 4 * s), ImGui.GetColorU32(accent),
                ChromeControls.FitLabel(progress.Name, width - textWidth - 40 * s));
            TextDrawing.Draw(list, start + new Vector2(width - textWidth, 4 * s),
                ImGui.GetColorU32(GratisTheme.Primary), percent);
        }
        var bar = start + new Vector2(0, 30 * s);
        var barSize = new Vector2(width, 4 * s);
        list.AddRectFilled(bar, bar + barSize, ImGui.GetColorU32(accent with { W = .12f }),
            GratisTheme.Radius.Pill(barSize));
        var fill = barSize with { X = width * Math.Clamp((float)progress.Completed / progress.Total, 0, 1) };
        list.AddRectFilled(bar, bar + fill, ImGui.GetColorU32(accent), GratisTheme.Radius.Pill(fill));
        ImGui.Dummy(new(width, 42 * s));
        ProgressBounds = ItemBounds();
        if (ImGui.IsItemHovered())
            UiMotion.TooltipText($"{progress.Name}: {progress.Completed}/{progress.Total} catalogued MSQ quests completed."
                + "\nIncludes base story and patches; alternate starting paths count separately.");
    }

    private readonly Dictionary<QuestBucket, SectionHeaderCache> sectionHeaders = [];

    private void DrawSection(QuestBucket bucket, QuestLogQuest[] quests, float top, float s) {
        var start = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1, ImGui.GetContentRegionAvail().X - 6 * s);
        var headerHeight = 32 * s;
        var gap = GratisTheme.Gap * s;
        var expanded = query.Length > 0 || !collapsed.Contains(bucket);
        var height = headerHeight + gap + (expanded ? quests.Length * (RowHeight(s) + gap) : 0);
        var headerY = MathF.Max(start.Y, MathF.Min(top, start.Y + height - headerHeight - gap));
        var header = new Vector2(start.X, headerY);
        ImGui.PushID((int)bucket);
        if (!sectionHeaders.TryGetValue(bucket, out var headingCache))
            sectionHeaders.Add(bucket, headingCache = new());
        if (SectionHeader.Sticky("##section", QuestBuckets.Label(bucket), headingCache.Count(quests.Length),
            header, width, s, expanded) && query.Length == 0) {
            if (!collapsed.Add(bucket))
                collapsed.Remove(bucket);
        }
        var b = ItemBounds();
        if (b.W > top && b.Y < ImGui.GetWindowPos().Y + ImGui.GetWindowHeight())
            sections[bucket] = b;
        var list = ImGui.GetWindowDrawList();
        ImGui.SetCursorScreenPos(start + new Vector2(0, headerHeight));
        ImGui.Dummy(new(0, gap));
        if (expanded) {
            list.PushClipRect(new(start.X, MathF.Max(top, headerY + headerHeight + gap)),
                ImGui.GetWindowPos() + ImGui.GetWindowSize(), true);
            foreach (var quest in quests)
                DrawRow(quest, width, headerY + headerHeight + gap, s);
            list.PopClipRect();
        }
        ImGui.PopID();
    }

    public static float ScenarioRimAlpha(double time, bool fullMotion) => fullMotion
        ? .16f + .06f * MathF.Sin((float)(time % 5) * MathF.Tau / 5) : .12f;

    private static float RowHeight(float s) => 52 * s;

    private void DrawRow(QuestLogQuest quest, float width, float clipTop, float s) {
#if GRATIS_PREVIEW
        using var benchmark = BenchmarkProbe.Measure(BenchmarkSection.QuestRow);
#endif
        var start = ImGui.GetCursorScreenPos();
        var height = RowHeight(s);
        var size = new Vector2(width, height);
        ImGui.PushID(presentation.For(quest).Id);
        var clicked = ChromeControls.Button("##quest", size);
        var hovered = ImGui.IsItemHovered() && ImGui.GetMousePos().Y >= clipTop;
        var b = ItemBounds();
        b.Y = MathF.Max(b.Y, clipTop);
        b.W = MathF.Min(b.W, ImGui.GetWindowPos().Y + ImGui.GetWindowHeight());
        if (b.W > b.Y)
            rows.Add((quest.RowId, b));
        var focused = ImGui.IsItemFocused() || !ImGui.GetIO().NavVisible && rowKeyboard && selected == quest.RowId;
        var mapKey = focused && !searchOpen && (ImGui.IsKeyPressed(ImGuiKey.M, false)
            || ImGui.GetIO().KeyShift && ImGui.IsKeyPressed(ImGuiKey.Enter, false));
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right) || mapKey) {
            rowKeyboard = true;
            selected = quest.RowId;
            ShowOnMap(quest);
        }
        else if (focused && !searchOpen && ImGui.IsKeyPressed(ImGuiKey.Enter, false)
            || clicked && (ImGui.GetIO().NavVisible || ImGui.GetMousePos().Y >= clipTop)) {
            rowKeyboard = true;
            selected = quest.RowId;
            if (DetailOpen && detailQuest?.RowId == quest.RowId)
                CloseDetail();
            else {
                source?.RequestDetail(quest.RowId);
                detailQuest = quest;
                DetailOpen = true;
            }
        }
        if (ImGui.IsItemVisible()) {
            var context = MapDrawContext.Current;
            var list = ImGui.GetWindowDrawList();
            PaletteRowDrawing.Background(list, start, size, context.Config.AccentColor, selected == quest.RowId,
                hovered, s);
            ChromeControls.Feedback(list, start, start + size, selected == quest.RowId, fill: false);
            if (quest.RowId == CurrentScenarioQuestId) {
                ScenarioRimQuestId = quest.RowId;
                ScenarioRimOpacity = ScenarioRimAlpha(ImGui.GetTime(), UiMotion.Full);
                var color = GratisTheme.WarmAccent with { W = ScenarioRimOpacity };
                var radius = GratisTheme.Radius.Control * s;
                MapWindowDrawing.DrawGradientRing(list, start, start + size, radius, 4 * s, color with { W = color.W / 2 }, 2);
                FrameMaterialDrawing.DrawStroke(list, start, start + size, radius, s, color);
            }

            var icon = RowIcon(quest);
            if (icon.Handle != default) {
                var iconSize = icon.Size * (24 * s / MathF.Max(1, MathF.Max(icon.Size.X, icon.Size.Y)));
                var iconMin = start + new Vector2(18, 25) * s - iconSize / 2;
                list.AddImage(icon.Handle, iconMin, iconMin + iconSize);
            }
            var text = start + new Vector2(38, 8) * s;
            var textWidth = MathF.Max(1, width - 46 * s);
            var muted = Vector4.Lerp(GratisTheme.Secondary, GratisTheme.Primary, .22f);
            float levelWidth;
            using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push()) {
                var level = presentation.For(quest).Level;
                levelWidth = TextDrawing.Measure(level).X + 12 * s;
                PaletteRowDrawing.Pill(list, start + new Vector2(width - levelWidth - 8 * s, 8 * s),
                    level, muted, s);
            }
            TextDrawing.Draw(list, text, ImGui.GetColorU32(GratisTheme.Primary),
                ChromeControls.FitLabel(quest.Name, MathF.Max(1, textWidth - levelWidth - 8 * s)));
            using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push()) {
                var dutyWidth = quest.Duty.Length > 0 ? TextDrawing.Measure("Duty").X + 12 * s : 0;
                var objective = Tab == QuestLogTab.Unaccepted ? QuestLogText.Available : quest.Objective.Length > 0 ? quest.Objective
                    : quest.Targets.Count == 0 ? "No map destination" : "Objective unavailable";
                TextDrawing.Draw(list, text + new Vector2(0, 20 * s), ImGui.GetColorU32(muted),
                    ChromeControls.FitLabel(objective, MathF.Max(1, textWidth - (dutyWidth > 0 ? dutyWidth + 8 * s : 0))));
                if (dutyWidth > 0)
                    PaletteRowDrawing.Pill(list, start + new Vector2(width - dutyWidth - 8 * s, 28 * s),
                        "Duty", GratisTheme.Crystal, s);
            }
            if (hovered)
                ChromeControls.Tooltip(quest.Name + (quest.Objective.Length > 0 ? "\n" + quest.Objective : "")
                    + (quest.Duty.Length > 0 ? "\n" + quest.Duty : "")
                    + (quest.Targets.Count == 0 ? "\nNo map destination is currently available." : "\nLeft-click: summary · Right-click: show on map\nEnter: summary · Shift+Enter / M: map"));
        }
        ImGui.SetCursorScreenPos(start + new Vector2(0, height));
        ImGui.Dummy(new(0, GratisTheme.Gap * s));
        ImGui.PopID();
    }

    private static Vector4 ItemBounds() {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        return new(min.X, min.Y, max.X, max.Y);
    }

    private static bool BeginPanel(Vector2 size, ImGuiWindowFlags flags) =>
#if GRATIS_PREVIEW
        ImGui.BeginChild("##quest-log", size, ImGuiChildFlags.AlwaysUseWindowPadding, flags);
#else
        ImGui.BeginChild("##quest-log", size, false, flags | ImGuiWindowFlags.AlwaysUseWindowPadding);
#endif

    public void DrawMarkerHover(Gratis.Classes.MarkerInfo marker) {
        if (!MapDrawContext.Current.Config.IsGratis || !IsOpen || HoveredQuestId == 0
            || marker.ObjectiveId is not { } id || (id < 65536 ? id + 65536 : id) != HoveredQuestId)
            return;
        var center = marker.Position + marker.Offset + ImGui.GetWindowPos();
        var radius = MarkerVisual.HalfSize(marker).Length() + 4 * GratisTheme.ChromeScale;
        if (!Gratis.Classes.MapShape.ContainsSymbol(center, new(radius)))
            return;
        var pulse = UiMotion.Full ? .65f + .2f * MathF.Sin((float)ImGui.GetTime() * 4) : .75f;
        ImGui.GetWindowDrawList().AddCircle(center, radius,
            ImGui.GetColorU32(GratisTheme.Crystal with { W = pulse }), 32, GratisTheme.ChromeScale);
    }
}
