using System;
using System.Collections.Generic;
using Gratis.Data;
using Gratis.Rendering;
#if GRATIS_PREVIEW
using System.Numerics;
#endif

namespace Gratis.Windows;

/// <summary>
/// The Gratis settings navigation: Essentials, Appearance, Map, Controls, Tweaks, Advanced, then Changelog. The
/// catalog's Section, Group and Path strings stay the data model (profiles, Quick setup and ProfileCodec read them);
/// this file only says which tab and sub-tab each catalog group is drawn under. Classic keeps the old Sections.
/// </summary>
public sealed partial class SettingsLayout {
    public const int EssentialsTab = 0, AppearanceTab = 1, MapTab = 2, ControlsTab = 3, TweaksTab = 4,
        AdvancedTab = 5, ChangelogTab = 6;
    public static readonly string[] Tabs = ["Essentials", "Appearance", "Map", "Controls", "Tweaks", "Advanced",
        "Changelog"];
    public static readonly string[][] SubTabs = [
        [],
        ["Look", "Frame & glass", "Map colour", "Classic"],
        ["View", "Places", "Markers", "Filters", "Quest log & tools"],
        ["Visibility", "Layout", "Cards", "Motion", "Window"],
        [], [], []];
    internal const int MaxSubTabs = 5;

    /// <summary>Sub-tab positions inside their tab. Tabs share the numbers, so read them with the tab beside them.</summary>
    internal static class Sub {
        public const int Look = 0, FrameGlass = 1, MapColour = 2, Classic = 3;
        public const int View = 0, Places = 1, Markers = 2, Filters = 3, QuestTools = 4;
        public const int Visibility = 0, Layout = 1, Cards = 2, Motion = 3, Window = 4;
    }

    /// <summary>Catalog sections each tab draws groups from, in the order their groups appear on the page.</summary>
    internal static readonly string[][] TabSections = [
        [],
        ["Appearance"],
        ["Map", "Markers", "Filters", "Modules / tools"],
        ["Chrome & motion", "Map", "Advanced"],
        [],
        ["Advanced", "Chrome & motion"],
        []];

    private static readonly char[] TabIcons = [GratisIcons.House, GratisIcons.Eye, GratisIcons.Compass,
        GratisIcons.Layers, GratisIcons.Wrench, GratisIcons.Settings, GratisIcons.Sparkles];
    internal static readonly string[] NavigationIds = [
        "##tab-essentials", "##tab-appearance", "##tab-map", "##tab-controls", "##tab-tweaks", "##tab-advanced",
        "##tab-changelog"];
    private static readonly string[] SubTabIds = ["##subtab-0", "##subtab-1", "##subtab-2", "##subtab-3", "##subtab-4"];
    private static readonly string[] TabHelpers = [
        "The few settings most people change.",
        "Make the map feel at home.",
        "Find your way around Eorzea.",
        "Keep the controls close and the map clear.",
        "", "Fine-tuning, window geometry and diagnostics.", ""];

