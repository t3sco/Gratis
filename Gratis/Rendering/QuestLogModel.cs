using System;
using System.Collections.Generic;
using System.Linq;
using Gratis.Controllers;

namespace Gratis.Rendering;

public enum QuestLogStatus { Loading, Ready, Unavailable }
public enum QuestLogTab { Accepted, Completed, Unaccepted }

public sealed record QuestLogQuest(uint RowId, string Name, ushort Level, QuestBucket Bucket,
    uint Icon, string Objective, string Duty, IReadOnlyList<PlaceEntry> Targets) {
    public uint Expansion { get; init; }
    public uint ExpansionIcon { get; init; }
    public string Description { get; init; } = "";
    public QuestLogRewards? Rewards { get; init; }
    public bool Completed { get; init; }
    public bool ClientKnown { get; init; }
    public bool Repeatable { get; init; }
    public string ObjectiveProgress { get; init; } = "";
    public string DestinationNote { get; init; } = "";
    public string StartNpc { get; init; } = "";
    public string StartArea { get; init; } = "";
    public string Jobs { get; init; } = "";
    public string RequiredClass { get; init; } = "";
    public string Prerequisites { get; init; } = "";
    public bool MoreConditions { get; init; }
}

public sealed record QuestLogRewards(uint Gil, uint? Exp, IReadOnlyList<QuestLogRewardItem> Items);

public sealed record QuestLogRewardItem(uint Id, string Name, uint Icon, uint Count, bool Optional = false);

public sealed record QuestLogProgress(uint Expansion, string Name, uint Icon, int Completed, int Total) {
    public int Percentage => Total > 0 ? (int)Math.Round(100d * Completed / Total) : 0;
}

public sealed record QuestLogSnapshot(int Revision, QuestLogStatus Status, IReadOnlyList<QuestLogQuest> Quests,
    string CurrentScenario = "", QuestLogProgress? Progress = null, ulong Character = 0) {
    public static readonly QuestLogSnapshot Loading = new(0, QuestLogStatus.Loading, []);
    public IReadOnlyList<QuestLogQuest> Completed { get; init; } = [];
    public IReadOnlyList<QuestLogQuest> Unaccepted { get; init; } = [];
    public bool HistoryLoading { get; init; }
    public bool HistoryCapped { get; init; }
    public bool UnacceptedCapped { get; init; }
}

public interface IQuestLogSource {
    QuestLogSnapshot Snapshot { get; }
    IItemTooltipSource? Items => null;
    bool Show(QuestLogQuest quest);
    void RequestDetail(uint id) { }
}

public static class QuestLogModel {
    public static QuestLogQuest[] Filter(QuestLogSnapshot snapshot, string query,
        QuestLogTab tab = QuestLogTab.Accepted, bool showLocked = false) {
        var words = PlaceSearch.Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var quests = tab switch {
            QuestLogTab.Completed => snapshot.Completed,
            QuestLogTab.Unaccepted => snapshot.Unaccepted.Where(q => showLocked || q.ClientKnown),
            _ => snapshot.Quests,
        };
        return quests.Where(q => {
            var text = PlaceSearch.Normalize(q.Name + " " + q.Objective + " " + q.Duty + " "
                + QuestBuckets.Label(q.Bucket));
            return words.All(w => text.Contains(w, StringComparison.Ordinal));
        }).ToArray();
    }

    public static uint[] ScenarioCategories(uint expansion) => expansion switch {
        0 => [1, 2], 1 => [3, 4, 5], 2 => [6, 7], 3 => [8, 9, 10], 4 => [11, 12], 5 => [13, 14, 15], _ => [],
    };
}
