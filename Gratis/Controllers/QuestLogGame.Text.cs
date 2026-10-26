using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.Evaluator;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using Gratis.Rendering;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace Gratis.Controllers;

public sealed partial class QuestLogGame {
    private sealed record JournalText(Dictionary<string, ReadOnlySeString> Text, QuestLogRewards? Rewards);
    private readonly Dictionary<uint, JournalText> journal = [];

    private (string Objective, string Description, QuestLogRewards? Rewards) ReadJournal(Quest row, byte sequence,
        bool needObjective) {
        if (!journal.TryGetValue(row.RowId, out var data))
        {
            if (journal.Count >= 96) journal.Clear();
            journal[row.RowId] = data = LoadJournal(row);
        }
        try {
            var prefix = "TEXT_" + row.Id.ExtractText().ToUpperInvariant();
            var sequences = row.TodoParams.Select(p => p.ToDoCompleteSeq).ToArray();
            var objectives = needObjective ? QuestLogText.ActiveTodos(sequences, sequence)
                .Select(i => JournalObjective(row.RowId, (byte)i,
                    data.Text.GetValueOrDefault($"{prefix}_TODO_{i:D2}")))
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() : [];
            var index = QuestLogText.DescriptionIndex(sequences, sequence);
            var description = EvaluateJournal(data.Text.GetValueOrDefault($"{prefix}_SEQ_{index:D2}"));
            if (description.Length == 0 && index != 0)
                description = EvaluateJournal(data.Text.GetValueOrDefault(prefix + "_SEQ_00"));
            return (string.Join("\n", objectives), description, data.Rewards);
        }
        catch (Exception) {
            return ("", "", data.Rewards);
        }
    }

    private static JournalText LoadJournal(Quest row) {
        var text = new Dictionary<string, ReadOnlySeString>(StringComparer.Ordinal);
        QuestLogRewards? rewards = null;
        try {
            var name = QuestLogText.SheetName(row.Id.ExtractText());
            if (name.Length > 0)
                foreach (var entry in Service.DataManager.GetExcelSheet<RawRow>(name: name)) {
                    if (entry.Columns.Count < 2)
                        break;
                    var key = entry.ReadStringColumn(0).ExtractText();
                    if (key.Contains("_TODO_", StringComparison.Ordinal) || key.Contains("_SEQ_", StringComparison.Ordinal))
                        text[key] = entry.ReadStringColumn(1);
                }
        }
        catch (Exception error) {
            Service.Log.Debug(error, "[QuestLog] Journal text unavailable for {Quest}", row.RowId);
        }
        try {
            var items = new List<QuestLogRewardItem>();
            void Add(uint id, uint count, bool optional = false) {
                if (id != 0 && count != 0 && Service.DataManager.GetExcelSheet<Item>().GetRowOrDefault(id) is { } item)
                    items.Add(new(id, item.Name.ExtractText(), item.Icon, count, optional));
            }
            if (row.ItemRewardType is 1 or 3 or 5)
                for (var i = 0; i < row.Reward.Count; i++)
                    Add(row.Reward[i].RowId, row.ItemCountReward[i]);
            for (var i = 0; i < row.OptionalItemReward.Count; i++)
                Add(row.OptionalItemReward[i].RowId, row.OptionalItemCountReward[i], true);
            Add(row.CurrencyReward.RowId, row.CurrencyRewardCount);
            uint? exp = row.ExpFactor == 0 ? 0u : null;
            if (Service.DataManager.GetExcelSheet<ParamGrow>().GetRowOrDefault(row.ClassJobLevel[0]) is { } grow)
                exp = QuestLogText.BaseExperience(row.ExpFactor, grow.ScaledQuestXP, grow.QuestExpModifier);
            rewards = new(row.GilReward, exp, items.ToArray());
        }
        catch (Exception error) {
            Service.Log.Debug(error, "[QuestLog] Rewards unavailable for {Quest}", row.RowId);
        }
        return new(text, rewards);
    }

    private static string EvaluateJournal(ReadOnlySeString text) {
        try {
            return Service.SeStringEvaluator.Evaluate(text).ExtractText();
        }
        catch (Exception) {
            return "";
        }
    }

    private static unsafe string JournalObjective(uint id, byte index, ReadOnlySeString text) {
        try {
            if (text.Data.Span.IndexOf((byte)2) < 0)
                return text.ExtractText();
            var events = EventFramework.Instance();
            var player = Service.ObjectTable.LocalPlayer;
            var handler = events == null ? null : (QuestEventHandler*)events->GetEventHandlerById(id);
            if (handler == null || handler->QuestId != (ushort)id || player == null)
                return "";
            uint arg0 = 0, arg1 = 0, arg2 = 0;
            handler->GetTodoArgs((BattleChara*)player.Address, index, &arg0, &arg1, &arg2);
            return Service.SeStringEvaluator.Evaluate(text,
                [new SeStringParameter(arg0), new SeStringParameter(arg1), new SeStringParameter(arg2)]).ExtractText();
        }
        catch (Exception) {
            return "";
        }
    }
}
