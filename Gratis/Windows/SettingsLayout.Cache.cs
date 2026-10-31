using System.Collections.Generic;
using System.Linq;
using Gratis.Data;

namespace Gratis.Windows;

public sealed partial class SettingsLayout {
    private readonly Dictionary<string, SectionCatalogue> catalogue = [];

    private sealed class EntryGroup(string key, int tab, int sub) {
        public readonly string Key = key;
        public readonly int Tab = tab, Sub = sub;
        public readonly string HeaderId = "##group-" + key, PrimaryId = "primary-" + key, TableId = "rows-" + key;
        public readonly List<SettingsEntry> Visible = [], Rows = [];
        public readonly List<SettingsPreset> VisiblePresets = [];
        public readonly List<SettingsMultiSelect> VisibleMultis = [];

        public void PrepareRows(SystemConfig config, bool searching) {
            Rows.Clear();
            foreach (var entry in Visible) {
                if (!entry.Enabled(config) && !searching)
                    continue;
                Rows.Add(entry);
            }
        }
    }

    private sealed class SectionCatalogue {
        private readonly SettingsEntry[] entries;
        private readonly SettingsPreset[] presets;
        private readonly SettingsMultiSelect[] multiSelects;
        private readonly EntryGroup[] entryGroups, presetGroups, multiGroups, groups;
        private readonly bool[] matches, presetMatches, multiMatches;
        private string? search;
        public readonly List<EntryGroup> Visible = [];
        public int Count { get; private set; }

        public SectionCatalogue(string section) {
            entries = Entries.Where(e => e.Section == section).ToArray();
            presets = Presets.Where(p => p.Section == section).ToArray();
            multiSelects = MultiSelects.Where(m => m.Section == section).ToArray();
            var byName = new Dictionary<string, EntryGroup>();
            EntryGroup GroupOf(string name) {
                if (!byName.TryGetValue(name, out var group)) {
                    var home = Home(section, name);
                    byName.Add(name, group = new(name, home.Tab, home.Sub));
                }
                return group;
            }
            entryGroups = entries.Select(e => GroupOf(e.Group)).ToArray();
            presetGroups = presets.Select(p => GroupOf(p.Group)).ToArray();
            multiGroups = multiSelects.Select(m => GroupOf(m.Group)).ToArray();
            groups = byName.Values.OrderBy(g => GroupRank(section, g.Key)).ToArray();
            matches = new bool[entries.Length];
            presetMatches = new bool[presets.Length];
            multiMatches = new bool[multiSelects.Length];
        }

        public void Prepare(SystemConfig config, string query) {
            _ = config.FilterOptions;
            if (search != query) {
                search = query;
                for (var i = 0; i < entries.Length; i++)
                    matches[i] = entries[i].Matches(query);
                for (var i = 0; i < presets.Length; i++)
                    presetMatches[i] = presets[i].Matches(query);
                for (var i = 0; i < multiSelects.Length; i++)
                    multiMatches[i] = multiSelects[i].Matches(query);
            }
            var hideAdvanced = HidesAdvanced(config, query);
            Count = 0;
            Visible.Clear();
            foreach (var group in groups) {
                group.Visible.Clear();
                group.VisiblePresets.Clear();
                group.VisibleMultis.Clear();
            }
            for (var i = 0; i < entries.Length; i++) {
                if (!entries[i].Visible(config) || !matches[i] || hideAdvanced && entries[i].Advanced)
                    continue;
                entryGroups[i].Visible.Add(entries[i]);
                Count++;
            }
            // Presets and multi-selects are Gratis controls and sit above the raw rows of their group.
            for (var i = 0; i < presets.Length; i++)
                if (config.IsGratis && presetMatches[i])
                    presetGroups[i].VisiblePresets.Add(presets[i]);
            for (var i = 0; i < multiSelects.Length; i++)
                if (config.IsGratis && multiMatches[i])
                    multiGroups[i].VisibleMultis.Add(multiSelects[i]);
            foreach (var group in groups)
                if (group.Visible.Count > 0 || group.VisiblePresets.Count > 0 || group.VisibleMultis.Count > 0)
                    Visible.Add(group);
        }
    }

    private SectionCatalogue VisibleCatalogue(SystemConfig config, string section) {
        if (!catalogue.TryGetValue(section, out var result))
            catalogue.Add(section, result = new(section));
        result.Prepare(config, Search);
        return result;
    }
}
