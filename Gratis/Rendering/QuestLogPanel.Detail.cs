using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class QuestLogPanel {
    private QuestLogQuest? detailQuest;
    private float detailOpacity;
    private bool detailKeyboard;
    public bool DetailOpen { get; private set; }
    public uint DetailQuestId => DetailOpen ? detailQuest?.RowId ?? 0 : 0;
    public Vector4 DetailBounds => !suspended && (DetailOpen || detailOpacity > .001f) ? Layout.Detail : Vector4.Zero;
    public Vector4 DetailMapBounds { get; private set; }
    public Vector4 DetailCloseBounds { get; private set; }

    private void ShowSearch() {
        if (Layout.DetailOverlay)
            CloseDetail();
        searchOpen = focusSearch = true;
    }

    private void HideSearch() {
        searchOpen = focusSearch = false;
        query = "";
    }

    private void CloseDetail() => DetailOpen = false;

    private void ShowOnMap(QuestLogQuest quest) {
        if (source?.Show(quest) != true)
            return;
        CloseDetail();
        if (!MapDrawContext.Current.Config.QuestLogPinned) {
            CloseLog();
        }
    }

    public static DrawTexture RowIcon(QuestLogQuest quest) {
        var context = MapDrawContext.Current;
        if (quest.Bucket == Controllers.QuestBucket.Scenario && quest.ExpansionIcon != 0) {
            var crest = context.GetExpansionCrest(quest.ExpansionIcon);
            if (crest.Handle != default)
                return crest;
        }
        return context.GetIcon(quest.Icon);
    }

    private void DrawDetail(IMapWindowHost host, float s, ImDrawListPtr parent) {
        detailKeyboard = false;
        DetailMapBounds = DetailCloseBounds = Vector4.Zero;
        detailOpacity = detailQuest == null ? 0
            : UiMotion.Visibility(ImGui.GetID("##quest-detail-visibility-" + motionEpoch), IsOpen && DetailOpen);
        if (detailQuest == null || detailOpacity <= .001f)
            return;
        var bounds = Layout.Detail;
        var origin = new Vector2(bounds.X, bounds.Y);
        var size = new Vector2(bounds.Z - bounds.X, bounds.W - bounds.Y);
        if (size.Y < 40 * s)
            return;
        var slide = UiMotion.Full ? new Vector2((1 - detailOpacity) * 18 * s, 0) : Vector2.Zero;
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push();
        using (var surface = new UiMotion.VisualScope(parent, origin, detailOpacity, slide))
            MapWindowDrawing.DrawSurface(parent, origin, origin + size, GratisTheme.Radius.Popup * s, surfaceOpacity: 1);
        ImGui.SetCursorScreenPos(origin);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Primary);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, GratisTheme.Secondary);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(16) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 6) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(12, 8) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, GratisTheme.Radius.Control * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 4 * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, GratisTheme.Radius.Pill(new Vector2(4 * s)));
        var visible = BeginDetail(size, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse | (!DetailOpen ? ImGuiWindowFlags.NoInputs : ImGuiWindowFlags.None));
        try {
            if (!visible)
                return;
            using var motion = new UiMotion.VisualScope(ImGui.GetWindowDrawList(), origin, detailOpacity, slide);
            ImGui.BeginDisabled(!DetailOpen);
            try {
                DrawDetailContent(detailQuest, s);
                detailKeyboard = ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows);
                if (Layout.DetailOverlay) {
                    keyboard = rowKeyboard || detailKeyboard;
                    if (DetailOpen && !searchOpen && keyboard && (ImGui.IsKeyPressed(ImGuiKey.M, false)
                        || ImGui.GetIO().KeyShift && ImGui.IsKeyPressed(ImGuiKey.Enter, false)))
                        ShowOnMap(detailQuest);
                }
                if (detailKeyboard || PointerOver) {
                    MapDrawContext.Current.Chrome.Reveal(host.Now);
                    ImGui.SetNextFrameWantCaptureKeyboard(true);
                }
            }
            finally { ImGui.EndDisabled(); }
        }
        finally { ImGui.EndChild(); ImGui.PopStyleVar(6); ImGui.PopStyleColor(3); }
    }

    private void DrawDetailContent(QuestLogQuest quest, float s) {
        var context = MapDrawContext.Current;
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var list = ImGui.GetWindowDrawList();
        using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push())
            TextDrawing.Draw(list, start + new Vector2(0, 8 * s), ImGui.GetColorU32(GratisTheme.WarmAccent), "QUEST SUMMARY");
        ImGui.SetCursorScreenPos(start + new Vector2(MathF.Max(0, width - 30 * s), 0));
        if (ChromeControls.IconButton("##quest-detail-close", GratisIcons.X,
            Layout.DetailOverlay ? "Back to Quest Log · Esc" : "Close summary · Esc", new(30 * s)))
            CloseDetail();
        DetailCloseBounds = ItemBounds();
        ImGui.SetCursorScreenPos(start + new Vector2(0, 42 * s));
        var remaining = ImGui.GetContentRegionAvail();
        if (remaining.Y < 64 * s) {
            ImGui.TextWrapped("Enlarge the map to read this quest.");
            return;
        }
        var footer = ImGui.GetFrameHeight() + 12 * s;
        UiMotion.Child("##quest-detail-body-" + quest.RowId, new(remaining.X, MathF.Max(1, remaining.Y - footer)),
            ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollWithMouse);
        try {
            using var motion = new UiMotion.VisualScope(ImGui.GetWindowDrawList(), ImGui.GetWindowPos(), detailOpacity,
                UiMotion.Full ? new Vector2((1 - detailOpacity) * 18 * s, 0) : Vector2.Zero);
            UiMotion.Scroll();
            using (context.GetChromeFont(20 * s, ChromeFontRole.Title)?.Push())
                ImGui.TextWrapped(quest.Name);
            var badge = ImGui.GetCursorScreenPos();
            PaletteRowDrawing.Pill(ImGui.GetWindowDrawList(), badge, $"Lv. {quest.Level}", GratisTheme.WarmAccent, s);
            ImGui.Dummy(new(0, 26 * s));
            if (quest.Repeatable) DetailChip("Repeatable", GratisTheme.Crystal, s);
            if (quest.Completed) DetailChip("Historically completed", GratisTheme.WarmAccent, s);
            if (quest.Start.Length > 0) {
                DetailHeading("Starts at", s);
                ImGui.TextWrapped(quest.Start);
            }
            DetailHeading("Requirements", s);
            ImGui.TextWrapped(quest.Requirements.Length > 0 ? quest.Requirements : $"Level {quest.Level}");
            if (quest.Prerequisites.Length > 0) {
                DetailHeading("Direct prerequisites / locks", s);
                ImGui.TextWrapped(quest.Prerequisites);
            }
            DrawRewards(quest.Rewards, s);
            if (quest.Description.Length > 0) {
                DetailHeading("Description", s);
                ImGui.TextWrapped(quest.Description);
            }
            if (quest.Objective.Length > 0) {
                DetailHeading("Objectives", s);
                ImGui.TextWrapped(quest.Objective);
            }
            if (quest.Duty.Length > 0) {
                DetailHeading("Duty", s);
                ImGui.TextColored(GratisTheme.Crystal, quest.Duty);
            }
        }
        finally { ImGui.EndChild(); }
        ImGui.BeginDisabled(quest.Targets.Count == 0);
        var buttonStart = ImGui.GetCursorScreenPos();
        var buttonSize = new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight());
        if (ChromeControls.Button("##quest-detail-map", buttonSize))
            ShowOnMap(quest);
        DetailMapBounds = ItemBounds();
        ChromeControls.Feedback(list, buttonStart, buttonStart + buttonSize, true);
        var label = quest.Targets.Count > 0 ? "Map" : "No map destination";
        var labelSize = TextDrawing.Measure(label);
        TextDrawing.Draw(list, buttonStart + (buttonSize - labelSize) / 2, ImGui.GetColorU32(GratisTheme.WarmAccent), label);
        ImGui.EndDisabled();
    }

    private static void DetailHeading(string title, float s) {
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push();
        ImGui.TextColored(GratisTheme.Secondary, title);
    }

    private static void DetailChip(string label, Vector4 ink, float s) {
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push();
        PaletteRowDrawing.Pill(ImGui.GetWindowDrawList(), ImGui.GetCursorScreenPos(), label, ink, s);
        ImGui.Dummy(new(TextDrawing.Measure(label).X + 12 * s, 22 * s));
    }

    private static void DrawRewards(QuestLogRewards? rewards, float s) {
        if (rewards == null || rewards.Gil == 0 && rewards.Exp is not > 0 && rewards.Items.Count == 0)
            return;
        DetailHeading("Base reward · before scaling and bonuses", s);
        var gil = $"{rewards.Gil:N0} gil";
        var exp = $"{rewards.Exp:N0} EXP";
        var width = ImGui.GetContentRegionAvail().X;
        if (rewards.Gil > 0)
            ImGui.TextColored(GratisTheme.WarmAccent, gil);
        if (rewards.Exp is > 0) {
            if (rewards.Gil > 0 && TextDrawing.Measure(gil).X + TextDrawing.Measure(exp).X + 20 * s <= width)
                ImGui.SameLine(0, 20 * s);
            ImGui.TextColored(GratisTheme.Crystal, exp);
        }
        DrawRewardItems(rewards, s);
    }

    private static void DrawRewardItems(QuestLogRewards rewards, float s) {
        if (rewards.Items.Count == 0)
            return;
        var width = ImGui.GetContentRegionAvail().X;
        var columns = Math.Max(1, (int)((width + 8 * s) / (72 * s)));
        var index = 0;
        foreach (var item in rewards.Items) {
            if (index % columns != 0)
                ImGui.SameLine(0, 8 * s);
            ImGui.PushID(index++);
            var cell = ImGui.GetCursorScreenPos();
            var min = cell + new Vector2(12 * s, 0);
            var size = new Vector2(40 * s);
            ChromeControls.Button("##quest-reward", new Vector2(64, 60) * s);
            var list = ImGui.GetWindowDrawList();
            list.AddRectFilled(min, min + size, ImGui.GetColorU32(GratisTheme.Crystal with { W = .08f }),
                GratisTheme.Radius.Control * s);
            var icon = MapDrawContext.Current.GetIcon(item.Icon);
            if (icon.Handle != default)
                list.AddImage(icon.Handle, min + new Vector2(3 * s), min + size - new Vector2(3 * s));
            using (MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push()) {
                var text = item.Count.ToString();
                var textSize = TextDrawing.Measure(text);
                TextDrawing.Draw(list, min + size - textSize - new Vector2(3 * s),
                    ImGui.GetColorU32(GratisTheme.Primary), text);
                if (item.Optional) {
                    const string choice = "Choose one";
                    var choiceWidth = TextDrawing.Measure(choice).X;
                    TextDrawing.Draw(list, cell + new Vector2((64 * s - choiceWidth) / 2, 44 * s),
                        ImGui.GetColorU32(GratisTheme.Secondary), choice);
                }
            }
            ChromeControls.Feedback(list, min, min + size, fill: false);
            ChromeControls.Tooltip($"{item.Name} × {item.Count}" + (item.Optional ? " · Choose one reward" : ""));
            ImGui.PopID();
        }
    }

    private static bool BeginDetail(Vector2 size, ImGuiWindowFlags flags) =>
#if GRATIS_PREVIEW
        ImGui.BeginChild("##quest-detail", size, ImGuiChildFlags.AlwaysUseWindowPadding, flags);
#else
        ImGui.BeginChild("##quest-detail", size, false, flags | ImGuiWindowFlags.AlwaysUseWindowPadding);
#endif
}
