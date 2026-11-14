using System;
using System.Collections.Generic;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

/// <summary>
/// The Shared FATE panel: a toolbar-corner button left of the FATE button, opening flat cards for the game's Shared
/// FATE zones (rank and progress toward the next rank) with the Bicolor Gemstone total. Read-only: it shows what the
/// game last loaded and never opens or refreshes the game's own window.
/// </summary>
public sealed class SharedFatePanel {
    private const float PanelWidth = 300;
    private const float MinimumWidth = 220;
    private const float CardHeight = 62;
    private static readonly PillProfile MaxChip = new() {
        Padding = new(14, 4), Inset = new(7, 2), Fill = GratisTheme.WarmAccent with { W = .20f },
        Ink = GratisTheme.WarmAccent,
    };

    private readonly List<(uint Id, Vector4 Bounds)> cards = [];
    private readonly Vector4[] tabBounds = new Vector4[SharedFateList.TabCount];
    private readonly SharedFateZone[][] ordered = [[], [], []];
    private readonly string[] tabIds = ["##shared-fate-tab-0", "##shared-fate-tab-1", "##shared-fate-tab-2"];
    private ISharedFateSource? source;
    private SharedFateSnapshot snapshot = SharedFateSnapshot.Empty;
    private bool swallowEscape, gesture, suspended, requestedOpen, initialized, questWasOpen, fateWasOpen, userTab;
    private int motionEpoch, escapeFrame = -1;
    private float minimumX;
    private QuestLogLayout corner;
    private bool cornerValid;
    private Vector4 toggle, panel;

    public bool IsOpen { get; private set; }
    public bool PointerOver { get; private set; }
    public bool InputActive { get; private set; }
    public float Opacity { get; private set; }
    public string Reason { get; private set; } = "";
    /// <summary>The tab being shown (0-2).</summary>
    public int Tab { get; private set; }
    /// <summary>What the cards area says instead of cards, or empty when it shows cards.</summary>
    public string EmptyMessage { get; private set; } = "";
    public Vector4 ToggleBounds => toggle;
    public Vector4 Bounds => suspended ? Vector4.Zero : panel;
    public Vector4 CloseBounds { get; private set; }
    public Vector4 ListBounds { get; private set; }
    public Vector4 GemBounds { get; private set; }
    public IReadOnlyList<Vector4> TabBounds => tabBounds;
    public IReadOnlyList<(uint Id, Vector4 Bounds)> Cards => cards;
    public IReadOnlyList<SharedFateZone> Results => ordered[Math.Clamp(Tab, 0, SharedFateList.TabCount - 1)];
    public bool CapturesKeyboard => MapDrawContext.Current.Config.IsGratis && swallowEscape;

    public void Toggle() {
        if (IsOpen)
            Close();
        else if (Reason.Length == 0)
            Open();
    }

    private void Open() {
        var context = MapDrawContext.Current;
        // The quest log, the FATE panel and this panel share one corner: the one opened last replaces the others,
        // except a pinned quest log, which stays and this panel moves beside it.
        if (context.QuestLog.IsOpen && !context.Config.QuestLogPinned)
            context.QuestLog.Toggle();
        context.Fates.Close();
        IsOpen = true;
        questWasOpen = context.QuestLog.IsOpen;
        fateWasOpen = context.Fates.IsOpen;
    }

    public void Close() {
        IsOpen = requestedOpen = false;
    }

    public void OnOpen() {
        initialized = true;
        IsOpen = requestedOpen;
        requestedOpen = false;
        swallowEscape = gesture = false;
    }

    public void OnClose() {
        if (IsOpen || Opacity > .001f)
            motionEpoch++;
        IsOpen = requestedOpen = swallowEscape = gesture = InputActive = PointerOver = userTab = false;
        Opacity = 0;
        Tab = 0;
        snapshot = SharedFateSnapshot.Empty;
        for (var index = 0; index < ordered.Length; index++)
            ordered[index] = [];
        EmptyMessage = "";
        cards.Clear();
        cornerValid = false;
        toggle = panel = CloseBounds = ListBounds = GemBounds = Vector4.Zero;
        Array.Clear(tabBounds);
    }

    private static bool Over(Vector4 b) => b.Z > b.X && b.W > b.Y
        && ImGui.IsMouseHoveringRect(new(b.X, b.Y), new(b.Z, b.W), false);