    // Every catalog group, keyed "Section/Group". A group missing from here still lands on its section's home tab.
    private static readonly Dictionary<string, (int Tab, int Sub)> Homes = new(StringComparer.Ordinal) {
        ["Appearance/Profiles"] = (AppearanceTab, Sub.Look),
        ["Appearance/Quick setup"] = (AppearanceTab, Sub.Look),
        ["Appearance/Style"] = (AppearanceTab, Sub.Look),
        ["Appearance/Gratis style"] = (AppearanceTab, Sub.Look),
        ["Appearance/Frame effects"] = (AppearanceTab, Sub.FrameGlass),
        ["Appearance/Edge feather"] = (AppearanceTab, Sub.FrameGlass),
        ["Appearance/Conic ring"] = (AppearanceTab, Sub.FrameGlass),
        ["Appearance/Glass surfaces"] = (AppearanceTab, Sub.FrameGlass),
        ["Appearance/Map recolour"] = (AppearanceTab, Sub.MapColour),
        ["Appearance/Classic frame"] = (AppearanceTab, Sub.Classic),

        ["Map/Viewport"] = (MapTab, Sub.View),
        ["Map/Zoom"] = (MapTab, Sub.View),
        ["Map/On open"] = (MapTab, Sub.View),
        ["Map/Zone changes"] = (MapTab, Sub.View),
        ["Map/Map links"] = (MapTab, Sub.View),
        ["Map/Map labels and fog"] = (MapTab, Sub.View),
        ["Map/Travel"] = (MapTab, Sub.Places),
        ["Map/Place search"] = (MapTab, Sub.Places),
        ["Map/Spotlight"] = (MapTab, Sub.Places),
        ["Map/Measure"] = (MapTab, Sub.Places),
        ["Map/Edge pointers"] = (MapTab, Sub.Places),
        ["Map/Quest Log"] = (MapTab, Sub.QuestTools),
        ["Map/Journal replacement"] = (MapTab, Sub.QuestTools),
        ["Map/Window"] = (ControlsTab, Sub.Window),

        ["Markers/Pins and notes"] = (MapTab, Sub.Markers),
        ["Markers/Visibility and interaction"] = (MapTab, Sub.Markers),
        ["Markers/Game icon scale"] = (MapTab, Sub.Markers),
        ["Markers/Legacy zoom scale"] = (MapTab, Sub.Markers),
        ["Markers/Marker colours and size"] = (MapTab, Sub.Markers),
        ["Markers/Player"] = (MapTab, Sub.Markers),
        ["Markers/Gratis player"] = (MapTab, Sub.Markers),
        ["Markers/Classic cone"] = (MapTab, Sub.Markers),
        ["Markers/Radar and areas"] = (MapTab, Sub.Markers),

        ["Filters/Categories in the list"] = (MapTab, Sub.Filters),
        ["Filters/Lenses"] = (MapTab, Sub.Filters),

        ["Chrome & motion/Map opacity"] = (ControlsTab, Sub.Visibility),
        ["Chrome & motion/Chrome visibility"] = (ControlsTab, Sub.Visibility),
        ["Chrome & motion/Toolbar visibility"] = (ControlsTab, Sub.Visibility),
        ["Chrome & motion/Chrome layout"] = (ControlsTab, Sub.Layout),
        ["Chrome & motion/Gratis toolbar"] = (ControlsTab, Sub.Layout),
        ["Chrome & motion/Classic toolbar"] = (ControlsTab, Sub.Layout),
        ["Chrome & motion/Hover cards"] = (ControlsTab, Sub.Layout),
        ["Chrome & motion/Title"] = (ControlsTab, Sub.Cards),
        ["Chrome & motion/Gratis title"] = (ControlsTab, Sub.Cards),
        ["Chrome & motion/Classic title"] = (ControlsTab, Sub.Cards),
        ["Chrome & motion/Coordinates"] = (ControlsTab, Sub.Cards),
        ["Chrome & motion/Gratis coordinates"] = (ControlsTab, Sub.Cards),
        ["Chrome & motion/Classic coordinates"] = (ControlsTab, Sub.Cards),
        ["Chrome & motion/Motion"] = (ControlsTab, Sub.Motion),
        ["Chrome & motion/Arrival notices"] = (ControlsTab, Sub.Motion),
        ["Chrome & motion/Extras"] = (AdvancedTab, 0),

        // The audit puts the window rules (move, lock, hide) with the window presets rather than under Advanced.
        ["Advanced/Window behaviour"] = (ControlsTab, Sub.Window),
        ["Advanced/Native window"] = (AdvancedTab, 0),
        ["Advanced/Map backdrop"] = (AdvancedTab, 0),
        ["Advanced/Diagnostics"] = (AdvancedTab, 0),
    };

    /// <summary>Where a section's unlisted groups go: the first sub-tab of the tab the section mostly lives on.</summary>
    internal static (int Tab, int Sub) SectionHome(string section) => section switch {
        "Appearance" => (AppearanceTab, Sub.Look),
        "Map" => (MapTab, Sub.View),
        "Markers" => (MapTab, Sub.Markers),
        "Filters" => (MapTab, Sub.Filters),
        "Modules / tools" => (MapTab, Sub.QuestTools),
        "Chrome & motion" => (ControlsTab, Sub.Visibility),
        "Tweaks" => (TweaksTab, 0),
        "Changelog" => (ChangelogTab, 0),
        _ => (AdvancedTab, 0),
    };

    internal static (int Tab, int Sub) Home(string section, string group) =>
        Homes.TryGetValue(section + "/" + group, out var home) ? home : SectionHome(section);

    internal static bool HasHome(string section, string group) => Homes.ContainsKey(section + "/" + group);
    internal static IReadOnlyCollection<string> HomeKeys => Homes.Keys;

    public int SelectedTab;
    private readonly int[] chosenSubTab = new int[Tabs.Length];
    private readonly int[] shownSubTabs = new int[MaxSubTabs];
    private int shownSubTabCount;
    private readonly ChipLabelCache[] subTabLabels = CreateLabelCaches();
    private readonly Dictionary<string, ActionLabelCache> helperLabels = [];

