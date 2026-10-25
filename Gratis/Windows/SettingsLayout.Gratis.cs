using System;
using System.Linq;
using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Windows;

public sealed partial class SettingsLayout
{
    private bool DrawGratis(SystemConfig config, Func<string, string, bool>? drawExtension, Action<string>? resetExtension)
    {
        config.MapColour ??= new MapColourSettings();
        config.Motion ??= new MotionSettings();
        config.Markers ??= new MarkerSettings();
        var scale = SettingsTheme.Scale;
        var changed = false;
        var list = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(32, 11) * scale);
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##settings-search", "Search settings...", ref Search, 128);
        ImGui.PopStyleVar();
        GratisIcons.Draw(list, start + new Vector2(16, 18) * scale, GratisIcons.Search, GratisTheme.Secondary, 14 * scale);
        if (Search.Length > 0) {
            var next = ImGui.GetCursorPos();
            ImGui.SetCursorScreenPos(start + new Vector2(width - 36 * scale, 0));
            if (ChromeControls.IconButton("##clear-search", GratisIcons.X, "Clear search", new Vector2(36) * scale)) Search = "";
            ImGui.SetCursorPos(next); ImGui.Dummy(Vector2.Zero);
        }
        ImGui.Spacing();
        var available = ImGui.GetContentRegionAvail();
        var compactNav = width < 640 * scale;
        var navWidth = (compactNav ? 44 : 168) * scale;
        var navStart = ImGui.GetCursorScreenPos();
#if GRATIS_PREVIEW
        TestRows.Clear(); TestNavigation = new(navStart.X, navStart.Y, navStart.X + navWidth, navStart.Y + available.Y);
#endif
        if (ImGui.BeginChild("settings-navigation", new Vector2(navWidth, available.Y))) {
            if (!compactNav) using (SettingsTheme.Font(12)) {
                ImGui.TextColored(GratisTheme.Secondary, "SETTINGS");
            }
            ImGui.Spacing();
            foreach (var index in NavigationOrder) {
                start = ImGui.GetCursorScreenPos();
                var size = new Vector2(navWidth - 4 * scale, 36 * scale);
                if (ImGui.Selectable($"##section-{index}", SelectedSection == index, ImGuiSelectableFlags.None, size)) {
                    SelectedSection = index; Search = "";
                }
                var selected = SelectedSection == index;
                var nav = ImGui.GetWindowDrawList();
                if (selected) {
                    nav.AddRectFilled(start, start + size, ImGui.GetColorU32(new Vector4(1, 1, 1, .04f)), 4 * scale);
                    nav.AddRectFilled(start + new Vector2(0, 8) * scale, start + new Vector2(2, 28) * scale,
                        ImGui.GetColorU32(GratisTheme.WarmAccent), scale);
                }
                GratisIcons.Draw(nav, start + new Vector2(18, 18) * scale, SectionIcons[index],
                    selected ? GratisTheme.WarmAccent : GratisTheme.Secondary, 16 * scale);
                if (compactNav) ChromeControls.Tooltip(SectionLabel(Sections[index]));
                else nav.AddText(start + new Vector2(34 * scale, (size.Y - ImGui.GetFontSize()) / 2),
                    ImGui.GetColorU32(selected ? GratisTheme.Primary : GratisTheme.Secondary), SectionLabel(Sections[index]));
            }
        }
        ImGui.EndChild();
        list.AddLine(navStart + new Vector2(navWidth, 0), navStart + new Vector2(navWidth, available.Y), ImGui.GetColorU32(GratisTheme.Border));
        ImGui.SameLine(0, 12 * scale);
#if GRATIS_PREVIEW
        var testContent = ImGui.GetCursorScreenPos(); TestContent = new(testContent.X, testContent.Y, testContent.X + ImGui.GetContentRegionAvail().X, testContent.Y + available.Y);
#endif
        if (ImGui.BeginChild("settings-content", new Vector2(0, available.Y))) {
            var sections = string.IsNullOrWhiteSpace(Search) ? new[] { Sections[SelectedSection] } : Sections;
            var found = false;
            foreach (var section in sections) {
                var entries = Find(config, section, Search);
                var extension = ExtensionMatches(config, section, Search);
                if (entries.Length == 0 && !extension) continue;
                found = true;
                ImGui.PushID(section);
                using (SettingsTheme.Font(14, ChromeFontRole.Title)) {
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted(SectionLabel(section));
                }
                ImGui.SameLine(MathF.Max(ImGui.GetCursorPosX(), ImGui.GetWindowWidth() - 40 * scale));
                if (ChromeControls.IconButton("##reset-section", GratisIcons.Repeat, "Reset section to defaults", new Vector2(36) * scale)) {
                    Reset(config, section); resetExtension?.Invoke(section); changed = true;
                }
                SettingsTheme.Helper(section switch {
                    "Appearance" => "Make the map feel at home.",
                    "Map" => "Find your way around Eorzea.",
                    "Markers" => "Choose what stands out on your map.",
                    "Filters" => "Choose the icons in your filter drawer.",
                    "Chrome & motion" => "Keep the controls close and the map clear.",
                    "Modules / tools" => "Your map's companion tools.",
                    _ => "Window behaviour and diagnostics.",
                });
                if (section == "Filters") {
                    if (ImGui.TreeNode("How filters work")) {
                        SettingsTheme.Helper("Click to mute. Ctrl/Alt-click to solo. Right-click for Loud / Quiet. " +
                            $"Hold {config.PeekKey} to peek. Quests, FATEs and other priority markers stay visible until their group is explicitly muted.");
                        ImGui.TreePop();
                    }
                    if (ImGui.Button("Clear temporary filters", new Vector2(0, 36 * scale))) MapDrawContext.Current.Visibility.ResetFilters();
                }
                ImGui.Spacing();
                if (section == "Markers" && MarkerOptionsMatch(Search) && drawExtension is not null)
                    changed |= drawExtension("Marker options", Search);
                foreach (var group in entries.GroupBy(entry => entry.Group)) {
                    var rows = group.Where(entry => entry.Enabled(config) || !string.IsNullOrWhiteSpace(Search)).ToArray();
                    if (rows.Length == 0) continue;
                    if (!string.IsNullOrWhiteSpace(Search)) ImGui.SetNextItemOpen(true, ImGuiCond.Always);
                    var primary = rows.Where(entry => !FineTune(entry)).ToArray();
                    var advanced = rows.Where(FineTune).ToArray();
                    bool open;
                    using (SettingsTheme.Font(12)) {
                        ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Secondary);
                        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0);
                        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 12) * scale);
                        var heading = ImGui.GetCursorScreenPos();
                        open = ImGui.CollapsingHeader("##group-" + group.Key, primary.Length > 0 && ExpandedByDefault(group.Key) ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
                        GratisIcons.Draw(ImGui.GetWindowDrawList(), heading + new Vector2(28, 18) * scale, GroupIcon(group.Key), GratisTheme.Secondary, 14 * scale);
                        ImGui.GetWindowDrawList().AddText(heading + new Vector2(44 * scale, (36 * scale - ImGui.GetFontSize()) / 2), ImGui.GetColorU32(GratisTheme.Secondary), GroupLabel(group.Key));
                        ImGui.PopStyleVar(2);
                        ImGui.PopStyleColor();
                    }
                    if (!open) continue;
                    var cardStart = ImGui.GetCursorScreenPos();
                    changed |= DrawEntryRows(config, primary, "primary-" + group.Key);
                    if (advanced.Length > 0) {
                        if (!string.IsNullOrWhiteSpace(Search)) ImGui.SetNextItemOpen(true, ImGuiCond.Always);
                        if (ImGui.TreeNode("Advanced##" + group.Key)) {
                            changed |= DrawEntryRows(config, advanced, "advanced-" + group.Key);
                            ImGui.TreePop();
                        }
                    }
                    var bottom = ImGui.GetCursorScreenPos().Y - ImGui.GetStyle().ItemSpacing.Y;
                    ImGui.GetWindowDrawList().AddRect(cardStart, new Vector2(cardStart.X + ImGui.GetContentRegionAvail().X, bottom),
                        ImGui.GetColorU32(GratisTheme.Border), GratisTheme.CardRadius * scale);
                    ImGui.Spacing();
                }
                if (section == "Map" && string.IsNullOrWhiteSpace(Search) && ImGui.Button("Marker declutter settings")) SelectedSection = 2;
                if (extension && drawExtension is not null) changed |= drawExtension(section, Search);
                ImGui.Spacing(); ImGui.PopID();
            }
            if (!found) SettingsTheme.Helper("No settings match. Try another name or change the visual style.");
        }
        ImGui.EndChild();
        return changed;
    }
}
