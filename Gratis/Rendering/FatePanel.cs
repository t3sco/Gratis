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
/// The FATE list: a toolbar-corner button beside the quest log's, opening a panel of the viewed zone's active FATEs.
/// Choosing a row spotlights that FATE on the map, the way the quest log's "Show on map" does.
/// </summary>
public sealed class FatePanel {
    private const float PanelWidth = 300;
    private const float MinimumWidth = 220;
    private static readonly PillProfile BonusChip = new() {
        Padding = new(14, 4), Inset = new(7, 2), Fill = GratisTheme.WarmAccent with { W = .20f },
        Ink = GratisTheme.WarmAccent,
    };

    private readonly List<(uint Id, Vector4 Bounds)> rows = [];
    private IFateListSource? source;
    private FateListEntry[] sorted = [];
    private int revision = -1;
    private bool swallowEscape, gesture, suspended, requestedOpen, initialized, questWasOpen;
    private int motionEpoch, escapeFrame = -1;
    private uint selected;
    private float minimumX;
    private QuestLogLayout corner;
    private bool cornerValid;
    private Vector4 toggle, panel;

    public bool IsOpen { get; private set; }
    public bool PointerOver { get; private set; }
    public bool InputActive { get; private set; }
    public float Opacity { get; private set; }
    public string Reason { get; private set; } = "";
    public Vector4 ToggleBounds => toggle;
    public Vector4 Bounds => suspended ? Vector4.Zero : panel;
    public Vector4 CloseBounds { get; private set; }
    public Vector4 ListBounds { get; private set; }
    public IReadOnlyList<(uint Id, Vector4 Bounds)> Rows => rows;
    public IReadOnlyList<FateListEntry> Results => sorted;
    public bool ReservesSpace => !suspended && (IsOpen || Opacity > .001f);
    public bool CapturesKeyboard => MapDrawContext.Current.Config.IsGratis && swallowEscape;

    /// <summary>Opens the panel (from the /fatelist command or the context menu). False when it cannot show now.</summary>
    public bool RequestOpen() {
        if (!MapDrawContext.Current.Config.IsGratis || Reason.Length > 0 && initialized)
            return false;
        requestedOpen = true;
        Open();
        return true;
    }

    public void Toggle() {
        if (IsOpen)
            Close();
        else if (Reason.Length == 0)
            Open();
    }

    private void Open() {
        var quest = MapDrawContext.Current.QuestLog;
        // The quest log and this panel share one corner: the one opened second replaces the other unless the log is pinned.
        if (quest.IsOpen && !MapDrawContext.Current.Config.QuestLogPinned)
            quest.Toggle();
        IsOpen = true;
        questWasOpen = quest.IsOpen;
    }

    public void Close() {
        IsOpen = requestedOpen = false;
        selected = 0;
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
        IsOpen = requestedOpen = swallowEscape = gesture = InputActive = PointerOver = false;
        Opacity = 0;
        selected = 0;
        revision = -1;
        sorted = [];
        rows.Clear();
        cornerValid = false;
        toggle = panel = CloseBounds = ListBounds = Vector4.Zero;
    }

    private static bool Over(Vector4 b) => b.Z > b.X && b.W > b.Y
        && ImGui.IsMouseHoveringRect(new(b.X, b.Y), new(b.Z, b.W), false);

    public void Prepare(IMapWindowHost host, Vector2 position, Vector2 size) {
        var context = MapDrawContext.Current;
        if (!initialized)
            OnOpen();
        if (!host.Config.IsGratis || context.FateSource == null) {
            OnClose();
            Reason = "";
            source = null;
            return;
        }
        source = context.FateSource;
        var pvp = context.IsPvP;
        suspended = host.Palette?.IsOpen == true;
        var (min, max) = ChromeLayout.RawSafeBounds(position, size);
        minimumX = min.X;
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
        var shift = ChromeLayout.QuestButtonsShown(context) ? 2 * (button + GratisTheme.Gap * s) : 0;
        var x = MathF.Max(min.X, corner.Toggle.X - shift);
        toggle = new(x, corner.Toggle.Y, x + button, corner.Toggle.W);
        panel = Place(corner, context, host.Config, s, out var room);
        Reason = FateList.Reason(pvp, source);
        if (Reason.Length == 0 && !room)
            Reason = FateList.NoRoomReason;
        if (Reason.Length > 0)
            Close();
        // The quest log was opened after this panel: it takes the corner.
        var questOpen = context.QuestLog.IsOpen;
        if (IsOpen && questOpen && !questWasOpen && !host.Config.QuestLogPinned)
            Close();
        questWasOpen = questOpen;
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
        rows.Clear();
        ListBounds = CloseBounds = Vector4.Zero;
        if (!host.Config.IsGratis || source == null || !cornerValid || suspended)
            return;
        var s = GratisTheme.ChromeScale;
        var parent = ImGui.GetWindowDrawList();
        var alpha = context.Chrome.ControlsAlpha(host.Config);
        if (alpha * ImGui.GetStyle().Alpha >= .05f)
            DrawToggle(parent, host, alpha, s);
        Opacity = UiMotion.Visibility(ImGui.GetID("##fate-panel-visibility-" + motionEpoch), IsOpen);
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
                DrawContent(host, s);
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
        if (ChromeControls.Button("##fate-toggle", size))
            Toggle();
        ChromeControls.Feedback(parent, min, min + size, IsOpen, radius: GratisTheme.Radius.Card * s);
        GratisIcons.Draw(parent, min + size / 2, GratisIcons.Swords,
            IsOpen ? GratisTheme.WarmAccent : GratisTheme.Secondary, GratisTheme.IconSize * s);
        if (enabled)
            ChromeControls.Tooltip("FATEs");
        else
            UnavailableDrawing.Control(Reason, s);
        if (!enabled)
            ImGui.EndDisabled();
    }

