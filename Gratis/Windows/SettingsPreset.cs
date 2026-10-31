using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Gratis.Data;

namespace Gratis.Windows;

/// <summary>One option of a preset: a label and the raw catalog setting each path is set to.</summary>
public sealed record SettingsPresetChoice(string Label, Dictionary<string, object> Values, string? Tip = null);

/// <summary>
/// A preset as data. Keep lists the settings the preset sets that stay visible without Show advanced; every other
/// setting it sets becomes an Advanced row.
/// </summary>
public sealed record SettingsPresetDefinition(string Id, string Section, string Group, string Label, string Tip,
    SettingsPresetChoice[] Choices, string[]? Keep = null);

/// <summary>
/// A named bundle of raw catalog settings. Nothing is stored: the control reads which choice's values all match the
/// current config (or none, which it calls Custom) and a click writes every value through SettingsEntry.Set, so
/// profiles, resets and Quick setup keep working on the raw settings.
/// </summary>
public sealed class SettingsPreset {
    public const float Tolerance = .0005f;
    public const int Custom = -1;
    private readonly (SettingsEntry Entry, object Value)[][] writes;
    private readonly Func<SystemConfig, bool>[] matchers;
    private readonly string[] searchable;

    public string Id { get; }
    public string Section { get; }
    public string Group { get; }
    public string Label { get; }
    public string Tooltip { get; }
    public string[] ChoiceLabels { get; }
    public string?[] ChoiceTips { get; }
    /// <summary>Every setting any choice sets, in catalog order of first mention.</summary>
    public SettingsEntry[] Covered { get; }

    private SettingsPreset(SettingsPresetDefinition definition, SettingsEntry[] covered,
        (SettingsEntry, object)[][] writes, Func<SystemConfig, bool>[] matchers) {
        Id = definition.Id;
        Section = definition.Section;
        Group = definition.Group;
        Label = definition.Label;
        Tooltip = definition.Tip;
        ChoiceLabels = definition.Choices.Select(c => c.Label).ToArray();
        ChoiceTips = definition.Choices.Select(c => c.Tip).ToArray();
        Covered = covered;
        this.writes = writes;
        this.matchers = matchers;
        searchable = [Label, .. ChoiceLabels, .. ChoiceTips.OfType<string>(), .. covered.Select(SettingsLayout.RowLabel)];
    }

    public int ChoiceCount => matchers.Length;

    /// <summary>The first choice whose values all match the config, or Custom. Allocation free.</summary>
    public int Current(SystemConfig config) {
        for (var i = 0; i < matchers.Length; i++)
            if (matchers[i](config))
                return i;
        return Custom;
    }

    /// <summary>How many choices match at once. Choices are built to be exclusive, so this is 1 or 0.</summary>
    public int Matching(SystemConfig config) {
        var count = 0;
        foreach (var matcher in matchers)
            count += matcher(config) ? 1 : 0;
        return count;
    }

    public void Apply(SystemConfig config, int choice) {
        foreach (var (entry, value) in writes[choice])
            entry.Set(config, value);
    }

    public bool Matches(string search) => string.IsNullOrWhiteSpace(search)
        || search.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(word =>
            searchable.Any(text => text.Contains(word, StringComparison.OrdinalIgnoreCase))
            || SettingsLayout.GroupLabel(Group).Contains(word, StringComparison.OrdinalIgnoreCase)
            || SettingsLayout.SectionLabel(Section).Contains(word, StringComparison.OrdinalIgnoreCase));

    /// <summary>Builds every preset; throws when one names a path that is not in the catalog or a value of the wrong type.</summary>
    internal static SettingsPreset[] CreateAll(IEnumerable<SettingsPresetDefinition> definitions,
        IReadOnlyCollection<SettingsEntry> entries) => definitions.Select(d => Create(d, entries)).ToArray();

    internal static SettingsPreset Create(SettingsPresetDefinition definition, IReadOnlyCollection<SettingsEntry> entries) {
        if (definition.Choices.Length == 0)
            throw new InvalidOperationException($"Preset {definition.Id} has no choices.");
        var covered = new List<SettingsEntry>();
        var writes = new (SettingsEntry, object)[definition.Choices.Length][];
        var matchers = new Func<SystemConfig, bool>[definition.Choices.Length];
        for (var i = 0; i < definition.Choices.Length; i++) {
            var choice = definition.Choices[i];
            if (choice.Values.Count == 0)
                throw new InvalidOperationException($"Preset {definition.Id} choice {choice.Label} sets nothing.");
            var parameter = Expression.Parameter(typeof(SystemConfig), "config");
            Expression? all = null;
            var choiceWrites = new List<(SettingsEntry, object)>();
            foreach (var (path, value) in choice.Values) {
                var entry = entries.FirstOrDefault(e => e.Path == path)
                    ?? throw new InvalidOperationException($"Preset {definition.Id} names an unknown setting: {path}");
                Expression member = parameter;
                foreach (var name in path.Split('.'))
                    member = Expression.Field(member, name);
                if (value.GetType() != member.Type)
                    throw new InvalidOperationException(
                        $"Preset {definition.Id} sets {path} to a {value.GetType().Name}, but it is a {member.Type.Name}.");
                Expression same = member.Type == typeof(float)
                    ? Expression.LessThan(Expression.Call(typeof(MathF).GetMethod(nameof(MathF.Abs), [typeof(float)])!,
                        Expression.Subtract(member, Expression.Constant(value))), Expression.Constant(Tolerance))
                    : Expression.Equal(member, Expression.Constant(value, member.Type));
                all = all is null ? same : Expression.AndAlso(all, same);
                choiceWrites.Add((entry, value));
                if (!covered.Contains(entry))
                    covered.Add(entry);
            }
            writes[i] = choiceWrites.ToArray();
            matchers[i] = Expression.Lambda<Func<SystemConfig, bool>>(all!, parameter).Compile();
        }
        foreach (var path in definition.Keep ?? [])
            if (!covered.Any(e => e.Path == path))
                throw new InvalidOperationException($"Preset {definition.Id} keeps a setting it never sets: {path}");
        return new(definition, covered.ToArray(), writes, matchers);
    }
}