    private static ChipLabelCache[] CreateLabelCaches() {
        var caches = new ChipLabelCache[MaxSubTabs];
        for (var i = 0; i < caches.Length; i++)
            caches[i] = new();
        return caches;
    }

    /// <summary>The sub-tab remembered for the selected tab, kept for the session.</summary>
    public int SelectedSubTab {
        get => chosenSubTab[SelectedTab];
        set => chosenSubTab[SelectedTab] = value;
    }

    public int SubTabOf(int tab) => chosenSubTab[tab];

    public void SelectTab(int tab, int sub = -1) {
        SelectedTab = Math.Clamp(tab, 0, Tabs.Length - 1);
        if (sub >= 0)
            chosenSubTab[SelectedTab] = Math.Min(sub, Math.Max(0, SubTabs[SelectedTab].Length - 1));
    }

    /// <summary>
    /// Opens a catalog section (Gratis: its home tab and sub-tab; Classic: its page) or a tab by name. A group
    /// picks the sub-tab inside a section that spans several. Search is cleared so the page shows.
    /// </summary>
    public bool OpenSection(string section, string? group = null) {
        var index = Array.IndexOf(Sections, section);
        var tab = Array.IndexOf(Tabs, section);
        if (index < 0 && tab < 0)
            return false;
        if (index >= 0) {
            SelectedSection = index;
            var home = group is null ? SectionHome(section) : Home(section, group);
            SelectTab(home.Tab, home.Sub);
        }
        else
            SelectTab(tab);
        Search = "";
        return true;
    }

    /// <summary>The sub-tab drawn for a tab: the remembered one, or the first one with anything to show.</summary>
    private int EffectiveSubTab(int tab) {
        var chosen = chosenSubTab[tab];
        for (var i = 0; i < shownSubTabCount; i++)
            if (shownSubTabs[i] == chosen)
                return chosen;
        return shownSubTabCount > 0 ? shownSubTabs[0] : 0;
    }

    /// <summary>
    /// Prepares every catalog section the tab reads and notes which sub-tabs have at least one row to show. A
    /// sub-tab whose rows are all hidden by the current style (or by their conditions) is left out of the strip.
    /// </summary>
    private void PrepareTab(SystemConfig config, int tab) {
        Span<bool> shown = stackalloc bool[MaxSubTabs];
        foreach (var section in TabSections[tab])
            foreach (var group in VisibleCatalogue(config, section).Visible) {
                if (group.Tab != tab)
                    continue;
                group.PrepareRows(config, false);
                if (group.Rows.Count > 0 || group.VisiblePresets.Count > 0 || group.VisibleMultis.Count > 0)
                    shown[group.Sub] = true;
            }
        if (tab == MapTab) {
            shown[Sub.Markers] |= ExtensionMatches(config, "Markers", "");
            shown[Sub.QuestTools] |= ExtensionMatches(config, "Modules / tools", "");
        }
        shownSubTabCount = 0;
        for (var i = 0; i < MaxSubTabs; i++)
            if (shown[i])
                shownSubTabs[shownSubTabCount++] = i;
    }

    /// <summary>Resets what the page shows: a sub-tab's groups, or the whole tab when it has no sub-tabs.</summary>
    public static void ResetPage(SystemConfig config, int tab, int sub, Action<string>? resetExtension = null) {
        _ = config.FilterOptions;
        var defaults = new SystemConfig();
        foreach (var entry in Entries) {
            if (!entry.Visible(config))
                continue;
            var home = Home(entry.Section, entry.Group);
            if (home.Tab == tab && (sub < 0 || home.Sub == sub))
                entry.Reset(config, defaults);
        }
        if (tab == MapTab && (sub < 0 || sub == Sub.View))
            config.PvpMapOnly = defaults.PvpMapOnly; // a policy with no row: reset still puts it back
        if (tab != MapTab || !config.IsGratis)
            return;
        if (sub == Sub.Filters) {
            config.ResetFilterBar();
            MapDrawContext.Current?.Visibility.ResetFilters();
        }
        if (sub == Sub.Markers) {
            config.ResetMarkers();
            config.MarkerStyle = 1;
            config.IconScale = defaults.IconScale;
            resetExtension?.Invoke("Markers");
        }
        if (sub == Sub.QuestTools)
            resetExtension?.Invoke("Modules / tools");
    }

#if GRATIS_PREVIEW
    public readonly Vector4[] TestTabs = new Vector4[Tabs.Length];
    public readonly List<(int Sub, Vector4 Bounds)> TestSubTabs = [];
    public readonly List<string> TestGroups = [];
    public int TestShownTab = -1, TestShownSubTab = -1;
    public bool TestSearchResults;
#endif
}
