using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.Evaluator;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Gratis.Rendering;
using Lumina.Excel.Sheets;

namespace Gratis.Controllers;

public sealed class ItemTooltipProvider : IItemTooltipSource {
    private readonly Dictionary<uint, ItemTooltipRecord?> cache = [];
    private Dictionary<uint, string>? materia;
    private readonly Dictionary<uint, ushort> tryOn = [];

    public ItemTooltipRecord? Get(uint id) {
        if (cache.TryGetValue(id, out var cached)) return cached;
        ItemTooltipRecord? result = null;
        try {
            if (Service.DataManager.GetExcelSheet<Item>().GetRowOrDefault(ItemTooltipModel.BaseId(id)) is { } item) {
                var effects = new List<string>();
                var description = item.Description.ExtractText().Trim();
                try { description = Service.SeStringEvaluator.Evaluate(item.Description).ExtractText().Trim(); }
                catch (Exception) { }
                if (description.Length > 0) effects.Add(description);
                var highQuality = id != item.RowId;
                for (var i = 0; i < item.BaseParam.Count; i++)
                    if (item.BaseParam[i].RowId != 0 && item.BaseParamValue[i] != 0
                        && item.BaseParam[i].ValueNullable is { } stat) {
                        var value = (int)item.BaseParamValue[i];
                        if (highQuality)
                            for (var j = 0; j < item.BaseParamSpecial.Count; j++)
                                if (item.BaseParamSpecial[j].RowId == stat.RowId) value += item.BaseParamValueSpecial[j];
                        effects.Add(ItemTooltipModel.Bonus(stat.Name.ExtractText(), value));
                    }
                AddActionEffects(item, highQuality, effects);
                materia ??= ReadMateria();
                if (materia.TryGetValue(item.RowId, out var bonus)) effects.Add(bonus);
                result = new(id, item.Name.ExtractText(), item.Icon) {
                    Category = item.ItemUICategory.ValueNullable?.Name.ExtractText() ?? "",
                    Rarity = item.Rarity, HighQuality = highQuality, StackSize = item.StackSize,
                    Effects = string.Join("\n", effects), ItemLevel = item.LevelItem.RowId,
                    Jobs = item.ClassJobCategory.ValueNullable?.Name.ExtractText() ?? "",
                    EquipLevel = item.LevelEquip, ShopPrice = item.PriceMid, SellPrice = item.PriceLow,
                    Untradable = item.IsUntradable, Unique = item.IsUnique,
                };
            }
        }
        catch (Exception error) { Service.Log.Debug(error, "[QuestLog] Item details unavailable for {Item}", id); }
        cache[id] = result;
        return result;
    }

    private static void AddActionEffects(Item item, bool highQuality, List<string> effects) {
        if (item.ItemAction.ValueNullable is not { } action) return;
        try {
            if (action.Action.RowId is 844 or 845 or 846) {
                var foodId = highQuality ? action.DataHQ[1] : action.Data[1];
                if (Service.DataManager.GetExcelSheet<ItemFood>().GetRowOrDefault(foodId) is not { } food) return;
                foreach (var bonus in food.Params) {
                    var name = bonus.BaseParam.ValueNullable?.Name.ExtractText() ?? "";
                    var value = highQuality ? bonus.ValueHQ : bonus.Value;
                    var maximum = highQuality ? bonus.MaxHQ : bonus.Max;
                    var text = ItemTooltipModel.Bonus(name, value, bonus.IsRelative, maximum);
                    if (text.Length > 0) effects.Add(text);
                }
            }
            else if (action.Action.RowId == 847) {
                var percent = highQuality ? action.DataHQ[0] : action.Data[0];
                var maximum = highQuality ? action.DataHQ[1] : action.Data[1];
                var text = Service.SeStringEvaluator.EvaluateFromAddon(998,
                    [new SeStringParameter(0u), new SeStringParameter((uint)percent), new SeStringParameter((uint)maximum)])
                    .ExtractText().Trim();
                if (text.Length > 0) effects.Add(text);
            }
        }
        catch (Exception error) { Service.Log.Debug(error, "[QuestLog] Item action details unavailable for {Item}", item.RowId); }
    }

