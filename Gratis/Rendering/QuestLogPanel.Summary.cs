using System;
using System.Collections.Generic;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class QuestLogPanel {
    private readonly List<Vector4> rewardBounds = [];
    private readonly ItemTooltipDrawing itemTooltip = new();
    public IReadOnlyList<Vector4> RewardBounds => rewardBounds;
    public Vector4 RewardMenuBounds => itemTooltip.MenuBounds;

    private void DrawDetailContent(QuestLogQuest quest, float s) {
#if GRATIS_PREVIEW
        using var benchmark = BenchmarkProbe.Measure(BenchmarkSection.QuestSummary);
#endif
        rewardBounds.Clear();
        if (DetailOpen) itemTooltip.Prepare(quest.RowId, quest.Rewards, source?.Items);
        var text = presentation.For(quest);
        var context = MapDrawContext.Current;
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push())
            TextDrawing.Draw(ImGui.GetWindowDrawList(), start + new Vector2(0, 8 * s),
                ImGui.GetColorU32(GratisTheme.WarmAccent), "QUEST SUMMARY");
        ImGui.SetCursorScreenPos(start + new Vector2(MathF.Max(0, width - 30 * s), 0));
        if (ChromeControls.IconButton("##quest-detail-close", GratisIcons.X,
            Layout.DetailOverlay ? "Back to Quest Log · Esc" : "Close summary · Esc", new(30 * s)))
            CloseDetail();
        DetailCloseBounds = ItemBounds();
        ImGui.SetCursorScreenPos(start + new Vector2(0, 42 * s));
        var remaining = ImGui.GetContentRegionAvail();
        var footerHeight = 56 * s;
        if (remaining.Y < footerHeight + 24 * s) {
            ImGui.TextWrapped("Enlarge the map to read this quest.");
            return;
        }
        var footerY = start.Y + 42 * s + remaining.Y - footerHeight;
        UiMotion.Child(text.ChildId, new(remaining.X, remaining.Y - footerHeight - 8 * s),
            ImGuiWindowFlags.NoBackground);
        try {
            using var motion = new UiMotion.VisualScope(ImGui.GetWindowDrawList(), ImGui.GetWindowPos(), detailOpacity,
                UiMotion.Full ? new Vector2((1 - detailOpacity) * 18 * s, 0) : Vector2.Zero);
            UiMotion.Scroll();
            using (context.GetChromeFont(24 * s, ChromeFontRole.Title)?.Push())
                ImGui.TextWrapped(quest.Name);
            ImGui.Dummy(new(0, 4 * s));
            DetailChips(quest, s);
            DetailDivider(s);
            if (quest.Start.Length > 0) {
                DetailHeading("WHERE", s);
                var parts = text.Start;
                ImGui.TextWrapped(parts[0]);
                if (parts.Length > 1) {
                    ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Secondary);
                    ImGui.TextWrapped(parts[1]);
                    ImGui.PopStyleColor();
                }
            }
            DetailHeading("REQUIREMENTS", s);
            ImGui.TextWrapped(text.Requirements);
            if (quest.Prerequisites.Length > 0 || quest.MoreConditions) {
                DetailHeading("REQUIRES", s);
                if (quest.Prerequisites.Length > 0) ImGui.TextWrapped(quest.Prerequisites);
                if (quest.MoreConditions) {
                    ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Secondary);
                    ImGui.TextWrapped("More conditions may apply");
                    ImGui.PopStyleColor();
                }
            }
            DrawRewards(quest.Rewards, text, s);
            if (DetailOpen) itemTooltip.Finish(source?.Items, s);
            if (quest.Description.Length > 0) {
                DetailHeading("DESCRIPTION", s);
                ImGui.TextWrapped(quest.Description);
            }
            if (quest.Objective.Length > 0) {
                DetailHeading("OBJECTIVES", s);
                using var objectiveFont = context.GetChromeFont(16 * s, ChromeFontRole.Chip)?.Push();
                ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.WarmAccent);
                ImGui.TextWrapped(quest.Objective);
                ImGui.PopStyleColor();
            }
            if (quest.Duty.Length > 0) {
                DetailHeading("DUTY", s);
                ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Crystal);
                ImGui.TextWrapped(quest.Duty);
                ImGui.PopStyleColor();
            }
            ImGui.Dummy(new(0, 8 * s));
        }
        finally { ImGui.EndChild(); }
        ImGui.SetCursorScreenPos(new(start.X, footerY));
        DetailDivider(s);
        var label = quest.Targets.Count > 0 ? "Show on map" : "No map destination";
        var clicked = ActionButton.Draw("##quest-detail-map", label,
            new(ActionFaces.QuestMap, Enabled: quest.Targets.Count > 0, Size: new(width, GratisTheme.ControlHeight * s)));
        if (clicked || ImGui.IsItemClicked(ImGuiMouseButton.Right)) ShowOnMap(quest);
        DetailMapBounds = ItemBounds();
    }

    private static void DetailDivider(float s) {
        var p = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddLine(p, p + new Vector2(ImGui.GetContentRegionAvail().X, 0),
            ImGui.GetColorU32(GratisTheme.Border), s);
        ImGui.Dummy(new(0, 2 * s));
    }

    private static void DetailHeading(string title, float s) {
        ImGui.SetCursorScreenPos(ImGui.GetCursorScreenPos() + new Vector2(0, 8 * s));
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push();
        ImGui.TextColored(GratisTheme.Secondary, title);
    }

    private void DetailChips(QuestLogQuest quest, float s) {
        var x = ImGui.GetCursorScreenPos().X;
        var right = x + ImGui.GetContentRegionAvail().X;
        var first = true;
        void Chip(string label, Vector4 ink) {
            using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push();
            var width = TextDrawing.Measure(label).X + 12 * s;
            if (!first && ImGui.GetItemRectMax().X + 8 * s + width <= right) ImGui.SameLine(0, 8 * s);
            PaletteRowDrawing.Pill(ImGui.GetWindowDrawList(), ImGui.GetCursorScreenPos(), label, ink, s);
            ImGui.Dummy(new(width, 22 * s));
            first = false;
        }
        Chip(presentation.For(quest).Level, GratisTheme.WarmAccent);
        Chip(quest.Completed ? "Completed" : Tab == QuestLogTab.Unaccepted ? QuestLogText.Available : "Accepted",
            GratisTheme.Crystal);
        if (quest.Repeatable) Chip("Repeatable", GratisTheme.Secondary);
    }

    private void DrawRewards(QuestLogRewards? rewards, QuestLogPresentation.Text text, float s) {
        if (rewards == null || rewards.Gil == 0 && rewards.Exp is not > 0 && rewards.Items.Count == 0) return;
        DetailHeading("REWARDS", s);
        var metricRight = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        if (rewards.Gil > 0) RewardMetric(text.Gil, GratisTheme.WarmAccent, s);
        if (rewards.Exp is > 0) {
            if (rewards.Gil > 0) {
                var nextWidth = TextDrawing.Measure(text.Exp).X + 24 * s;
                if (ImGui.GetItemRectMax().X + nextWidth + 8 * s <= metricRight)
                    ImGui.SameLine(0, 8 * s);
            }
            RewardMetric(text.Exp, GratisTheme.Crystal, s);
        }
        using (MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push())
            ImGui.TextColored(GratisTheme.Secondary, "Base rewards · before scaling and bonuses");
        DrawRewardGroup(rewards.Items, text.Counts, false, s);
        DrawRewardGroup(rewards.Items, text.Counts, true, s);
    }

    private static void RewardMetric(string text, Vector4 ink, float s) {
        var p = ImGui.GetCursorScreenPos();
        var size = new Vector2(TextDrawing.Measure(text).X + 20 * s, 30 * s);
        ImGui.GetWindowDrawList().AddRectFilled(p, p + size, ImGui.GetColorU32(ink with { W = .08f }),
            GratisTheme.Radius.Control * s);
        TextDrawing.Draw(ImGui.GetWindowDrawList(), p + (size - TextDrawing.Measure(text)) / 2,
            ImGui.GetColorU32(ink), text);
        ImGui.Dummy(size);
    }

    private void DrawRewardGroup(IReadOnlyList<QuestLogRewardItem> items, string[] counts, bool optional, float s) {
        var count = 0;
        foreach (var item in items) if (item.Optional == optional) count++;
        if (count == 0) return;
        if (optional) {
            using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push();
            ImGui.TextColored(GratisTheme.Secondary, "Choose one");
        }
        var origin = ImGui.GetCursorScreenPos();
        var slot = 48 * s;
        var stride = slot + 8 * s;
        var columns = Math.Max(1, (int)((ImGui.GetContentRegionAvail().X + 8 * s) / stride));
        var index = 0;
        for (var i = 0; i < items.Count; i++) {
            var item = items[i];
            if (item.Optional != optional) continue;
            var p = origin + new Vector2(index % columns * stride, index / columns * stride);
            ImGui.SetCursorScreenPos(p);
            ImGui.PushID(i);
            ChromeControls.Button("##quest-reward", new(slot));
            rewardBounds.Add(ItemBounds());
            var list = ImGui.GetWindowDrawList();
            list.AddRectFilled(p, p + new Vector2(slot), ImGui.GetColorU32(GratisTheme.Crystal with { W = .06f }),
                GratisTheme.Radius.Control * s);
            list.AddRect(p, p + new Vector2(slot), ImGui.GetColorU32(GratisTheme.Border), GratisTheme.Radius.Control * s);
            var icon = MapDrawContext.Current.GetIcon(item.Icon);
            if (icon.Handle != default) list.AddImage(icon.Handle, p + new Vector2(4 * s), p + new Vector2(slot - 4 * s));
            using (MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push()) {
                var label = counts[i];
                var textSize = TextDrawing.Measure(label);
                var badgeSize = textSize + new Vector2(8, 4) * s;
                var badge = p + new Vector2(slot) - badgeSize;
                list.AddRectFilled(badge, p + new Vector2(slot), ImGui.GetColorU32(GratisTheme.Surface with { W = 1 }),
                    GratisTheme.Radius.Inner(GratisTheme.Radius.Control * s, 2 * s));
                TextDrawing.Draw(list, badge + new Vector2(4, 2) * s, ImGui.GetColorU32(GratisTheme.Primary), label);
            }
            ChromeControls.Feedback(list, p, p + new Vector2(slot), fill: false);
            itemTooltip.Draw(item, source?.Items, s);
            ImGui.PopID();
            index++;
        }
        ImGui.SetCursorScreenPos(origin + new Vector2(0, (index + columns - 1) / columns * stride - 8 * s));
        ImGui.Dummy(Vector2.Zero);
    }
}
