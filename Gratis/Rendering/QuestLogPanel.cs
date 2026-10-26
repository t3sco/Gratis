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

public sealed class QuestLogPanel {
    private readonly HashSet<QuestBucket> collapsed = [];
    private readonly List<(uint Id, Vector4 Bounds)> rows = [];
    private readonly Dictionary<QuestBucket, Vector4> sections = [];
    private IQuestLogSource? source;
    private QuestLogSnapshot snapshot = QuestLogSnapshot.Loading;
    private QuestLogQuest[] filtered = [];
    private string query = "", searched = "\0";
    private int revision = -1;
    private bool focusSearch, keyboard, gesture, swallowEscape, suspended, initialized;
    private ulong character;
    private uint selected;
    private int motionEpoch;
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

    public void OnOpen(MapDrawingConfig config) {
        initialized = true;
        IsOpen = config.IsGratis && config.QuestLogOpenWithMap;
        keyboard = gesture = swallowEscape = focusSearch = false;
    }

    public void OnClose() {
        if (IsOpen || Opacity > .001f)
            motionEpoch++;
        IsOpen = keyboard = gesture = swallowEscape = focusSearch = InputActive = PointerOver = false;
        Opacity = 0;
        HoveredQuestId = 0;
        Layout = default;
        rows.Clear();
        sections.Clear();
    }

    public void Toggle() {
        IsOpen = !IsOpen;
        focusSearch = IsOpen;
    }

    private static bool Over(Vector4 b) => b.Z > b.X && b.W > b.Y
        && ImGui.IsMouseHoveringRect(new(b.X, b.Y), new(b.Z, b.W), false);

