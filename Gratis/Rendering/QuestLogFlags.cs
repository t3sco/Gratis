using System.Collections.Generic;
using System.Linq;

namespace Gratis.Rendering;

// The game's own per-quest "priority" and "hidden" state, mirrored read-only: the game keeps both on the character's
// quest work and no safe call to change them exists (Gratis-notes/research/quest-priority.md), so the quest log shows
// what the Journal holds and leaves changing it to the Journal.
public static class QuestLogFlags {
    public const string PriorityLabel = "Priority";
    public const string HiddenLabel = "Hidden";
    public const string HiddenChip = "Hidden from map";

    // Only accepted quests carry the state; the Completed and Available tabs never show it.
    public static bool ShowPriority(QuestLogQuest quest, QuestLogTab tab) =>
        tab == QuestLogTab.Accepted && quest.Priority;

    public static bool ShowHidden(QuestLogQuest quest, QuestLogTab tab) =>
        tab == QuestLogTab.Accepted && quest.Hidden;

    // The priority quest leads its section, as it leads the game's Duty List; the guided Main Scenario quest follows.
    // The sort is stable, so every other quest keeps the order the game reported.
    public static QuestLogQuest[] Order(IEnumerable<QuestLogQuest> rows, uint scenarioId) =>
        rows.OrderByDescending(q => q.Priority).ThenByDescending(q => q.RowId == scenarioId).ToArray();

    // The player hid this quest from the game's map, so an empty objective line with no destination says so rather
    // than implying the game lost it.
    public static string ObjectiveFallback(QuestLogQuest quest) =>
        quest.Targets.Count > 0 ? "Objective unavailable" : quest.Hidden ? HiddenChip : "No map destination";
}