    public void Prepare(IMapWindowHost host, Vector2 position, Vector2 size) {
        var context = MapDrawContext.Current;
        if (!initialized)
            OnOpen();
        if (!host.Config.IsGratis || context.SharedFateSource == null) {
            OnClose();
            Reason = "";
            source = null;
            return;
        }
        source = context.SharedFateSource;
        suspended = host.Palette?.IsOpen == true;
        var (min, max) = ChromeLayout.RawSafeBounds(position, size);
        minimumX = min.X;
        // No room for the button without squeezing the toolbar: it is not shown, and nothing is reserved for it.
        if (!ChromeLayout.SharedFateButtonFits(context, max.X - min.X)) {
            OnClose();
            Reason = "";
            return;
        }
        var s = GratisTheme.ChromeScale;
        var top = min.Y;
        if (host.Config.MoveHandle) {
            var (shapeMin, shapeMax) = Gratis.Classes.MapShape.Bounds(position, size);
            var handle = MoveHandleMath.Geometry(shapeMin, shapeMax, host.Config.MapCircle, context.GlobalScale);
            top = MathF.Max(top, MathF.Ceiling(handle.HitMax.Y + GratisTheme.Gap * s));
        }
        corner = QuestLogLayout.Resolve(min, max, top, PanelWidth, s);
        cornerValid = true;
        var button = GratisTheme.ToolbarHeight * s;
        // This button is the last one in the row: it sits left of the FATE button and the quest log's two.
        var shift = (ChromeLayout.CornerButtons(context, max.X - min.X) - 1) * (button + GratisTheme.Gap * s);
        var x = MathF.Max(min.X, corner.Toggle.X - shift);
        toggle = new(x, corner.Toggle.Y, x + button, corner.Toggle.W);
        panel = Place(corner, context, host.Config, s, out var room);
        Reason = SharedFateList.Reason(context.IsPvP, source);
        if (Reason.Length == 0 && !room)
            Reason = SharedFateList.NoRoomReason;
        if (Reason.Length > 0)
            Close();
        // The quest log or the FATE panel was opened after this panel: it takes the corner.
        var questOpen = context.QuestLog.IsOpen;
        var fateOpen = context.Fates.IsOpen;
        if (IsOpen && (questOpen && !questWasOpen && !host.Config.QuestLogPinned || fateOpen && !fateWasOpen))
            Close();
        questWasOpen = questOpen;
        fateWasOpen = fateOpen;
        swallowEscape &= ImGui.IsKeyDown(ImGuiKey.Escape);
        var popup = ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel);
        if (!suspended && !popup && IsOpen && (host.MapFocused || PointerOver) && escapeFrame != ImGui.GetFrameCount()
            && ImGui.IsKeyPressed(ImGuiKey.Escape, false)) {
            escapeFrame = ImGui.GetFrameCount();
            Close();
            swallowEscape = true;
        }
        PointerOver = !suspended && !popup && (Over(toggle) || IsOpen && Over(panel));
        gesture = ImGui.IsAnyMouseDown() && (gesture || PointerOver);
        InputActive = PointerOver || gesture;
        if (PointerOver || IsOpen && !suspended)
            context.Chrome.Reveal(host.Now);
        if (CapturesKeyboard)
            ImGui.SetNextFrameWantCaptureKeyboard(true);
    }

    // The panel sits in the quest log's slot. A pinned quest log (and its open detail) is always there, so the panel
    // moves to the left of them; when that leaves no room the button is disabled with a reason instead.
    private Vector4 Place(in QuestLogLayout layout, MapDrawContext context, MapDrawingConfig config, float scale,
        out bool room) {
        var result = layout.Panel;
        room = true;
        if (!config.QuestLogPinned || context.QuestLogSource == null || context.IsPvP)
            return result;
        var gap = GratisTheme.Gap * scale;
        var right = result.Z;
        var quest = context.QuestLog.Bounds;
        if (quest.Z > quest.X)
            right = MathF.Min(right, quest.X - gap);
        var detail = context.QuestLog.DetailBounds;
        if (detail.Z > detail.X)
            right = MathF.Min(right, detail.X - gap);
        if (right >= result.Z)
            return result;
        var width = MathF.Min(result.Z - result.X, right - minimumX);
        room = width >= MinimumWidth * scale;
        return result with { X = right - MathF.Max(0, width), Z = right };
    }

    public void Draw(IMapWindowHost host) {
        var context = MapDrawContext.Current;
        cards.Clear();
        ListBounds = CloseBounds = GemBounds = Vector4.Zero;
        Array.Clear(tabBounds);
        if (!host.Config.IsGratis || source == null || !cornerValid || suspended)
            return;
        var s = GratisTheme.ChromeScale;
        var parent = ImGui.GetWindowDrawList();
        var alpha = context.Chrome.ControlsAlpha(host.Config);
        if (alpha * ImGui.GetStyle().Alpha >= .05f)
            DrawToggle(parent, host, alpha, s);
        Opacity = UiMotion.Visibility(ImGui.GetID("##shared-fate-panel-visibility-" + motionEpoch), IsOpen);
        var avoided = corner.AvoidChrome(MapToolbarDrawing.LastBounds, MapWindowDrawing.TitleBounds,
            MapWindowDrawing.CoordinateBounds, s);
        panel = panel with { Y = avoided.Panel.Y, W = avoided.Panel.W };
        var bounds = panel;
        if (Opacity <= .001f || bounds.W - bounds.Y < 40 * s)
            return;
        var origin = new Vector2(bounds.X, bounds.Y);
        var extent = new Vector2(bounds.Z - bounds.X, bounds.W - bounds.Y);
        using var font = context.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push();
        var slide = UiMotion.Full ? new Vector2((1 - Opacity) * 18 * s, 0) : Vector2.Zero;
        using (var surface = new UiMotion.VisualScope(parent, origin, Opacity, slide))
            MapWindowDrawing.DrawSurface(parent, origin, origin + extent, GratisTheme.Radius.Popup * s, surfaceOpacity: 1);
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
        var visible = BeginPanel(extent, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse | (!IsOpen ? ImGuiWindowFlags.NoInputs : ImGuiWindowFlags.None));
        try {
            if (!visible)
                return;
            using var motion = new UiMotion.VisualScope(ImGui.GetWindowDrawList(), origin, Opacity, slide);
            ImGui.BeginDisabled(!IsOpen);
            try {
                DrawContent(s);
            }
            finally { ImGui.EndDisabled(); }
        }
        finally { ImGui.EndChild(); ImGui.PopStyleVar(6); ImGui.PopStyleColor(3); }
    }

    private void DrawToggle(ImDrawListPtr parent, IMapWindowHost host, float alpha, float s) {
        using var fade = FrameScopes.PushStyle(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * alpha, true);
        var min = new Vector2(toggle.X, toggle.Y);
        var size = new Vector2(toggle.Z - toggle.X, toggle.W - toggle.Y);
        var enabled = Reason.Length == 0;
        MapWindowDrawing.DrawSurface(parent, min, min + size, GratisTheme.Radius.Card * s);
        ImGui.SetCursorScreenPos(min);
        if (!enabled)
            ImGui.BeginDisabled(true);
        if (ChromeControls.Button("##shared-fate-toggle", size))
            Toggle();
        ChromeControls.Feedback(parent, min, min + size, IsOpen, radius: GratisTheme.Radius.Card * s);
        GratisIcons.Draw(parent, min + size / 2, GratisIcons.Sparkles,
            IsOpen ? GratisTheme.WarmAccent : GratisTheme.Secondary, GratisTheme.IconSize * s);
        if (enabled)
            ChromeControls.Tooltip("Shared FATEs");
        else
            UnavailableDrawing.Control(Reason, s);
        if (!enabled)
            ImGui.EndDisabled();
    }

    // The game hands over a new snapshot about once a second; ordering three small lists then is the only work.
    private void Refresh() {
        var read = source!.Snapshot;
        if (ReferenceEquals(read, snapshot))
            return;
        snapshot = read;
        for (var index = 0; index < SharedFateList.TabCount; index++)
            ordered[index] = SharedFateList.Order(snapshot.Zones(index));
        // Until the player picks a tab, follow the one the game last showed (or the first with data).
        if (!userTab)
            Tab = SharedFateList.DefaultTab(snapshot);
    }

    private void DrawContent(float s) {
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var list = ImGui.GetWindowDrawList();
        Refresh();
        DrawHeader(list, start, width, s);
        var y = 38 * s;
        if (snapshot.Gemstones >= 0) {
            DrawGemstones(list, start + new Vector2(0, y), width, s);
            y += 36 * s;
        }
        if (snapshot.AnyLoaded) {
            DrawTabs(start + new Vector2(0, y), width, s);
            y += (TabFaces.QuestLog.Height + 8) * s;
        }
        ImGui.SetCursorScreenPos(start + new Vector2(0, y));
        EmptyMessage = "";
        if (ImGui.GetContentRegionAvail().Y < 56 * s) {
            EmptyState.Draw("Enlarge the map to read Shared FATEs.", EmptyFaces.QuestHelper);
            return;
        }
        var remaining = ImGui.GetContentRegionAvail();
        remaining.X = MathF.Max(1, remaining.X - 4 * s);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        UiMotion.Child("##shared-fate-cards", remaining, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollWithMouse
            | (!IsOpen ? ImGuiWindowFlags.NoInputs : ImGuiWindowFlags.None));
        try {
            using var motion = new UiMotion.VisualScope(ImGui.GetWindowDrawList(), ImGui.GetWindowPos(), Opacity,
                UiMotion.Full ? new Vector2((1 - Opacity) * 18 * s, 0) : Vector2.Zero);
            var min = ImGui.GetWindowPos();
            var max = min + ImGui.GetWindowSize();
            ListBounds = new(min.X, min.Y, max.X, max.Y);
            UiMotion.Scroll();
            var zones = Results;
            if (zones.Count == 0) {
                EmptyMessage = SharedFateList.EmptyText(snapshot);
                EmptyState.Draw(EmptyMessage, EmptyFaces.Quest);
                return;
            }
            var cardWidth = MathF.Max(1, ImGui.GetContentRegionAvail().X - 6 * s);
            for (var index = 0; index < zones.Count; index++)
                DrawCard(zones[index], index, cardWidth, s);
            using (MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push())
                ImGui.TextDisabled(SharedFateList.Caption);
        }
        finally { ImGui.EndChild(); ImGui.PopStyleVar(2); }
    }

    private void DrawHeader(ImDrawListPtr list, Vector2 start, float width, float s) {
        using (MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push())
            TextDrawing.Draw(list, start + new Vector2(2 * s, 7 * s), ImGui.GetColorU32(GratisTheme.Primary),
                "Shared FATEs");
        ImGui.SetCursorScreenPos(start + new Vector2(MathF.Max(0, width - 30 * s), 0));
        var close = ChromeControls.IconButton("##shared-fate-close", GratisIcons.X, "", new(30 * s));
        CloseBounds = ItemBounds();
        if (close) {
            Close();
            gesture = ImGui.IsAnyMouseDown();
        }
    }

    // Bicolor Gemstones, the currency Shared FATEs pay: the count against the cap, in the quest log's scenario-bar style.
    private void DrawGemstones(ImDrawListPtr list, Vector2 start, float width, float s) {
        var value = SharedFateList.GemText(snapshot.Gemstones, snapshot.GemstoneMax);
        using (MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push()) {
            TextDrawing.Draw(list, start + new Vector2(2 * s, 0), ImGui.GetColorU32(GratisTheme.Secondary), "Gemstones");
            var valueWidth = TextDrawing.Measure(value).X;
            TextDrawing.Draw(list, start + new Vector2(width - valueWidth - 2 * s, 0),
                ImGui.GetColorU32(GratisTheme.Primary), value);
        }
        var bar = start + new Vector2(0, 22 * s);
        var barSize = new Vector2(width, 4 * s);
        list.AddRectFilled(bar, bar + barSize, ImGui.GetColorU32(GratisTheme.WarmAccent with { W = .14f }),
            GratisTheme.Radius.Pill(barSize));
        var fill = barSize with { X = barSize.X * SharedFateList.GemFraction(snapshot.Gemstones, snapshot.GemstoneMax) };
        if (fill.X > 0)
            list.AddRectFilled(bar, bar + fill, ImGui.GetColorU32(GratisTheme.WarmAccent), GratisTheme.Radius.Pill(fill));
        GemBounds = new(start.X, start.Y, start.X + width, bar.Y + barSize.Y);
    }

    private void DrawTabs(Vector2 start, float width, float s) {
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push();
        var gap = TabFaces.QuestLog.Gap * s;
        var size = new Vector2(MathF.Max(1, (width - 2 * gap) / SharedFateList.TabCount), TabFaces.QuestLog.Height * s);
        for (var index = 0; index < SharedFateList.TabCount; index++) {
            ImGui.SetCursorScreenPos(start + new Vector2(index * (size.X + gap), 0));
            var tab = new TabStrip(tabIds[index], TabFaces.QuestLog, size, s);
            if (tab.Clicked) {
                Tab = index;
                userTab = true;
            }
            tabBounds[index] = ItemBounds();
            tab.Draw(SharedFateList.TabLabel(index), Tab == index);
        }
    }

    private void DrawCard(SharedFateZone zone, int index, float width, float s) {
        var start = ImGui.GetCursorScreenPos();
        var size = new Vector2(width, CardHeight * s);
        ImGui.PushID(index);
        // Read-only: the card is an item only so it can be hovered (for a cut-off name) and scrolled past.
        ImGui.Dummy(size);
        var b = ItemBounds();
        b.W = MathF.Min(b.W, ImGui.GetWindowPos().Y + ImGui.GetWindowHeight());
        if (b.W > b.Y)
            cards.Add((zone.TerritoryId, b));
        if (ImGui.IsItemVisible()) {
            var list = ImGui.GetWindowDrawList();
            list.AddRectFilled(start, start + size, ImGui.GetColorU32(new Vector4(1, 1, 1, .035f)),
                GratisTheme.Radius.Control * s);
            DrawCardText(list, zone, start, width, s);
        }
        ImGui.SetCursorScreenPos(start + new Vector2(0, size.Y));
        ImGui.Dummy(new(0, GratisTheme.Gap * s));
        ImGui.PopID();
    }

    private static void DrawCardText(ImDrawListPtr list, SharedFateZone zone, Vector2 start, float width, float s) {
        var context = MapDrawContext.Current;
        var pad = 12 * s;
        var max = SharedFateList.IsMaxRank(zone);
        var muted = Vector4.Lerp(GratisTheme.Secondary, GratisTheme.Primary, .22f);
        var chip = SharedFateList.Chip(zone);
        var chipWidth = 0f;
        // The rank chip sits right-aligned on the name row; max rank takes the gold chip.
        using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push()) {
            var profile = max ? MaxChip : PillFaces.PaletteRow;
            chipWidth = Pill.Measure(chip, profile, s).X;
            var at = start + new Vector2(width - pad + 2 * s - chipWidth, 9 * s);
            if (max)
                Pill.Draw(list, at, chip, profile, s);
            else
                Pill.Draw(list, at, chip, profile, s, muted);
        }
        var name = ChromeControls.FitLabel(zone.Name, MathF.Max(1, width - 2 * pad - chipWidth - 8 * s));
        TextDrawing.Draw(list, start + new Vector2(pad, 10 * s), ImGui.GetColorU32(GratisTheme.Primary), name);
        if (name != zone.Name)
            ChromeControls.Tooltip(zone.Name);
        var text = SharedFateList.ProgressText(zone);
        var textWidth = 0f;
        using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push()) {
            if (text.Length > 0) {
                var measured = TextDrawing.Measure(text);
                textWidth = measured.X;
                TextDrawing.Draw(list, start + new Vector2(width - pad - textWidth, 44 * s - measured.Y / 2),
                    ImGui.GetColorU32(muted), text);
            }
        }
        var barWidth = MathF.Max(1, width - 2 * pad - (textWidth > 0 ? textWidth + 10 * s : 0));
        DrawProgress(list, zone, start + new Vector2(pad, 41 * s), new Vector2(barWidth, 6 * s), s);
    }

    // Pips (one per needed FATE) when there are few, otherwise a plain bar; full gold at max rank.
    private static void DrawProgress(ImDrawListPtr list, SharedFateZone zone, Vector2 at, Vector2 size, float s) {
        var gold = GratisTheme.WarmAccent;
        var track = ImGui.GetColorU32(gold with { W = .16f });
        var fill = ImGui.GetColorU32(gold);
        var pips = SharedFateList.Pips(zone);
        if (pips == 0) {
            list.AddRectFilled(at, at + size, track, GratisTheme.Radius.Pill(size));
            var done = size with { X = size.X * SharedFateList.Fraction(zone) };
            if (done.X > 0)
                list.AddRectFilled(at, at + done, fill, GratisTheme.Radius.Pill(done));
            return;
        }
        var gap = 3 * s;
        var pip = new Vector2(MathF.Max(1, (size.X - gap * (pips - 1)) / pips), size.Y);
        var filled = SharedFateList.Filled(zone);
        for (var index = 0; index < pips; index++) {
            var min = at + new Vector2(index * (pip.X + gap), 0);
            list.AddRectFilled(min, min + pip, index < filled ? fill : track, GratisTheme.Radius.Pill(pip));
        }
    }

    private static Vector4 ItemBounds() {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        return new(min.X, min.Y, max.X, max.Y);
    }

    private static bool BeginPanel(Vector2 size, ImGuiWindowFlags flags) =>
#if GRATIS_PREVIEW
        ImGui.BeginChild("##shared-fate-panel", size, ImGuiChildFlags.AlwaysUseWindowPadding, flags);
#else
        ImGui.BeginChild("##shared-fate-panel", size, false, flags | ImGuiWindowFlags.AlwaysUseWindowPadding);
#endif
}
