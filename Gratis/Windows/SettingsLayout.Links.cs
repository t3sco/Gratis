using System;
using Gratis.Data;

namespace Gratis.Windows;

public sealed partial class SettingsLayout {
    /// <summary>Where a setting lives, for a changelog link: tab, sub-tab and row (Classic: section and row).</summary>
    public static string? Breadcrumb(string path, bool gratis) {
        var entry = Array.Find(Entries, e => e.Path == path);
        if (entry is null)
            return null;
        if (!gratis)
            return $"{entry.Section} > {entry.Label}";
        var home = Home(entry.Section, entry.Group);
        var sub = SubTabs[home.Tab];
        return sub.Length > home.Sub ? $"{Tabs[home.Tab]} > {sub[home.Sub]} > {entry.Label}"
            : $"{Tabs[home.Tab]} > {entry.Label}";
    }

    /// <summary>Opens the page that holds a setting, the way a Tweaks "Configure" button does.</summary>
    public bool OpenSetting(string path, SystemConfig config) {
        var entry = Array.Find(Entries, e => e.Path == path);
        if (entry is null)
            return false;
        SelectedSection = Array.IndexOf(Sections, entry.Section);
        if (!config.IsGratis) {
            Search = entry.Label;
            return true;
        }
        var home = Home(entry.Section, entry.Group);
        SelectTab(home.Tab, home.Sub);
        Search = "";
        if (entry.Advanced)
            config.ShowAdvancedSettings = true;
        return true;
    }
}
