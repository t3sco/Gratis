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
        TestRows.Clear(); TestPresetChips.Clear(); TestPresetRows.Clear(); TestMultiRows.Clear(); TestMultiItems.Clear(); TestNavigation = new(navStart.X, navStart.Y, navStart.X + navWidth, navStart.Y + available.Y);
        TestSubTabs.Clear(); TestGroups.Clear(); TestShownTab = TestShownSubTab = -1; TestSearchResults = false;
#endif
        if (UiMotion.Child("settings-navigation", new Vector2(navWidth, available.Y),
            ImGuiWindowFlags.NoScrollWithMouse)) {
            UiMotion.Scroll();
            if (!compactNav)
                using (SettingsTheme.Font(12)) {
                    ImGui.TextColored(GratisTheme.Secondary, "SETTINGS");
                }
            ImGui.Spacing();
            for (var index = 0; index < Tabs.Length; index++) {
                if (index == ChangelogTab)
                    ImGui.Separator();
                var tab = new TabStrip(NavigationIds[index],
                    compactNav ? TabFaces.SettingsCompact : TabFaces.Settings, navWidth, scale, SelectedTab == index);
                if (tab.Clicked) {
                    SelectedTab = index;
                    Search = "";
                }
#if GRATIS_PREVIEW
                TestTabs[index] = new(tab.Min.X, tab.Min.Y, tab.Min.X + tab.Size.X, tab.Min.Y + tab.Size.Y);
#endif
                tab.Draw(Tabs[index], SelectedTab == index, TabIcons[index]);
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
        var contentTab = UiMotion.Page("##settings-page", SelectedTab, out var pageAlpha, out var pageInput);
        if (UiMotion.Child("settings-content", new Vector2(0, available.Y),
            ImGuiWindowFlags.NoScrollWithMouse | (pageInput ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoInputs))) {
            UiMotion.Scroll();
            using var pageMotion = new UiMotion.VisualScope(ImGui.GetWindowDrawList(), ImGui.GetWindowPos(), pageAlpha,
                UiMotion.Full ? new Vector2((1 - pageAlpha) * 6 * scale, 0) : Vector2.Zero);
            var searching = !string.IsNullOrWhiteSpace(Search);
            var tweaksPage = contentTab == TweaksTab;
            var changelogPage = contentTab == ChangelogTab && !searching;
            if (tweaksPage)
                changed |= DrawTweaks(config);
            if (SelectedTab == ChangelogTab && config.LastSeenTweakVersion != TweakNews.LatestVersion) {
                TweakNews.Dismiss(config);
                changed = true;
            }
            if (changelogPage)
                DrawChangelog();
            var found = true;
            if (tweaksPage || changelogPage) { }
            else if (searching)
                changed |= DrawSearchResults(config, drawExtension, resetExtension, scale, out found);
            else if (contentTab == EssentialsTab) {
#if GRATIS_PREVIEW
                TestShownTab = EssentialsTab;
#endif
                changed |= DrawEssentials(config, scale);
            }
            else
                changed |= DrawTabPage(config, contentTab, drawExtension, resetExtension, scale);
            if (!found)
                SettingsTheme.Helper("No settings match. Try another name or change the visual style.");
        }
        ImGui.EndChild();
        UiMotion.DrawTooltipTail();
        return changed;
    }

    /// <summary>
    /// A search ignores tabs and sub-tabs: every matching catalog row, grouped under its catalog section, so a row
    /// hidden by Show advanced or sitting on another tab is still found.
    /// </summary>
    private bool DrawSearchResults(SystemConfig config, Func<string, string, bool>? drawExtension,
        Action<string>? resetExtension, float scale, out bool found) {
        var changed = false;
        found = false;
#if GRATIS_PREVIEW
        TestSearchResults = true;
#endif
        foreach (var section in Sections) {
            var entries = VisibleCatalogue(config, section);
            var extension = ExtensionMatches(config, section, Search);
            var geometry = section == "Advanced" && GeometryMatches(Search);
            if (entries.Visible.Count == 0 && !extension && !geometry)
                continue;
            found = true;
            ImGui.PushID(section);
            changed |= DrawPageHeader(config, SectionLabel(section), SectionHelper(section), scale, false,
                "Reset section to defaults", out var reset);
            if (reset) {
                Reset(config, section);
                resetExtension?.Invoke(section);
                changed = true;
            }
            if (section == "Filters")
                DrawFiltersIntro(config, scale);
            ImGui.Spacing();
            if (section == "Markers" && MarkerOptionsMatch(Search) && drawExtension is not null)
                changed |= drawExtension("Marker options", Search);
            foreach (var group in entries.Visible)
                changed |= DrawGroup(config, group, true, scale);
            if (geometry)
                changed |= DrawWindowGeometry(config, true, scale);
            if (extension && drawExtension is not null)
                changed |= drawExtension(section, Search);
            ImGui.Spacing();
            ImGui.PopID();
        }
        return changed;
    }

    /// <summary>One tab's page: header, sub-tab strip, then the groups the table puts under the chosen sub-tab.</summary>
    private bool DrawTabPage(SystemConfig config, int tab, Func<string, string, bool>? drawExtension,
        Action<string>? resetExtension, float scale) {
        PrepareTab(config, tab);
        var changed = false;
        var hasSubTabs = SubTabs[tab].Length > 0;
        ImGui.PushID(tab);
        changed |= DrawPageHeader(config, Tabs[tab], TabHelpers[tab], scale, true,
            hasSubTabs ? "Reset this page to defaults" : "Reset section to defaults", out var reset);
        var sub = EffectiveSubTab(tab);
        if (hasSubTabs) {
            ImGui.Dummy(new Vector2(0, GratisTheme.Grid * scale));
            sub = DrawSubTabStrip(tab, sub, scale);
        }
        if (reset) {
            ResetPage(config, tab, hasSubTabs ? sub : -1, resetExtension);
            changed = true;
        }
#if GRATIS_PREVIEW
        TestShownTab = tab; TestShownSubTab = sub;
#endif
        ImGui.Spacing();
        if (shownSubTabCount == 0)
            SettingsTheme.Helper("Nothing here applies to the current visual style.");
        if (tab == MapTab && sub == Sub.Filters)
            DrawFiltersIntro(config, scale);
        if (tab == MapTab && sub == Sub.Markers && ExtensionMatches(config, "Markers", "") && drawExtension is not null)
            changed |= drawExtension("Marker options", "");
        foreach (var section in TabSections[tab]) {
            foreach (var group in catalogue[section].Visible)
                if (group.Tab == tab && group.Sub == sub)
                    changed |= DrawGroup(config, group, false, scale);
            if (tab == AdvancedTab && section == "Advanced")
                changed |= DrawWindowGeometry(config, false, scale);
        }
        if (tab == MapTab && sub == Sub.View && ChromeControls.TextButton("Marker declutter settings", scale: scale))
            SelectTab(MapTab, Sub.Markers);
        if (tab == MapTab && sub == Sub.QuestTools && ExtensionMatches(config, "Modules / tools", "")
            && drawExtension is not null)
            changed |= drawExtension("Modules / tools", "");
        ImGui.Spacing();
        ImGui.PopID();
        return changed;
    }

    /// <summary>The page title with its helper line; Show advanced and the page reset sit on the right.</summary>
    private bool DrawPageHeader(SystemConfig config, string title, string helper, float scale, bool advancedToggle,
        string resetTip, out bool reset) {
        var changed = false;
        using (SettingsTheme.Font(14, ChromeFontRole.Title)) {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(title);
        }
        var resetX = MathF.Max(ImGui.GetCursorPosX(), ImGui.GetWindowWidth() - 40 * scale);
        if (advancedToggle)
            changed |= DrawAdvancedToggle(config, resetX,
                ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + ImGui.GetScrollX(), scale);
        ImGui.SameLine(resetX);
        reset = ChromeControls.IconButton("##reset-section", GratisIcons.Repeat, resetTip, new Vector2(36) * scale, scale);
        SettingsTheme.Helper(helper);
        return changed;
    }

    /// <summary>Title and helper only: Essentials has no Show advanced switch and nothing to reset.</summary>
    private static void DrawPageTitle(string title, string helper) {
        using (SettingsTheme.Font(14, ChromeFontRole.Title)) {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(title);
        }
        SettingsTheme.Helper(helper);
    }

    private static string SectionHelper(string section) => section switch {
        "Appearance" => "Make the map feel at home.",
        "Map" => "Find your way around Eorzea.",
        "Markers" => "Choose what stands out on your map.",
        "Filters" => "Choose the icons in your filter drawer.",
        "Chrome & motion" => "Keep the controls close and the map clear.",
        "Modules / tools" => "Your map's companion tools.",
        _ => "Window behaviour and diagnostics.",
    };

    private static void DrawFiltersIntro(SystemConfig config, float scale) {
        if (ImGui.TreeNode("How filters work")) {
            SettingsTheme.Helper("Click to mute. Ctrl/Alt-click to solo. Right-click for Loud / Quiet. "
                + $"Hold {config.PeekKey} to peek. Quests, FATEs and other priority markers stay "
                + "visible until their group is explicitly muted.");
            ImGui.TreePop();
        }
        if (ChromeControls.TextButton("Clear temporary filters", new Vector2(0, 36 * scale), scale: scale))
            MapDrawContext.Current.Visibility.ResetFilters();
    }

    /// <summary>A catalog group as a collapsible card of rows. The journal group draws its own card.</summary>
    private bool DrawGroup(SystemConfig config, EntryGroup group, bool searching, float scale) {
        if (group.Key == "Journal replacement") {
            var journal = SettingsQuestLogJournal.Draw(config);
            ImGui.Spacing();
#if GRATIS_PREVIEW
            TestGroups.Add(group.Key);
#endif
            return journal;
        }
        group.PrepareRows(config, searching);
        var rows = group.Rows;
        if (rows.Count == 0 && group.VisiblePresets.Count == 0 && group.VisibleMultis.Count == 0)
            return false;
        if (searching)
            ImGui.SetNextItemOpen(true, ImGuiCond.Always);
        var open = SectionHeader.Settings(group.HeaderId, GroupLabel(group.Key), GroupIcon(group.Key),
            ExpandedByDefault(group.Key), scale);
#if GRATIS_PREVIEW
        TestGroups.Add(group.Key);
#endif
        if (!open)
            return false;
        var cardStart = ImGui.GetCursorScreenPos();
        var changed = DrawEntryRows(config, rows, group.PrimaryId, presets: group.VisiblePresets,
            multiSelects: group.VisibleMultis);
        var bottom = ImGui.GetCursorScreenPos().Y - ImGui.GetStyle().ItemSpacing.Y;
        ImGui.GetWindowDrawList().AddRect(cardStart,
            new Vector2(cardStart.X + ImGui.GetContentRegionAvail().X, bottom),
            ImGui.GetColorU32(GratisTheme.Border), GratisTheme.Radius.Card * scale);
        ImGui.Spacing();
        return changed;
    }

    /// <summary>
    /// Sub-tabs as a row of chips under the page title. Only sub-tabs with a row to show appear, and a tab left with
    /// one draws no strip. Chips wrap onto a second line when the window is narrow. Returns the sub-tab to show.
    /// </summary>
    private int DrawSubTabStrip(int tab, int selected, float scale) {
        if (shownSubTabCount < 2)
            return selected;
        var names = SubTabs[tab];
        using var font = SettingsTheme.Font(GratisTheme.TextSize, ChromeFontRole.Chip);
        var origin = ImGui.GetCursorScreenPos();
        var gap = GratisTheme.Grid * scale;
        var height = ChipFaces.Compact.Height * scale;
        var width = MathF.Max(1, ImGui.GetContentRegionAvail().X);
        float x = 0, y = 0;
        var result = selected;
        ImGui.PushID("subtabs");
        for (var i = 0; i < shownSubTabCount; i++) {
            var sub = shownSubTabs[i];
            var chip = MathF.Min(width, TextDrawing.Measure(names[sub]).X + ChipFaces.Compact.WidthPadding * scale);
            if (x > 0 && x + chip > width) {
                x = 0;
                y += height + gap;
            }
            ImGui.SetCursorScreenPos(origin + new Vector2(x, y));
            if (SegmentedChip.Draw(SubTabIds[sub], names[sub], sub == selected,
                new(ChipFaces.Compact, GratisTheme.WarmAccent, new Vector2(chip, height), scale,
                    LabelCache: subTabLabels[sub]))) {
                chosenSubTab[tab] = sub;
                result = sub;
            }
#if GRATIS_PREVIEW
            var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax();
            TestSubTabs.Add((sub, new(min.X, min.Y, max.X, max.Y)));
#endif
            x += chip + gap;
        }
        ImGui.PopID();
        ImGui.SetCursorScreenPos(origin + new Vector2(0, y + height));
        ImGui.Dummy(Vector2.Zero);
        return result;
    }

    private const string AdvancedLabel = "Show advanced";
    private const string AdvancedTip = "Show every fine-tuning setting. Search always finds them, switched on or not.";
#if GRATIS_PREVIEW
    public Vector4 TestAdvancedToggle;
#endif

    /// <summary>Right-aligned beside the reset button; the label drops out before it can touch the title.</summary>
    private bool DrawAdvancedToggle(SystemConfig config, float resetX, float titleEnd, float scale) {
        var full = 44 * scale + ImGui.CalcTextSize(AdvancedLabel).X;
        var labelled = resetX - 8 * scale - full >= titleEnd + 12 * scale;
        var width = labelled ? full : 36 * scale;
        var shown = config.ShowAdvancedSettings;
        ImGui.SameLine(resetX - 8 * scale - width);
        var changed = SettingsTheme.Toggle("##show-advanced", ref shown, labelled ? AdvancedLabel : null);
        ChromeControls.Tooltip(AdvancedTip, scale: scale);
#if GRATIS_PREVIEW
        var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax();
        TestAdvancedToggle = new(min.X, min.Y, max.X, max.Y);
#endif
        if (changed)
            config.ShowAdvancedSettings = shown;
        return changed;
    }
}
