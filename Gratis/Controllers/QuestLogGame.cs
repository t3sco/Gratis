using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Rendering;
using Lumina.Excel.Sheets;
using GameMap = FFXIVClientStructs.FFXIV.Client.Game.UI.Map;

namespace Gratis.Controllers;

public sealed partial class QuestLogGame : IQuestLogSource, IDisposable {
    private sealed record Metadata(Quest Row, string Name, ushort Level, QuestBucket Bucket, uint Category,
        uint Expansion, string ExpansionName, uint Crest, uint Icon, string Duty);
    private Dictionary<uint, Metadata>? metadata;
    private QuestLogSnapshot snapshot = QuestLogSnapshot.Loading;
    private long nextRead, nextProgress;
    private ulong character;
    private string acceptance = "";
    private uint? progressExpansion;
    private QuestLogProgress? progress;
    private int revision;
    private bool loggedFailure;
    public QuestLogSnapshot Snapshot => Volatile.Read(ref snapshot);

    public QuestLogGame() => Service.Framework.Update += Update;

    private void Publish(QuestLogStatus status, QuestLogQuest[]? quests = null, string scenario = "") =>
        Volatile.Write(ref snapshot, new(++revision, status, quests ?? [], scenario, progress, character) {
            Completed = status == QuestLogStatus.Ready ? completed : [],
            Unaccepted = status == QuestLogStatus.Ready ? unaccepted : [],
            HistoryLoading = historyIndex < historyRows.Length,
            HistoryCapped = completedIds.Count > 500, UnacceptedCapped = offersCapped,
        });

    private void Clear() {
        if (character == 0 && Snapshot.Status == QuestLogStatus.Loading)
            return;
        character = 0;
        acceptance = "";
        progress = null;
        progressExpansion = null;
        nextProgress = 0;
        ClearHistory();
        Publish(QuestLogStatus.Loading);
    }

    private unsafe void Update(IFramework framework) {
        if (!Service.ClientState.IsLoggedIn || Service.ClientState.IsPvP || !System.SystemConfig.IsGratis) {
            Clear();
            return;
        }
        var now = Environment.TickCount64;
        if (now < nextRead)
            return;
        nextRead = now + 1000;
        try {
            var state = PlayerState.Instance();
            var manager = QuestManager.Instance();
            var player = Service.ObjectTable.LocalPlayer;
            if (state == null || !state->IsLoaded || manager == null || player == null) {
                Clear();
                return;
            }
            var currentCharacter = player.GameObjectId;
            if (character != currentCharacter) {
                Clear();
                character = currentCharacter;
            }
            metadata ??= BuildMetadata();
            ReadHistory(now);
            var accepted = new Dictionary<uint, byte>();
            foreach (var work in manager->NormalQuests)
                if (work.QuestId != 0)
                    accepted[65536u + work.QuestId] = work.Sequence;
            var signature = string.Join(",", accepted.Keys.Order());
            if (acceptance != signature) {
                acceptance = signature;
                nextProgress = 0;
            }
            var targets = ReadTargets();
            var objectives = ReadObjectives(accepted);
            var quests = accepted.Keys.Select(id => {
                var data = metadata.GetValueOrDefault(id);
                var destinations = targets.GetValueOrDefault(id) ?? [];
                var details = data == null ? default : ReadJournal(data.Row, accepted[id],
                    string.IsNullOrWhiteSpace(objectives.GetValueOrDefault(id)));
                var quest = new QuestLogQuest(id, data?.Name ?? $"Quest {id}", data?.Level ?? 0,
                    data?.Bucket ?? QuestBucket.Other,
                    data?.Icon ?? 71023,
                    QuestLogText.PreferTracked(objectives.GetValueOrDefault(id), details.Objective),
                    data?.Duty ?? "", destinations) {
                    Expansion = data?.Expansion ?? 0, ExpansionIcon = data?.Crest ?? 0,
                    Description = details.Description ?? "", Rewards = details.Rewards,
                    Completed = data != null && Service.UnlockState.IsQuestCompleted(data.Row),
                    Repeatable = data?.Row.IsRepeatable ?? false, ClientKnown = true,
                };
                return data == null ? quest : WithRequirements(quest, data.Row);
            }).ToArray();
            ReadLists(accepted);
            var guide = ReadGuide();
            var scenario = quests.FirstOrDefault(q => q.RowId == guide && q.Bucket == QuestBucket.Scenario)
                ?? quests.FirstOrDefault(q => q.Bucket == QuestBucket.Scenario);
            var current = metadata.GetValueOrDefault(scenario?.RowId ?? guide);
            if (current?.Bucket != QuestBucket.Scenario) {
                progress = null;
                progressExpansion = null;
            }
            else if (nextProgress <= now || progressExpansion != current.Expansion) {
                var categories = QuestLogModel.ScenarioCategories(current.Expansion);
                try {
                    var rows = metadata.Values.Where(m => m.Bucket == QuestBucket.Scenario
                        && m.Expansion == current.Expansion && categories.Contains(m.Category)).ToArray();
                    var complete = rows.Count(m => Service.UnlockState.IsQuestCompleted(m.Row));
                    progress = rows.Length == 0 ? null : new(current.Expansion, current.ExpansionName, current.Crest,
                        complete, rows.Length);
                }
                catch (Exception error) {
                    progress = null;
                    Service.Log.Debug(error, "[QuestLog] Completion unavailable; keeping accepted quests");
                }
                progressExpansion = current.Expansion;
                nextProgress = now + 15000;
            }
            Publish(QuestLogStatus.Ready, quests, scenario?.Name ?? "");
            loggedFailure = false;
        }
        catch (Exception error) {
            progress = null;
            nextProgress = 0;
            Publish(QuestLogStatus.Unavailable);
            if (!loggedFailure)
                Service.Log.Warning(error, "[QuestLog] Snapshot unavailable; retrying on the framework thread");
            loggedFailure = true;
        }
    }

