using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gratis.Controllers;

namespace Gratis.Rendering;

internal sealed class QuestLogPresentation {
    internal sealed class Text(QuestLogQuest quest) {
        public readonly string Id = quest.RowId.ToString();
        public readonly string Level = $"Lv. {quest.Level}";
        public readonly string Requirements = quest.Requirements.Length > 0 ? quest.Requirements : $"Level {quest.Level}";
        public readonly string[] Start = quest.Start.Split(" · ", 2, StringSplitOptions.TrimEntries);
        public readonly string ChildId = "##quest-detail-body-" + quest.RowId;
        public readonly string Gil = $"{quest.Rewards?.Gil:N0} gil";
        public readonly string Exp = $"{quest.Rewards?.Exp:N0} EXP";
        public readonly string[] Counts = quest.Rewards?.Items.Select(i => i.Count.ToString()).ToArray() ?? [];
    }

    private QuestLogSnapshot? snapshot;
    private int revision;
    private CultureInfo? culture;
    private readonly Dictionary<uint, QuestLogQuest> quests = [];
    private readonly Dictionary<QuestLogQuest, Text> texts = new(ReferenceEqualityComparer.Instance);
    private bool indexed;
    private string? query;
    private QuestLogTab tab;
    private bool showLocked, repeatable;
    public uint ScenarioId { get; private set; }
    public QuestLogQuest[] Filtered { get; private set; } = [];
    public (QuestBucket Bucket, QuestLogQuest[] Quests)[] Groups { get; private set; } = [];

    public void Update(QuestLogSnapshot next) {
        var currentCulture = CultureInfo.CurrentCulture;
        if (ReferenceEquals(snapshot, next) && revision == next.Revision && ReferenceEquals(culture, currentCulture)
            && currentCulture.IsReadOnly && currentCulture.NumberFormat.IsReadOnly)
            return;
        snapshot = next;
        revision = next.Revision;
        culture = currentCulture;
        query = null;
        quests.Clear();
        texts.Clear();
        ScenarioId = 0;
        if (next.CurrentScenario.Length > 0)
            foreach (var quest in next.Quests)
                if (quest.Bucket == QuestBucket.Scenario && quest.Name == next.CurrentScenario) {
                    ScenarioId = quest.RowId;
                    break;
                }
        indexed = false;
    }

    private void Add(IReadOnlyList<QuestLogQuest> entries) {
        foreach (var quest in entries)
            quests.TryAdd(quest.RowId, quest);
    }

    public QuestLogQuest? Find(uint id) {
        if (!indexed) {
            Add(snapshot!.Quests);
            Add(snapshot.Completed);
            Add(snapshot.Unaccepted);
            indexed = true;
        }
        return quests.GetValueOrDefault(id);
    }

    public Text For(QuestLogQuest quest) {
        if (!texts.TryGetValue(quest, out var text))
            texts.Add(quest, text = new(quest));
        return text;
    }

    public void Filter(string search, QuestLogTab currentTab, bool locked, bool repeatableOnly) {
        if (query == search && tab == currentTab && showLocked == locked && repeatable == repeatableOnly)
            return;
        query = search;
        tab = currentTab;
        showLocked = locked;
        repeatable = repeatableOnly;
        Filtered = QuestLogModel.Filter(snapshot!, search, tab, locked, repeatableOnly);
        Groups = QuestBuckets.Order.Select(bucket => {
            var rows = Filtered.Where(q => q.Bucket == bucket).ToArray();
            if (bucket == QuestBucket.Scenario)
                rows = rows.OrderByDescending(q => q.RowId == ScenarioId).ToArray();
            return (bucket, rows);
        }).Where(group => group.rows.Length > 0).ToArray();
    }
}