    public void Prepare(IMapWindowHost host, Vector2 position, Vector2 size) {
        var context = MapDrawContext.Current;
        if (!initialized)
            OnOpen(host.Config);
        if (!host.Config.IsGratis || context.IsPvP || context.QuestLogSource == null) {
            OnClose();
            return;
        }
        if (!ReferenceEquals(source, context.QuestLogSource)) {
            source = context.QuestLogSource;
            revision = -1;
        }
        snapshot = source.Snapshot;
        if (character != snapshot.Character) {
            if (character != 0)
                OnClose();
            character = snapshot.Character;
            query = "";
            collapsed.Clear();
            selected = 0;
            revision = -1;
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
            && ImGui.IsKeyPressed(ImGuiKey.Escape, false)) {
            IsOpen = focusSearch = false;
            swallowEscape = true;
            keyboard = false;
        }
        PointerOver = !suspended && !popup && (Over(Layout.Toggle) || IsOpen && Over(Layout.Panel));
        gesture = ImGui.IsAnyMouseDown() && (gesture || PointerOver);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !PointerOver)
            keyboard = false;
        if (suspended || popup)
            keyboard = false;
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
        SearchBounds = ListBounds = Vector4.Zero;
        if (!host.Config.IsGratis || context.IsPvP || source == null || suspended)
            return;
        var s = GratisTheme.ChromeScale;
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
            ChromeControls.Tooltip("Quest Log");
        }
        Opacity = UiMotion.Visibility(ImGui.GetID("##quest-log-visibility-" + motionEpoch), IsOpen);
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
                keyboard = IsOpen && ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows);
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
        var context = MapDrawContext.Current;
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var list = ImGui.GetWindowDrawList();
        GratisIcons.Draw(list, start + new Vector2(10, 15) * s, GratisIcons.BookOpen,
            GratisTheme.WarmAccent, 18 * s);
        using (context.GetChromeFont(16 * s, ChromeFontRole.Title)?.Push())
            TextDrawing.Draw(list, start + new Vector2(28, 4) * s, ImGui.GetColorU32(GratisTheme.Primary), "Quest Log");
        ImGui.SetCursorScreenPos(start + new Vector2(MathF.Max(0, width - 30 * s), 0));
        if (ChromeControls.IconButton("##quest-log-close", GratisIcons.X, "Close Quest Log", new(30 * s))) {
            IsOpen = keyboard = focusSearch = false;
            gesture = ImGui.IsAnyMouseDown();
        }
        ImGui.SetCursorScreenPos(start + new Vector2(0, 38 * s));
        if (ImGui.GetContentRegionAvail().Y < 96 * s) {
            ImGui.TextWrapped("Enlarge the map to read quests.");
            return;
        }
        DrawScenario(s, width);
        if (focusSearch && IsOpen) {
            ImGui.SetKeyboardFocusHere();
            focusSearch = false;
        }
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##quest-log-search", "Search accepted quests...", ref query, 256);
        SearchBounds = ItemBounds();
        if (revision != snapshot.Revision || searched != query) {
            filtered = QuestLogModel.Filter(snapshot, query);
            revision = snapshot.Revision;
            searched = query;
        }
        using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push())
            ImGui.TextColored(GratisTheme.Secondary, $"Accepted quests · {filtered.Length}"
                + (query.Length > 0 ? $" of {snapshot.Quests.Count}" : ""));
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
                ImGui.TextWrapped(snapshot.Status == QuestLogStatus.Loading ? "Loading accepted quests..."
                    : "Quest data is unavailable. Try again when the character has loaded.");
                return;
            }
            if (filtered.Length == 0) {
                ImGui.TextWrapped(query.Length > 0 ? "No quests match your search." : "No accepted quests.");
                return;
            }
            var top = ImGui.GetCursorScreenPos().Y + ImGui.GetScrollY();
            foreach (var bucket in QuestBuckets.Order) {
                var quests = filtered.Where(q => q.Bucket == bucket).ToArray();
                if (quests.Length == 0)
                    continue;
                DrawSection(bucket, quests, top, s);
            }
        }
        finally { ImGui.EndChild(); ImGui.PopStyleVar(2); }
    }

    private void DrawScenario(float s, float width) {
        var context = MapDrawContext.Current;
        var list = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var progress = snapshot.Progress;
        var accent = PaletteOverlay.ExpansionAccent(progress?.Expansion ?? 0);
        using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push())
            TextDrawing.Draw(list, start, ImGui.GetColorU32(GratisTheme.Secondary), "Current Main Scenario Quest");
        var title = snapshot.CurrentScenario.Length > 0 ? snapshot.CurrentScenario
            : snapshot.Status == QuestLogStatus.Loading ? "Loading..." : "No main scenario quest accepted";
        TextDrawing.Draw(list, start + new Vector2(0, 22 * s), ImGui.GetColorU32(GratisTheme.Primary),
            ChromeControls.FitLabel(title, width));
        var line = start + new Vector2(0, 47 * s);
        if (progress is { Total: > 0 }) {
            var crest = context.GetExpansionCrest(progress.Icon);
            if (crest.Handle != default) {
                var size = crest.Size * (24 * s / MathF.Max(1, MathF.Max(crest.Size.X, crest.Size.Y)));
                list.AddImage(crest.Handle, line, line + size);
            }
            else {
                var center = line + new Vector2(12 * s);
                list.AddCircleFilled(center, 11 * s, ImGui.GetColorU32(accent with { W = .18f }));
                GratisIcons.Draw(list, center, GratisIcons.BookOpen, accent, 14 * s);
            }
            using (context.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push()) {
                var percent = $"({progress.Percentage}%)";
                var textWidth = TextDrawing.Measure(percent).X;
                TextDrawing.Draw(list, line + new Vector2(30 * s, 4 * s), ImGui.GetColorU32(accent),
                    ChromeControls.FitLabel(progress.Name, width - textWidth - 38 * s));
                TextDrawing.Draw(list, line + new Vector2(width - textWidth, 4 * s),
                    ImGui.GetColorU32(GratisTheme.Primary), percent);
            }
            var bar = line + new Vector2(0, 30 * s);
            var barSize = new Vector2(width, 4 * s);
            list.AddRectFilled(bar, bar + barSize, ImGui.GetColorU32(accent with { W = .12f }),
                GratisTheme.Radius.Pill(barSize));
            var fill = barSize with { X = width * Math.Clamp((float)progress.Completed / progress.Total, 0, 1) };
            list.AddRectFilled(bar, bar + fill, ImGui.GetColorU32(accent), GratisTheme.Radius.Pill(fill));
            ImGui.SetCursorScreenPos(start + new Vector2(0, 42 * s));
            ImGui.Dummy(new(width, 40 * s));
            if (ImGui.IsItemHovered())
                UiMotion.TooltipText($"{progress.Name}: {progress.Completed}/{progress.Total} catalogued MSQ quests completed."
                    + "\nIncludes base story and patches; alternate starting paths count separately.");
        }
        else {
            using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push())
                TextDrawing.Draw(list, line, ImGui.GetColorU32(GratisTheme.Secondary), "Progress unavailable");
            ImGui.SetCursorScreenPos(start + new Vector2(0, 70 * s));
            ImGui.Dummy(Vector2.Zero);
        }
    }

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
        ImGui.SetCursorScreenPos(header);
        if (ChromeControls.Button("##section", new(width, headerHeight)) && query.Length == 0) {
            if (!collapsed.Add(bucket))
                collapsed.Remove(bucket);
        }
        var b = ItemBounds();
        if (b.W > top && b.Y < ImGui.GetWindowPos().Y + ImGui.GetWindowHeight())
            sections[bucket] = b;
        var list = ImGui.GetWindowDrawList();
        list.AddRectFilled(header, header + new Vector2(width, headerHeight),
            ImGui.GetColorU32(GratisTheme.Surface with { W = 1 }), GratisTheme.Radius.Control * s);
        ChromeControls.Feedback(list, header, header + new Vector2(width, headerHeight), fill: true);
        var arrow = header + new Vector2(10, 15) * s;
        var ink = ImGui.GetColorU32(GratisTheme.WarmAccent);
        if (expanded)
            list.AddTriangleFilled(arrow + new Vector2(-4, -2) * s, arrow + new Vector2(4, -2) * s,
                arrow + new Vector2(0, 3) * s, ink);
        else
            list.AddTriangleFilled(arrow + new Vector2(-2, -4) * s, arrow + new Vector2(-2, 4) * s,
                arrow + new Vector2(3, 0) * s, ink);
        TextDrawing.Draw(list, header + new Vector2(24, 6) * s, ImGui.GetColorU32(GratisTheme.Primary),
            QuestBuckets.Label(bucket));
        var count = quests.Length.ToString();
        TextDrawing.Draw(list, header + new Vector2(width - TextDrawing.Measure(count).X - 8 * s, 6 * s),
            ImGui.GetColorU32(GratisTheme.Secondary), count);
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

    private static float RowHeight(float s) => 52 * s;

    private void DrawRow(QuestLogQuest quest, float width, float clipTop, float s) {
        var start = ImGui.GetCursorScreenPos();
        var height = RowHeight(s);
        var size = new Vector2(width, height);
        ImGui.PushID(quest.RowId.ToString());
        var clicked = ChromeControls.Button("##quest", size);
        var hovered = ImGui.IsItemHovered() && ImGui.GetMousePos().Y >= clipTop;
        var b = ItemBounds();
        b.Y = MathF.Max(b.Y, clipTop);
        b.W = MathF.Min(b.W, ImGui.GetWindowPos().Y + ImGui.GetWindowHeight());
        if (b.W > b.Y)
            rows.Add((quest.RowId, b));
        if (clicked && (ImGui.GetIO().NavVisible || ImGui.GetMousePos().Y >= clipTop)) {
            selected = quest.RowId;
            if (source?.Show(quest) == true)
                IsOpen = keyboard = false;
        }
        if (ImGui.IsItemVisible()) {
            var context = MapDrawContext.Current;
            var list = ImGui.GetWindowDrawList();
            PaletteRowDrawing.Background(list, start, size, context.Config.AccentColor, selected == quest.RowId,
                hovered, s);
            ChromeControls.Feedback(list, start, start + size, selected == quest.RowId, fill: false);
            var icon = context.GetIcon(quest.Icon);
            if (icon.Handle != default)
                list.AddImage(icon.Handle, start + new Vector2(8, 10) * s, start + new Vector2(28, 30) * s);
            var text = start + new Vector2(38, 8) * s;
            var textWidth = MathF.Max(1, width - 46 * s);
            var muted = Vector4.Lerp(GratisTheme.Secondary, GratisTheme.Primary, .22f);
            float levelWidth;
            using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push()) {
                var level = $"Lv. {quest.Level}";
                levelWidth = TextDrawing.Measure(level).X + 12 * s;
                PaletteRowDrawing.Pill(list, start + new Vector2(width - levelWidth - 8 * s, 8 * s),
                    level, muted, s);
            }
            TextDrawing.Draw(list, text, ImGui.GetColorU32(GratisTheme.Primary),
                ChromeControls.FitLabel(quest.Name, MathF.Max(1, textWidth - levelWidth - 8 * s)));
            using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push()) {
                var dutyWidth = quest.Duty.Length > 0 ? TextDrawing.Measure("Duty").X + 12 * s : 0;
                var objective = quest.Objective.Length > 0 ? quest.Objective
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
                    + (quest.Targets.Count == 0 ? "\nNo map destination is currently available." : "\nShow on map"));
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