    private static Dictionary<uint, Metadata> BuildMetadata() => Service.DataManager.GetExcelSheet<Quest>()
        .ToDictionary(q => q.RowId, q => {
            var category = q.JournalGenre.ValueNullable?.JournalCategory.ValueNullable;
            var section = category?.JournalSection.ValueNullable;
            var expansion = q.Expansion.ValueNullable;
            var iconType = q.EventIconType.ValueNullable;
            var icon = QuestLogIcon.Accepted(iconType?.MapIconAvailable ?? 0,
                iconType?.IconRange ?? 0, section?.RowId);
            var duty = q.InstanceContentUnlock.RowId != 0
                ? q.InstanceContentUnlock.ValueNullable?.ContentFinderCondition.ValueNullable?.Name.ExtractText() ?? "Duty unlock"
                : "";
            return new Metadata(q, q.Name.ExtractText(), q.ClassJobLevel[0], QuestBuckets.FromSection(section?.RowId),
                category?.RowId ?? 0, q.Expansion.RowId, expansion?.Name.ExtractText() ?? "", expansion?.Icon ?? 0,
                icon, duty);
        });

    private static unsafe Dictionary<uint, PlaceEntry[]> ReadTargets(bool unaccepted = false) {
        var result = new Dictionary<uint, PlaceEntry[]>();
        var map = GameMap.Instance();
        if (map == null)
            return result;
        void Add(FFXIVClientStructs.FFXIV.Client.Game.UI.MarkerInfo quest) {
            if (quest.ObjectiveId == 0)
                return;
            var id = quest.ObjectiveId + 65536u;
            var targets = new List<PlaceEntry>();
            foreach (var marker in quest.MarkerData) {
                if (marker.MapId == 0 || !float.IsFinite(marker.Position.X) || !float.IsFinite(marker.Position.Z))
                    continue;
                targets.Add(new($"quest-log:{id}:{targets.Count}", PlaceKind.Quest, quest.Label.ToString(), "",
                    marker.MapId, marker.TerritoryTypeId, new Vector2(marker.Position.X, marker.Position.Z)) {
                    QuestRowId = id, Icon = marker.IconId,
                });
            }
            result[id] = targets.ToArray();
        }
        if (unaccepted) {
            foreach (var quest in map->UnacceptedQuestMarkers) Add(quest);
        }
        else {
            foreach (var quest in map->QuestMarkers) Add(quest);
        }
        return result;
    }

    private static unsafe Dictionary<uint, string> ReadObjectives(IReadOnlyDictionary<uint, byte> accepted) {
        var result = new Dictionary<uint, string>();
        var ui = UIState.Instance();
        if (ui == null)
            return result;
        ref var quests = ref ui->QuestTodoList.Todo.TrackedQuests;
        if (quests.LongCount < 0 || quests.LongCount > 64)
            return result;
        foreach (ref var quest in quests) {
            var index = quest.CurrentObjective;
            if (quest.QuestId == 0 || !accepted.TryGetValue(65536u + quest.QuestId, out var sequence)
                || sequence != quest.Sequence || index < 0 || index >= Math.Clamp(quest.ObjectivesCount, 0, 24))
                continue;
            ref var objective = ref quest.Objectives[index];
            var text = objective.Objective.ToString();
            var count = objective.ProgressText.ToString();
            if (!string.IsNullOrWhiteSpace(text))
                result[65536u + quest.QuestId] = text + (count.Length > 0 ? " · " + count : "");
        }
        return result;
    }

    private static unsafe uint ReadGuide() {
        var agent = AgentScenarioTree.Instance();
        if (agent == null || agent->Data == null)
            return 0;
        var data = agent->Data;
        var index = data->MSQPathIndex;
        var id = index < 3 ? data->MainScenarioQuestIds[index] : (ushort)0;
        if (id == 0)
            id = data->MainScenarioQuestIds[3];
        return id == 0 ? 0 : 65536u + id;
    }

    public unsafe bool Show(QuestLogQuest quest) {
        if (!System.SystemConfig.IsGratis || Service.ClientState.IsPvP)
            return false;
        var agent = AgentMap.Instance();
        if (agent == null)
            return false;
        var target = quest.Targets.FirstOrDefault(t => t.MapId == agent->SelectedMapId)
            ?? quest.Targets.FirstOrDefault();
        if (target?.World is not { } world)
            return false;
        System.ZoneSwitchController?.NotifyUserAction();
        if (agent->SelectedMapId != target.MapId)
            System.IntegrationsController.OpenMap(target.MapId);
        if (!System.MapRenderer.Frame.BeginQuestSpotlight(world, target.MapId,
            target.MapId == agent->CurrentMapId, target.Icon)) {
            System.SystemConfig.FollowPlayer = false;
            System.MapRenderer.CenterOnCoordinate(world);
        }
        System.MapWindow.ProcessingCommand = true;
        return true;
    }

    public void Dispose() => Service.Framework.Update -= Update;
}
