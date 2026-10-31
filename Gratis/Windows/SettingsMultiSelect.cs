using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Gratis.Data;

namespace Gratis.Windows;

/// <summary>One checkbox of a multi-select: the catalog bool it flips, the label it shows, and the short form used in the summary.</summary>
public sealed record SettingsMultiSelectItem(string Path, string Label, string? Summary = null);

/// <summary>A multi-select as data. Every bool it covers becomes an Advanced row.</summary>
public sealed record SettingsMultiSelectDefinition(string Id, string Section, string Group, string Label, string Tip,
    SettingsMultiSelectItem[] Items, string NoneLabel = "Never");

/// <summary>
/// A dropdown of checkboxes over raw catalog bools. Nothing is stored: each checkbox reads and writes its own setting
/// through SettingsEntry.Set, so profiles, resets and Quick setup keep working on the raw rows. The summary shown on
/// the closed dropdown is built once per combination, so drawing it allocates nothing.
/// </summary>
public sealed class SettingsMultiSelect {
    public const int MaxItems = 8;
    private readonly Func<SystemConfig, bool>[] readers;
    private readonly string?[] summaries;
    private readonly string[] shortLabels;
    private readonly string noneLabel;
    private readonly string[] searchable;

    public string Id { get; }
    public string Section { get; }
    public string Group { get; }
    public string Label { get; }
    public string Tooltip { get; }
    public string[] ItemLabels { get; }
    /// <summary>The raw rows, in item order.</summary>
    public SettingsEntry[] Covered { get; }

    private SettingsMultiSelect(SettingsMultiSelectDefinition definition, SettingsEntry[] covered,
        Func<SystemConfig, bool>[] readers) {
        Id = definition.Id;
        Section = definition.Section;
        Group = definition.Group;
        Label = definition.Label;
        Tooltip = definition.Tip;
        ItemLabels = definition.Items.Select(i => i.Label).ToArray();
        shortLabels = definition.Items.Select(i => i.Summary ?? i.Label).ToArray();
        noneLabel = definition.NoneLabel;
        Covered = covered;
        this.readers = readers;
        summaries = new string?[1 << covered.Length];
        searchable = [Label, .. ItemLabels, .. covered.Select(SettingsLayout.RowLabel)];
    }

    public int ItemCount => readers.Length;

    public bool IsOn(SystemConfig config, int item) => readers[item](config);

    /// <summary>Bit i is set while item i is on.</summary>
    public int Mask(SystemConfig config) {
        var mask = 0;
        for (var i = 0; i < readers.Length; i++)
            mask |= readers[i](config) ? 1 << i : 0;
        return mask;
    }

    /// <summary>Flips one item by writing its raw bool and nothing else.</summary>
    public void Set(SystemConfig config, int item, bool on) => Covered[item].Set(config, on);

    /// <summary>The closed dropdown's text for the current selection. Allocation free after the first call per combination.</summary>
    public string Summary(SystemConfig config) => Summary(Mask(config));

    public string Summary(int mask) => summaries[mask] ??= Build(mask);

    private string Build(int mask) {
        if (mask == 0)
            return noneLabel;
        var parts = new List<string>();
        for (var i = 0; i < shortLabels.Length; i++)
            if ((mask & 1 << i) != 0)
                parts.Add(parts.Count == 0 ? shortLabels[i] : char.ToLowerInvariant(shortLabels[i][0]) + shortLabels[i][1..]);
        return string.Join(", ", parts);
    }

    public bool Matches(string search) => string.IsNullOrWhiteSpace(search)
        || search.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(word =>
            searchable.Any(text => text.Contains(word, StringComparison.OrdinalIgnoreCase))
            || SettingsLayout.GroupLabel(Group).Contains(word, StringComparison.OrdinalIgnoreCase)
            || SettingsLayout.SectionLabel(Section).Contains(word, StringComparison.OrdinalIgnoreCase));

    /// <summary>Builds every multi-select; throws when one names a path that is not in the catalog or is not a bool.</summary>
    internal static SettingsMultiSelect[] CreateAll(IEnumerable<SettingsMultiSelectDefinition> definitions,
        IReadOnlyCollection<SettingsEntry> entries) => definitions.Select(d => Create(d, entries)).ToArray();

    internal static SettingsMultiSelect Create(SettingsMultiSelectDefinition definition,
        IReadOnlyCollection<SettingsEntry> entries) {
        if (definition.Items.Length is 0 or > MaxItems)
            throw new InvalidOperationException($"Multi-select {definition.Id} needs 1 to {MaxItems} items.");
        var covered = new SettingsEntry[definition.Items.Length];
        var readers = new Func<SystemConfig, bool>[definition.Items.Length];
        for (var i = 0; i < definition.Items.Length; i++) {
            var item = definition.Items[i];
            covered[i] = entries.FirstOrDefault(e => e.Path == item.Path)
                ?? throw new InvalidOperationException($"Multi-select {definition.Id} names an unknown setting: {item.Path}");
            if (definition.Items.Count(other => other.Path == item.Path) > 1)
                throw new InvalidOperationException($"Multi-select {definition.Id} lists {item.Path} twice.");
            var parameter = Expression.Parameter(typeof(SystemConfig), "config");
            Expression member = parameter;
            foreach (var name in item.Path.Split('.'))
                member = Expression.Field(member, name);
            if (member.Type != typeof(bool))
                throw new InvalidOperationException(
                    $"Multi-select {definition.Id} item {item.Path} is a {member.Type.Name}, not a bool.");
            readers[i] = Expression.Lambda<Func<SystemConfig, bool>>(member, parameter).Compile();
        }
        return new(definition, covered, readers);
    }
}