    private void DrawContent(IMapWindowHost host, float s) {
        var context = MapDrawContext.Current;
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var list = ImGui.GetWindowDrawList();
        // The list is sampled once a second by the source, and only while the panel is showing.
        var snapshot = source!.Snapshot;
        if (snapshot.Revision != revision || sorted.Length != snapshot.Fates.Count) {
            revision = snapshot.Revision;
            sorted = FateList.Sort(snapshot.Fates);
        }
        DrawHeader(list, start, width, s);
        ImGui.SetCursorScreenPos(start + new Vector2(0, 38 * s));
        if (ImGui.GetContentRegionAvail().Y < 56 * s) {
            EmptyState.Draw("Enlarge the map to read FATEs.", EmptyFaces.QuestHelper);
            return;
        }
        var remaining = ImGui.GetContentRegionAvail();
        remaining.X = MathF.Max(1, remaining.X - 4 * s);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        UiMotion.Child("##fate-rows", remaining, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollWithMouse
            | (!IsOpen ? ImGuiWindowFlags.NoInputs : ImGuiWindowFlags.None));
        try {
            using var motion = new UiMotion.VisualScope(ImGui.GetWindowDrawList(), ImGui.GetWindowPos(), Opacity,
                UiMotion.Full ? new Vector2((1 - Opacity) * 18 * s, 0) : Vector2.Zero);
            var min = ImGui.GetWindowPos();
            var max = min + ImGui.GetWindowSize();
            ListBounds = new(min.X, min.Y, max.X, max.Y);
            UiMotion.Scroll();
            if (sorted.Length == 0) {
                EmptyState.Draw(FateList.EmptyText, EmptyFaces.Quest);
                return;
            }
            var rowWidth = MathF.Max(1, ImGui.GetContentRegionAvail().X - 6 * s);
            var now = context.UnixTimeSeconds;
            foreach (var fate in sorted)
                DrawRow(fate, rowWidth, now, s);
        }
        finally { ImGui.EndChild(); ImGui.PopStyleVar(2); }
    }

