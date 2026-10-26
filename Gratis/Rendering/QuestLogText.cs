using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gratis.Rendering;

public static class QuestLogText {
    public const string NearbySubtitle = "Quests available nearby";
    public const string Available = "Available";
    public static string TabLabel(QuestLogTab tab) => tab == QuestLogTab.Unaccepted ? Available : tab.ToString();
    public static string EmptyTab(QuestLogTab tab) => $"No {TabLabel(tab).ToLowerInvariant()} quests.";

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

    public static uint BaseExperience(ushort factor, ushort scaledXp, byte modifier) =>
        (uint)((ulong)factor * scaledXp * modifier / 100);
}
