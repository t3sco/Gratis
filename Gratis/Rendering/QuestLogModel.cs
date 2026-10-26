using System;
using System.Collections.Generic;
using System.Linq;
using Gratis.Controllers;

namespace Gratis.Rendering;

public enum QuestLogStatus { Loading, Ready, Unavailable }

public sealed record QuestLogQuest(uint RowId, string Name, ushort Level, QuestBucket Bucket,
    uint Icon, string Objective, string Duty, IReadOnlyList<PlaceEntry> Targets);

public sealed record QuestLogProgress(uint Expansion, string Name, uint Icon, int Completed, int Total) {
    public int Percentage => Total > 0 ? (int)Math.Round(100d * Completed / Total) : 0;
}

public sealed record QuestLogSnapshot(int Revision, QuestLogStatus Status, IReadOnlyList<QuestLogQuest> Quests,
    string CurrentScenario = "", QuestLogProgress? Progress = null, ulong Character = 0) {
    public static readonly QuestLogSnapshot Loading = new(0, QuestLogStatus.Loading, []);
}

public interface IQuestLogSource {
    QuestLogSnapshot Snapshot { get; }
    bool Show(QuestLogQuest quest);
}

public static class QuestLogModel {
    public static QuestLogQuest[] Filter(QuestLogSnapshot snapshot, string query) {
        var words = PlaceSearch.Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return snapshot.Quests.Where(q => {
            var text = PlaceSearch.Normalize(q.Name + " " + q.Objective + " " + q.Duty + " "
                + QuestBuckets.Label(q.Bucket));
            return words.All(w => text.Contains(w, StringComparison.Ordinal));
        }).ToArray();
    }

    public static uint[] ScenarioCategories(uint expansion) => expansion switch {
        0 => [1, 2], 1 => [3, 4, 5], 2 => [6, 7], 3 => [8, 9, 10], 4 => [11, 12], 5 => [13, 14, 15], _ => [],
    };
}