    private void DrawHeader(ImDrawListPtr list, Vector2 start, float width, float s) {
        var context = MapDrawContext.Current;
        var title = "FATEs";
        float titleWidth;
        using (context.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push()) {
            titleWidth = TextDrawing.Measure(title).X;
            TextDrawing.Draw(list, start + new Vector2(2 * s, 7 * s), ImGui.GetColorU32(GratisTheme.Primary), title);
        }
        if (sorted.Length > 0)
            using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push())
                PaletteRowDrawing.Pill(list, start + new Vector2(titleWidth + 12 * s, 6 * s),
                    sorted.Length.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
                    GratisTheme.Secondary, s);
        ImGui.SetCursorScreenPos(start + new Vector2(MathF.Max(0, width - 30 * s), 0));
        var close = ChromeControls.IconButton("##fate-close", GratisIcons.X, "", new(30 * s));
        CloseBounds = ItemBounds();
        if (close) {
            Close();
            gesture = ImGui.IsAnyMouseDown();
        }
    }

    private static float RowHeight(float s) => 58 * s;

    private void DrawRow(FateListEntry fate, float width, long now, float s) {
        var context = MapDrawContext.Current;
        var start = ImGui.GetCursorScreenPos();
        var size = new Vector2(width, RowHeight(s));
        ImGui.PushID((int)fate.Id);
        var clicked = ChromeControls.Button("##fate", size);
        var hovered = ImGui.IsItemHovered();
        var b = ItemBounds();
        b.W = MathF.Min(b.W, ImGui.GetWindowPos().Y + ImGui.GetWindowHeight());
        if (b.W > b.Y)
            rows.Add((fate.Id, b));
        var focused = ImGui.IsItemFocused();
        if (clicked || focused && ImGui.IsKeyPressed(ImGuiKey.Enter, false)) {
            selected = fate.Id;
            if (source?.Locate(fate) == true)
                Close();
            else
                context.ContextMenu.Notify("That FATE could not be shown on this map.");
        }
        if (ImGui.IsItemVisible()) {
            var list = ImGui.GetWindowDrawList();
            PaletteRowDrawing.Background(list, start, size, context.Config.AccentColor, selected == fate.Id, hovered, s);
            ChromeControls.Feedback(list, start, start + size, selected == fate.Id, fill: false);
            var icon = context.GetIcon(fate.IconId);
            if (icon.Handle != default) {
                var iconSize = icon.Size * (24 * s / MathF.Max(1, MathF.Max(icon.Size.X, icon.Size.Y)));
                var iconMin = start + new Vector2(18, 22) * s - iconSize / 2;
                list.AddImage(icon.Handle, iconMin, iconMin + iconSize);
            }
            DrawText(list, fate, start, width, now, s, hovered);
        }
        ImGui.SetCursorScreenPos(start + new Vector2(0, size.Y));
        ImGui.Dummy(new(0, GratisTheme.Gap * s));
        ImGui.PopID();
    }

    private static void DrawText(ImDrawListPtr list, FateListEntry fate, Vector2 start, float width, long now, float s,
        bool hovered) {
        var context = MapDrawContext.Current;
        var textX = 38 * s;
        var textWidth = MathF.Max(1, width - textX - 8 * s);
        var muted = Vector4.Lerp(GratisTheme.Secondary, GratisTheme.Primary, .22f);
        // Chips sit right-aligned on the name row: Lv N, with the gold Bonus chip to its left.
        var chips = 0f;
        using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push()) {
            var level = "Lv " + fate.Level;
            var levelWidth = TextDrawing.Measure(level).X + 12 * s;
            chips = levelWidth + 8 * s;
            PaletteRowDrawing.Pill(list, start + new Vector2(width - levelWidth - 8 * s, 8 * s), level, muted, s);
            if (fate.Bonus) {
                var bonusWidth = Pill.Measure("Bonus", BonusChip, s).X;
                Pill.Draw(list, start + new Vector2(width - chips - bonusWidth - 4 * s, 8 * s), "Bonus", BonusChip, s);
                chips += bonusWidth + 4 * s;
            }
        }
        var name = ChromeControls.FitLabel(fate.Name, MathF.Max(1, textWidth - chips - 6 * s));
        TextDrawing.Draw(list, start + new Vector2(textX, 8 * s), ImGui.GetColorU32(GratisTheme.Primary), name);
        var accent = fate.Bonus ? GratisTheme.WarmAccent
            : fate.State == FateListState.Ending ? GratisTheme.Party : GratisTheme.Crystal;
        using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push()) {
            var trailing = FateList.Trailing(fate, now);
            var trailingWidth = trailing.Length > 0 ? TextDrawing.Measure(trailing).X : 0;
            var remaining = FateList.RemainingSeconds(fate, now);
            var urgent = fate.State == FateListState.Running && remaining is >= 0 and <= 60;
            var state = ChromeControls.FitLabel(FateList.StateText(fate),
                MathF.Max(1, textWidth - (trailingWidth > 0 ? trailingWidth + 10 * s : 0)));
            TextDrawing.Draw(list, start + new Vector2(textX, 28 * s), ImGui.GetColorU32(
                fate.State == FateListState.Running ? accent : muted), state);
            if (trailingWidth > 0)
                TextDrawing.Draw(list, start + new Vector2(width - 8 * s - trailingWidth, 28 * s),
                    ImGui.GetColorU32(urgent ? GratisTheme.Objective : muted), trailing);
        }
        if (fate.State == FateListState.Preparing)
            return;
        var bar = start + new Vector2(textX, 47 * s);
        var barSize = new Vector2(textWidth, 4 * s);
        list.AddRectFilled(bar, bar + barSize, ImGui.GetColorU32(accent with { W = .14f }),
            GratisTheme.Radius.Pill(barSize));
        var fill = barSize with { X = barSize.X * FateList.Fraction(fate) };
        if (fill.X > 0)
            list.AddRectFilled(bar, bar + fill, ImGui.GetColorU32(accent with { W = fate.State == FateListState.Ending ? .5f : 1 }),
                GratisTheme.Radius.Pill(fill));
    }

    private static Vector4 ItemBounds() {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        return new(min.X, min.Y, max.X, max.Y);
    }

    private static bool BeginPanel(Vector2 size, ImGuiWindowFlags flags) =>
#if GRATIS_PREVIEW
        ImGui.BeginChild("##fate-panel", size, ImGuiChildFlags.AlwaysUseWindowPadding, flags);
#else
        ImGui.BeginChild("##fate-panel", size, false, flags | ImGuiWindowFlags.AlwaysUseWindowPadding);
#endif
}
