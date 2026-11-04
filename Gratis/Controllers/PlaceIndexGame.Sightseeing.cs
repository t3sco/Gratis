using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Rendering;
using Lumina.Excel.Sheets;

namespace Gratis.Controllers;

public sealed partial class PlaceIndexGame {
    private SightseeingRow[]? sightseeingRows;
    private unsafe IEnumerable<PlaceEntry> SightseeingEntries() {
        if (!System.SystemConfig.IsGratis || !System.SystemConfig.SightseeingLog
            || !Service.ClientState.IsLoggedIn || Service.ClientState.IsPvP)
            return [];
        sightseeingRows ??= Sightseeing.Load(() => Service.DataManager.GetExcelSheet<Adventure>(), a => {
            if (a.Level.RowId == 0 || a.Level.ValueNullable is not { } level
                || level.Map.RowId == 0 || level.Map.ValueNullable is not { } map
                || map.TerritoryType.RowId == 0 || !float.IsFinite(level.X) || !float.IsFinite(level.Z))
                return (SightseeingRow?)null;
            return new SightseeingRow(a.RowId, a.Name.ExtractText(), level.Map.RowId,
                map.TerritoryType.RowId, new Vector2(level.X, level.Z), a.MinTime, a.MaxTime,
                (uint)a.IconDiscovered);
        }, ex => Service.Log.Warning(ex, "Sightseeing index row failed"));
        // Native API takes the Adventure row ID. Verify one known completed entry in game.
        // Materialize on the UI thread; never carry PlayerState pointers into the sheet worker.
        return Sightseeing.Entries(sightseeingRows, AgentMap.Instance()->CurrentTerritoryId,
            id => PlayerState.Instance()->IsAdventureComplete(id)).ToArray();
    }
}
