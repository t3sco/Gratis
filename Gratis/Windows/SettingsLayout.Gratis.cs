using System;
using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Windows;

public sealed partial class SettingsLayout {
    private bool DrawGratis(SystemConfig config, Func<string, string, bool>? drawExtension,
        Action<string>? resetExtension) {
        config.MapColour ??= new MapColourSettings();
        config.Motion ??= new MotionSettings();
        config.Markers ??= new MarkerSettings();
        var scale = SettingsTheme.Scale;
        var changed = false;
        var list = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        if (TextField.Draw("##settings-search", ref Search, new(FieldFaces.Settings, Scale: scale)).Cleared)
            Search = "";
        ImGui.Spacing();
        var available = ImGui.GetContentRegionAvail();
        var compactNav = width < 640 * scale;
        var navWidth = (compactNav ? 44 : 168) * scale;
        var navStart = ImGui.GetCursorScreenPos();
#if GRATIS_PREVIEW
        TestRows.Clear(); TestNavigation = new(navStart.X, navStart.Y, navStart.X + navWidth, navStart.Y + available.Y);
#endif
        if (UiMotion.Child("settings-navigation", new Vector2(navWidth, available.Y),
            ImGuiWindowFlags.NoScrollWithMouse)) {
            UiMotion.Scroll();
            if (!compactNav)
                using (SettingsTheme.Font(12)) {
                    ImGui.TextColored(GratisTheme.Secondary, "SETTINGS");
                }
            ImGui.Spacing();
            foreach (var index in NavigationOrder) {
                var tab = new TabStrip(NavigationIds[index],
                    compactNav ? TabFaces.SettingsCompact : TabFaces.Settings, navWidth, scale, SelectedSection == index);
                if (tab.Clicked) {
                    SelectedSection = index;
                    Search = "";
                }
                tab.Draw(SectionLabel(Sections[index]), SelectedSection == index, SectionIcons[index]);
            }
        }
        ImGui.EndChild();
        list.AddLine(navStart + new Vector2(navWidth, 0), navStart + new Vector2(navWidth, available.Y),
            ImGui.GetColorU32(GratisTheme.Border));
        ImGui.SameLine(0, 12 * scale);
#if GRATIS_PREVIEW
        var testContent = ImGui.GetCursorScreenPos(); TestContent = new(testContent.X, testContent.Y,
            testContent.X + ImGui.GetContentRegionAvail().X, testContent.Y + available.Y);
#endif
        var contentSection = UiMotion.Page("##settings-page", SelectedSection, out var pageAlpha, out var pageInput);
        if (UiMotion.Child("settings-content", new Vector2(0, available.Y),
            ImGuiWindowFlags.NoScrollWithMouse | (pageInput ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoInputs))) {
            UiMotion.Scroll();
            using var pageMotion = new UiMotion.VisualScope(ImGui.GetWindowDrawList(), ImGui.GetWindowPos(), pageAlpha,
                UiMotion.Full ? new Vector2((1 - pageAlpha) * 6 * scale, 0) : Vector2.Zero);
            var tweaksPage = Sections[contentSection] == "Tweaks";
            if (tweaksPage)
                changed |= DrawTweaks(config);
            var sections = tweaksPage ? Array.Empty<string>() : string.IsNullOrWhiteSpace(Search)
                ? SingleSections[contentSection] : Sections;
            var found = false;
            foreach (var section in sections) {
                var entries = VisibleCatalogue(config, section);
                var extension = ExtensionMatches(config, section, Search);
                if (entries.Count == 0 && !extension)
                    continue;
                found = true;
                ImGui.PushID(section);
                using (SettingsTheme.Font(14, ChromeFontRole.Title)) {
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted(SectionLabel(section));
                }
                ImGui.SameLine(MathF.Max(ImGui.GetCursorPosX(), ImGui.GetWindowWidth() - 40 * scale));
                if (ChromeControls.IconButton("##reset-section", GratisIcons.Repeat, "Reset section to defaults",
                    new Vector2(36) * scale, scale)) {
                    Reset(config, section);
                    resetExtension?.Invoke(section);
                    changed = true;
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
                        SettingsTheme.Helper("Click to mute. Ctrl/Alt-click to solo. Right-click for Loud / Quiet. "
                            + $"Hold {config.PeekKey} to peek. Quests, FATEs and other priority markers stay "
                            + "visible until their group is explicitly muted.");
                        ImGui.TreePop();
                    }
                    if (ChromeControls.TextButton("Clear temporary filters", new Vector2(0, 36 * scale), scale: scale))
                        MapDrawContext.Current.Visibility.ResetFilters();
                }
                ImGui.Spacing();
                if (section == "Markers" && MarkerOptionsMatch(Search) && drawExtension is not null)
                    changed |= drawExtension("Marker options", Search);
                foreach (var group in entries.Visible) {
                    if (group.Key == "Journal replacement") {
                        changed |= SettingsQuestLogJournal.Draw(config);
                        ImGui.Spacing();
                        continue;
                    }
                    group.PrepareRows(config, !string.IsNullOrWhiteSpace(Search));
                    var rows = group.Rows;
                    if (rows.Count == 0)
                        continue;
                    if (!string.IsNullOrWhiteSpace(Search))
                        ImGui.SetNextItemOpen(true, ImGuiCond.Always);
                    var primary = group.Primary;
                    var advanced = group.Advanced;
                    var open = SectionHeader.Settings(group.HeaderId, GroupLabel(group.Key), GroupIcon(group.Key),
                        primary.Count > 0 && ExpandedByDefault(group.Key), scale);
                    if (!open)
                        continue;
                    var cardStart = ImGui.GetCursorScreenPos();
                    changed |= DrawEntryRows(config, primary, group.PrimaryId);
                    if (advanced.Count > 0) {
                        if (!string.IsNullOrWhiteSpace(Search))
                            ImGui.SetNextItemOpen(true, ImGuiCond.Always);
                        if (ImGui.TreeNode(group.TreeId)) {
                            changed |= DrawEntryRows(config, advanced, group.AdvancedId);
                            ImGui.TreePop();
                        }
                    }
                    var bottom = ImGui.GetCursorScreenPos().Y - ImGui.GetStyle().ItemSpacing.Y;
                    ImGui.GetWindowDrawList().AddRect(cardStart,
                        new Vector2(cardStart.X + ImGui.GetContentRegionAvail().X, bottom),
                        ImGui.GetColorU32(GratisTheme.Border), GratisTheme.Radius.Card * scale);
                    ImGui.Spacing();
                }
                if (section == "Map" && string.IsNullOrWhiteSpace(Search)
                    && ChromeControls.TextButton("Marker declutter settings", scale: scale))
                    SelectedSection = 2;
                if (extension && drawExtension is not null)
                    changed |= drawExtension(section, Search);
                ImGui.Spacing();
                ImGui.PopID();
            }
            if (!found && !tweaksPage)
                SettingsTheme.Helper("No settings match. Try another name or change the visual style.");
        }
        ImGui.EndChild();
        UiMotion.DrawTooltipTail();
        return changed;
    }
}
