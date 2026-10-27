using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Gratis.Rendering;
using Lumina.Excel.Sheets;

namespace Gratis.Controllers;

public sealed partial class QuestLogGame {
    private Metadata[] historyRows = [];
    private readonly HashSet<uint> completedIds = [];
    private QuestLogQuest[] completed = [], unaccepted = [];
    private int historyIndex;
    private long nextHistory;
    private uint detailRequest;
    private bool offersCapped;
    private readonly Dictionary<uint, QuestLogQuest> details = [];

    public void RequestDetail(uint id) => Volatile.Write(ref detailRequest, id);

    private void ClearHistory() {
        completedIds.Clear();
        completed = unaccepted = [];
        historyIndex = 0;
        nextHistory = 0;
        detailRequest = 0;
        details.Clear();
        journal.Clear();
    }

    private void ReadHistory(long now) {
        if (historyRows.Length == 0)
            historyRows = metadata!.Values.Where(m => m.Name.Length > 0).OrderBy(m => m.Name,
                StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (historyIndex >= historyRows.Length) {
            if (now < nextHistory) return;
            historyIndex = 0;
        }
        var end = Math.Min(historyRows.Length, historyIndex + 512);
        for (; historyIndex < end; historyIndex++) {
            var data = historyRows[historyIndex];
            if (Service.UnlockState.IsQuestCompleted(data.Row)) completedIds.Add(data.Row.RowId);
            else completedIds.Remove(data.Row.RowId);
        }
        if (historyIndex == historyRows.Length) nextHistory = now + 30000;
    }

    private QuestLogQuest Summary(Metadata data, bool known, PlaceEntry[]? targets = null) {
        var quest = new QuestLogQuest(data.Row.RowId, data.Name, data.Level, data.Bucket, data.Icon,
            "", data.Duty, targets ?? []) {
            Expansion = data.Expansion, ExpansionIcon = data.Crest, ClientKnown = known,
            Completed = completedIds.Contains(data.Row.RowId), Repeatable = data.Row.IsRepeatable,
        };
        var id = Volatile.Read(ref detailRequest);
        if (id == quest.RowId && !details.ContainsKey(id)) {
            if (details.Count >= 64) details.Clear();
            var text = ReadJournal(data.Row, quest.Completed ? (byte)255 : (byte)0, false);
            details[id] = quest with { Description = text.Description, Rewards = text.Rewards };
        }
        return details.TryGetValue(quest.RowId, out var detail)
            ? WithRequirements(detail with { ClientKnown = known, Completed = quest.Completed, Targets = quest.Targets }, data.Row) : quest;
    }

    private void ReadLists(IReadOnlyDictionary<uint, byte> accepted) {
        var offers = ReadTargets(true);
        completed = historyRows.Where(m => completedIds.Contains(m.Row.RowId)).Take(500)
            .Select(m => Summary(m, true)).ToArray();
        var candidates = historyRows.Where(m => !accepted.ContainsKey(m.Row.RowId)
            && (offers.ContainsKey(m.Row.RowId) || System.SystemConfig.QuestLogShowLocked
                && !completedIds.Contains(m.Row.RowId))).ToArray();
        offersCapped = candidates.Length > 500;
        unaccepted = candidates.Take(500).Select(m => Summary(m, offers.ContainsKey(m.Row.RowId),
            offers.GetValueOrDefault(m.Row.RowId))).ToArray();
    }

    private QuestLogQuest WithRequirements(QuestLogQuest quest, Quest row) {
        var area = row.PlaceName.ValueNullable?.Name.ExtractText() ?? "Area unavailable";
        var npc = row.IssuerStart.Is<ENpcResident>()
            ? row.IssuerStart.GetValueOrDefault<ENpcResident>()?.Singular.ExtractText() : null;
        if (npc == null && row.IssuerStart.Is<ENpcBase>())
            npc = Service.DataManager.GetExcelSheet<ENpcResident>().GetRowOrDefault(row.IssuerStart.RowId)?.Singular.ExtractText();
        var jobs = row.ClassJobCategory0.ValueNullable?.Name.ExtractText() ?? "Class requirement unavailable";
        if (row.ClassJobCategory1.RowId != 0)
            jobs += " / " + row.ClassJobCategory1.ValueNullable?.Name.ExtractText();
        var required = row.ClassJobRequired.ValueNullable?.Name.ExtractText() ?? "";
        var previous = row.PreviousQuest.Where(q => q.RowId != 0).Select(q => Prerequisite(q.RowId)).ToArray();
        return quest with {
            Start = (string.IsNullOrWhiteSpace(npc) ? "Start NPC unavailable" : npc) + " · " + area,
            Requirements = $"Level {quest.Level} · {jobs}" + (required.Length > 0 ? " · " + required : ""),
            Prerequisites = string.Join("\n", previous),
            MoreConditions = QuestLogText.MoreConditions(previous.Length, row.PreviousQuestJoin,
                row.QuestLock.Any(q => q.RowId != 0)),
        };
    }

    private string Prerequisite(uint id) {
        var known = System.SystemConfig.QuestLogShowLocked || completedIds.Contains(id)
            || Snapshot.Quests.Any(q => q.RowId == id) || unaccepted.Any(q => q.RowId == id && q.ClientKnown);
        return known && metadata!.TryGetValue(id, out var data) ? data.Name : "Undiscovered quest";
    }
}
