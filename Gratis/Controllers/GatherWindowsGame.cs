using System.Linq;
using Gratis.Rendering;
using Lumina.Excel.Sheets;

namespace Gratis.Controllers;

/// <summary>
/// Reads a node's timed windows from GatheringPointTransient. The sheet is 1:1 with GatheringPoint, so the join key
/// is the object's BaseId (not GatheringPointBase). 65535 means no window. Game-only; the maths is GatherWindow.
/// </summary>
public static class GatherWindowsGame {
    public static GatherSlot[]? Load(uint gatheringPointId) {
        var transients = Service.DataManager.GetExcelSheet<GatheringPointTransient>();
        if (!transients.TryGetRow(gatheringPointId, out var row))
            return null;

        int[] starts = [], durations = [];
        if (row.GatheringRarePopTimeTable.IsValid) {
            var table = row.GatheringRarePopTimeTable.Value;
            starts = table.StartTime.Select(v => (int)v).ToArray();
            durations = table.Duration.Select(v => (int)v).ToArray();
        }
        return GatherWindow.FromRow(row.EphemeralStartTime, row.EphemeralEndTime, starts, durations);
    }
}
