using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Gratis.Rendering;

namespace Gratis.Controllers;

public sealed partial class QuestLogGame {
    private sealed record AbandonCheck(uint Quest, uint[] Before, long Deadline);
    private AbandonCheck? abandonCheck;

    /// <summary>
    /// Abandons an accepted quest through the game's own Journal call, the one its Journal window's Abandon button
    /// makes. It runs only for a quest that is accepted right now, that the game allows to be abandoned, and never for
    /// a Main Scenario quest. The panel asks the player to confirm before it gets here.
    /// </summary>
    public unsafe bool Abandon(QuestLogQuest quest) {
        const uint offset = 65536;
        if (!quest.CanAbandon || quest.Bucket == QuestBucket.Scenario || quest.RowId < offset
            || quest.RowId - offset > ushort.MaxValue)
            return false;
        if (!Service.ClientState.IsLoggedIn || Service.ClientState.IsPvP || !System.SystemConfig.IsGratis)
            return false;
        var manager = QuestManager.Instance();
        var journal = Journal.Instance();
        var id = (ushort)(quest.RowId - offset);
        if (manager == null || journal == null || !manager->IsQuestAccepted(id))
            return false;
        var before = Snapshot.Quests.Select(q => q.RowId).ToArray();
        Service.Log.Information($"[QuestLog] Abandoning quest {id} ({quest.Name}) at the player's request");
        journal->AbandonQuest(Journal.QuestType.NormalQuest, id);
        abandonCheck = new(quest.RowId, before, Environment.TickCount64 + 4000);
        // Read the quest list again on the next frame so the abandoned quest leaves the log at once.
        nextRead = 0;
        return true;
    }

    // A few seconds after an abandon, confirms that exactly the chosen quest left the accepted list, and says so in
    // the log either way. The game's call is made once and never retried.
    private void CheckAbandon(long now, IReadOnlyDictionary<uint, byte> accepted) {
        if (abandonCheck is not { } check || now < check.Deadline)
            return;
        abandonCheck = null;
        var gone = check.Before.Where(id => !accepted.ContainsKey(id)).ToArray();
        if (gone.Length == 1 && gone[0] == check.Quest)
            Service.Log.Information($"[QuestLog] Abandon confirmed: quest {check.Quest - 65536u} left the accepted list");
        else
            Service.Log.Warning($"[QuestLog] Abandon of quest {check.Quest - 65536u} did not match what the game did; "
                + $"quests no longer accepted: [{string.Join(", ", gone.Select(id => id - 65536u))}]");
    }
}
