using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gratis.Rendering;

public static class QuestLogText {
    public const string NearbySubtitle = "Quests available nearby";
    public const string Available = "Available";
    public static string TabLabel(QuestLogTab tab) => tab == QuestLogTab.Unaccepted ? Available : tab.ToString();
    private static readonly string[] captions = [.. Enum.GetValues<QuestLogTab>()
        .Select(t => t == QuestLogTab.Accepted ? "In progress" : TabLabel(t) + " quests")];
    public static string Caption(QuestLogTab tab) => captions[(int)tab];
    public static string EmptyTab(QuestLogTab tab) => $"No {TabLabel(tab).ToLowerInvariant()} quests.";

    // Expansion access is not a quest prerequisite worth showing; only entries that exactly equal an expansion name go.
    public static string FilterPrerequisites(string prerequisites) {
        if (prerequisites.Length == 0)
            return prerequisites;
        var kept = prerequisites.Split('\n').Where(p => !string.IsNullOrWhiteSpace(p) && !IsExpansionName(p)).ToArray();
        return string.Join('\n', kept);
    }

    private static bool IsExpansionName(string entry) {
        var name = entry.Trim();
        foreach (var expansion in PaletteBrowser.ExpansionNames)
            if (string.Equals(name, expansion, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    public static bool MoreConditions(int previousCount, byte previousJoin, bool hasLocks) =>
        hasLocks || previousCount > 1 && previousJoin != 1;

    public static string SheetName(string id) {
        var suffix = id.LastIndexOf('_');
        return suffix < 0 || !uint.TryParse(id.AsSpan(suffix + 1), NumberStyles.None,
            CultureInfo.InvariantCulture, out var number) ? ""
            : $"quest/{(number / 100).ToString("D3", CultureInfo.InvariantCulture)}/{id}";
    }

    public static int[] ActiveTodos(IReadOnlyList<byte> completeSequences, byte sequence) {
        var next = completeSequences.Where(s => s != 0 && s >= sequence).Select(s => (int)s)
            .DefaultIfEmpty(-1).Min();
        return Enumerable.Range(0, completeSequences.Count).Where(i => completeSequences[i] == next).ToArray();
    }

    public static int DescriptionIndex(IReadOnlyList<byte> completeSequences, byte sequence) {
        var active = ActiveTodos(completeSequences, sequence);
        return active.Length == 0 ? 0
            : completeSequences.Where(s => s != 0 && s < completeSequences[active[0]]).Distinct().Count() + 1;
    }

    public static string PreferTracked(string? tracked, string? journal) =>
        !string.IsNullOrWhiteSpace(tracked) ? tracked : journal ?? "";

    // The game writes its em dash as box-drawing U+2500, which the plugin text fonts lack (ranges end at U+206F), so
    // it would draw as '?'. Other mappings cover glyphs that appear in quest text but not in the font atlas.
    public static string CleanJournalText(string text) {
        var needs = false;
        foreach (var c in text)
            if (c is '\r' or '\u00AD' or '\u2500' or '\u2501' or '\u2015' or '\u2605' or '\u2606' or '\u3000'
                || c is >= '\uE000' and <= '\uF8FF' and not '\uE0BE' and not '\uE0BF') {
                needs = true;
                break;
            }
        if (!needs)
            return text.Trim();
        var result = new global::System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
            switch (text[i]) {
                case '\r':
                    if (i + 1 >= text.Length || text[i + 1] != '\n')
                        result.Append('\n');
                    break;
                case '\u00AD': result.Append('-'); break;
                case '\u2500' or '\u2501' or '\u2015': result.Append('\u2014'); break;
                case '\u2605' or '\u2606': result.Append('*'); break;
                case '\u3000': result.Append(' '); break;
                case >= '\uE000' and <= '\uF8FF' and not '\uE0BE' and not '\uE0BF': break;
                default: result.Append(text[i]); break;
            }
        return result.ToString().Trim();
    }

    public static uint BaseExperience(ushort factor, ushort scaledXp, byte modifier) =>
        (uint)((ulong)factor * scaledXp * modifier / 100);
}
