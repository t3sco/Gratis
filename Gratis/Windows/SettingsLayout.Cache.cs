using System.Collections.Generic;
using System.Linq;
using Gratis.Data;

namespace Gratis.Windows;

public sealed partial class SettingsLayout {
    private readonly Dictionary<string, SectionCatalogue> catalogue = [];

    private sealed class EntryGroup(string key) {
        public readonly string Key = key;
        public readonly string HeaderId = "##group-" + key, PrimaryId = "primary-" + key,
            AdvancedId = "advanced-" + key, TreeId = "Advanced##" + key, TableId = "rows-" + key;
        public readonly List<SettingsEntry> Visible = [], Rows = [], Primary = [], Advanced = [];

        public void PrepareRows(SystemConfig config, bool searching) {
            Rows.Clear();
            Primary.Clear();
            Advanced.Clear();
            foreach (var entry in Visible) {
                if (!entry.Enabled(config) && !searching)
                    continue;
                Rows.Add(entry);
                (FineTune(entry) ? Advanced : Primary).Add(entry);
            }
        }
    }

    private sealed class SectionCatalogue {
        private readonly SettingsEntry[] entries;
        private readonly EntryGroup[] entryGroups, groups;
        private readonly bool[] matches;
        private string? search;
        public readonly List<EntryGroup> Visible = [];
        public int Count { get; private set; }

        public SectionCatalogue(string section) {
            entries = Entries.Where(e => e.Section == section).ToArray();
            var byName = new Dictionary<string, EntryGroup>();
            entryGroups = entries.Select(e => {
                if (!byName.TryGetValue(e.Group, out var group))
                    byName.Add(e.Group, group = new(e.Group));
                return group;
            }).ToArray();
            groups = byName.Values.ToArray();
            matches = new bool[entries.Length];
        }

        public void Prepare(SystemConfig config, string query) {
            _ = config.FilterOptions;
            if (search != query) {
                search = query;
                for (var i = 0; i < entries.Length; i++)
                    matches[i] = entries[i].Matches(query);
            }
            Count = 0;
            Visible.Clear();
            foreach (var group in groups)
                group.Visible.Clear();
            for (var i = 0; i < entries.Length; i++) {
                if (!entries[i].Visible(config) || !matches[i])
                    continue;
                var group = entryGroups[i];
                if (group.Visible.Count == 0)
                    Visible.Add(group);
                group.Visible.Add(entries[i]);
                Count++;
            }
        }
    }

    private SectionCatalogue VisibleCatalogue(SystemConfig config, string section) {
        if (!catalogue.TryGetValue(section, out var result))
            catalogue.Add(section, result = new(section));
        result.Prepare(config, Search);
        return result;
    }
}
