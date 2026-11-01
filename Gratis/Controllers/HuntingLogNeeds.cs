using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Gratis.Classes;
using Lumina.Excel.Sheets;

namespace Gratis.Controllers;

// Which monsters the player's current hunting log rank (own class and Grand Company) still asks for.
// Layout and rank base verified against other plugins' measurements: MonsterNoteManager slots are indexed by the
// log's class index (ClassJob/GrandCompany .MonsterNote), Rank is 0-based, counts sit in sheet order, and a
// class block is five ranks of ten rows (a company block three), found by its row names.
public sealed class HuntingLogNeeds {
    private const int EntriesPerRank = 10;
    private Dictionary<uint, uint>? blockStarts;

    // BNpcName ids of the monsters still needed; empty when anything about the layout looks wrong.
    public unsafe void Refresh(HashSet<uint> needed, uint classJobId) {
        needed.Clear();
        var manager = MonsterNoteManager.Instance();
        if (manager == null) return;
        var data = Service.DataManager;
        var starts = blockStarts ??= BuildBlockStarts();
        var notes = data.GetExcelSheet<MonsterNote>();
        var targets = data.GetExcelSheet<MonsterNoteTarget>();
        if (notes == null || targets == null) return;
        if (data.GetExcelSheet<ClassJob>().GetRowOrDefault(classJobId) is { } job)
            Add(manager, starts, notes, targets, needed, job.MonsterNote.RowId, 5);
        var company = PlayerState.Instance() is var state && state != null ? state->GrandCompany : (byte)0;
        if (data.GetExcelSheet<GrandCompany>().GetRowOrDefault(company) is { } gc)
            Add(manager, starts, notes, targets, needed, gc.MonsterNote.RowId, 3);
    }

    private static unsafe void Add(MonsterNoteManager* manager, Dictionary<uint, uint> starts,
        Lumina.Excel.ExcelSheet<MonsterNote> notes, Lumina.Excel.ExcelSheet<MonsterNoteTarget> targets,
        HashSet<uint> needed, uint classIndex, int ranks) {
        if (!starts.TryGetValue(classIndex, out var start)) return;
        // Trust a slot only when it claims this class index, so a shifted layout reads nothing instead of the wrong log.
        for (var slot = 0; slot < manager->RankData.Length; slot++) {
            var info = manager->RankData[slot];
            if (info.Index != classIndex) continue;
            if (info.Rank < 0 || info.Rank >= ranks) return;
            var first = start + (uint)(info.Rank * EntriesPerRank);
            for (var entry = 0; entry < EntriesPerRank; entry++) {
                if (!notes.TryGetRow(first + (uint)entry, out var note)) continue;
                var counts = info.RankData[entry];
                for (var i = 0; i < 4; i++) {
                    var required = note.Count[i];
                    if (required == 0 || counts[i] >= required) continue;
                    if (targets.TryGetRow(note.MonsterNoteTarget[i].RowId, out var target) && target.BNpcName.RowId != 0)
                        needed.Add(target.BNpcName.RowId);
                }
            }
            return;
        }
    }

    // Entries are named "<class> 21", the only link the sheet carries between a block and its class index.
    private static Dictionary<uint, uint> BuildBlockStarts() {
        var data = Service.DataManager;
        var indexByName = new Dictionary<string, uint>(global::System.StringComparer.OrdinalIgnoreCase);
        foreach (var job in data.GetExcelSheet<ClassJob>()) {
            var index = job.MonsterNote.RowId;
            var name = job.Name.ExtractText().Trim();
            if (index < 12 && name.Length > 0) indexByName.TryAdd(name, index);
        }
        foreach (var company in data.GetExcelSheet<GrandCompany>()) {
            var index = company.MonsterNote.RowId;
            var name = company.Name.ExtractText().Replace("[p]", "").Trim();
            if (index < 12 && name.Length > 0) indexByName.TryAdd(name, index);
        }
        var starts = new Dictionary<uint, uint>();
        foreach (var row in data.GetExcelSheet<MonsterNote>()) {
            var name = row.Name.ExtractText().Trim();
            var cut = name.LastIndexOf(' ');
            if (cut <= 0 || !indexByName.TryGetValue(name[..cut], out var index)) continue;
            if (!starts.TryGetValue(index, out var known) || row.RowId < known) starts[index] = row.RowId;
        }
        return starts;
    }
}