    private static Dictionary<uint, string> ReadMateria() {
        var bonuses = new Dictionary<uint, string>();
        try {
            foreach (var row in Service.DataManager.GetExcelSheet<Materia>()) {
                var name = row.BaseParam.ValueNullable?.Name.ExtractText() ?? "";
                if (name.Length == 0) continue;
                for (var i = 0; i < row.Item.Count; i++)
                    if (row.Item[i].RowId != 0 && row.Value[i] != 0)
                        bonuses[row.Item[i].RowId] = ItemTooltipModel.Bonus(name, row.Value[i]);
            }
        }
        catch (Exception error) { Service.Log.Debug(error, "[QuestLog] Materia details unavailable"); }
        return bonuses;
    }

    public unsafe int? OwnedCount(uint id) {
        var inventory = InventoryManager.Instance();
        if (inventory == null || Service.ObjectTable.LocalPlayer == null) return null;
        var baseId = ItemTooltipModel.BaseId(id);
        return inventory->GetInventoryItemCount(baseId) + inventory->GetInventoryItemCount(baseId, true);
    }

    public unsafe bool CanLink => Service.ObjectTable.LocalPlayer != null && AgentChatLog.Instance() != null;
    public unsafe bool CanSearch => Service.ObjectTable.LocalPlayer != null && ItemFinderModule.Instance() != null;
    public unsafe void Link(uint id) {
        if (CanLink && id != 0) AgentChatLog.Instance()->LinkItem(id);
    }
    public unsafe void Search(uint id) {
        if (CanSearch && id != 0) ItemFinderModule.Instance()->SearchForItem(ItemTooltipModel.BaseId(id));
    }

    // Bit (race - 1) * 2 + sex is set when that race/gender may wear the item; 0 means it is not equippable gear.
    private ushort TryOnMask(uint id) {
        var baseId = ItemTooltipModel.BaseId(id);
        if (tryOn.TryGetValue(baseId, out var cached)) return cached;
        ushort mask = 0;
        try {
            if (Service.DataManager.GetExcelSheet<Item>().GetRowOrDefault(baseId) is { } item
                && item.EquipSlotCategory.ValueNullable is { } slot
                && (slot.MainHand != 0 || slot.OffHand != 0 || slot.Head != 0 || slot.Body != 0 || slot.Gloves != 0
                    || slot.Waist != 0 || slot.Legs != 0 || slot.Feet != 0 || slot.Ears != 0 || slot.Neck != 0
                    || slot.Wrists != 0 || slot.FingerL != 0 || slot.FingerR != 0)) {
                var race = item.EquipRestriction.ValueNullable;
                for (var r = 0; r < 8; r++)
                    for (var g = 0; g < 2; g++) {
                        var allowed = race is not { } e || (r switch {
                            0 => e.Hyur, 1 => e.Elezen, 2 => e.Lalafell, 3 => e.Miqote,
                            4 => e.Roegadyn, 5 => e.AuRa, 6 => e.Hrothgar, _ => e.Viera,
                        } && (g == 0 ? e.Male : e.Female));
                        if (allowed) mask |= (ushort)(1 << (r * 2 + g));
                    }
            }
        }
        catch (Exception error) { Service.Log.Debug(error, "[QuestLog] Try On data unavailable for {Item}", baseId); }
        tryOn[baseId] = mask;
        return mask;
    }

    public unsafe bool CanTryOn(uint id) {
        if (id == 0 || Service.ObjectTable.LocalPlayer == null) return false;
        var player = PlayerState.Instance();
        if (player == null || player->Race is < 1 or > 8 || player->Sex > 1) return false;
        return (TryOnMask(id) & (1 << ((player->Race - 1) * 2 + player->Sex))) != 0;
    }

    public bool TryOnEnabled => !Service.ClientState.IsPvP && Service.ObjectTable.LocalPlayer != null
        && !Service.Condition[ConditionFlag.InCombat];

    public unsafe void TryOn(uint id) {
        if (CanTryOn(id) && TryOnEnabled) AgentTryon.TryOn(0, ItemTooltipModel.BaseId(id), 0, 0, 0, false);
    }
}
