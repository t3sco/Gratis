using System.Collections.Generic;
using Dalamud.Game.ClientState.Fates;
using Gratis.Classes;
using Gratis.Rendering;

namespace Gratis.MapRenderer;

public partial class MapRenderer {
    private readonly Dictionary<uint, FateDisplay> fateProgress = new(256);
    private long fateSecond = long.MinValue;
    private uint fateTerritory;

    public unsafe bool PrepareFateProgress(MarkerInfo marker) {
        var now = global::System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var territory = Service.ClientState.TerritoryType;
        if (now != fateSecond || territory != fateTerritory) {
            fateSecond = now;
            fateTerritory = territory;
            fateProgress.Clear();
            // IFateTable wrappers and formatted text are sampled once a second, not once per frame.
            for (var i = 0; i < Service.FateTable.Length; i++) {
                var fate = Service.FateTable[i];
                if (fate == null) continue;
                var state = fate.State switch {
                    FateState.Preparing => FateProgressState.Preparing,
                    FateState.Running => FateProgressState.Active,
                    FateState.Ended => FateProgressState.Complete,
                    FateState.Failed => FateProgressState.Failed,
                    FateState.Ending => FateProgressState.WaitingForEnd,
                    _ => FateProgressState.Unknown,
                };
                fateProgress[fate.FateId] = Rendering.FateProgress.Evaluate(new(state, fate.Progress,
                    (long)fate.StartTimeEpoch + fate.Duration - now, fate.Duration, fate.HasBonus));
            }
        }
        marker.FateProgress = null;
        if (FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap.Instance()->SelectedTerritoryId == territory
            && marker.DataId is { } id && fateProgress.TryGetValue(id, out var display)) {
            marker.FateProgress = display;
            marker.MotionRevision = (uint)(display.Fraction * 100) | (uint)display.State << 8 | (display.HasBonus ? 1u << 16 : 0);
        }
        return true;
    }
}
