using System;
using System.Collections.Generic;

namespace Gratis.Rendering;

public sealed record ItemTooltipRecord(uint Id, string Name, uint Icon) {
    public string Category { get; init; } = "";
    public byte Rarity { get; init; }
    public bool HighQuality { get; init; }
    public uint StackSize { get; init; }
    public string Effects { get; init; } = "";
    public uint ItemLevel { get; init; }
    public string Jobs { get; init; } = "";
    public byte EquipLevel { get; init; }
    public uint? ShopPrice { get; init; }
    public uint? SellPrice { get; init; }
    public bool Untradable { get; init; }
    public bool Unique { get; init; }
}

public interface IItemTooltipSource {
    ItemTooltipRecord? Get(uint id);
    int? OwnedCount(uint id) => null;
    bool CanLink => false;
    bool CanSearch => false;
    void Link(uint id) { }
    void Search(uint id) { }
}

public sealed record ItemTooltipSection(string Heading, string Text);

public static class ItemTooltipModel {
    public static IReadOnlyList<ItemTooltipSection> Sections(ItemTooltipRecord item) {
        var sections = new List<ItemTooltipSection>(4);
        if (!string.IsNullOrWhiteSpace(item.Effects))
            sections.Add(new("Effects", item.Effects));
        var requirements = new List<string>(3);
        if (item.ItemLevel > 0) requirements.Add($"Item Level {item.ItemLevel}");
        if (!string.IsNullOrWhiteSpace(item.Jobs)) requirements.Add(item.Jobs);
        if (item.EquipLevel > 0) requirements.Add($"Lv. {item.EquipLevel}");
        if (requirements.Count > 0)
            sections.Add(new("Requirements", string.Join("\n", requirements)));
        var trade = new List<string>(3);
        if (item.ShopPrice is > 0) trade.Add($"Shop selling price: {item.ShopPrice:N0} gil");
        if (item.SellPrice is { } sell) trade.Add(sell > 0 ? $"Sells for {sell:N0} gil" : "Unsellable");
        if (item.Untradable || item.Unique)
            trade.Add(item.Untradable && item.Unique ? "Untradable · Unique" : item.Untradable ? "Untradable" : "Unique");
        if (trade.Count > 0) sections.Add(new("Base Item", string.Join("\n", trade)));
        return sections;
    }

    public static string Stack(ItemTooltipRecord item, uint count, int? owned) =>
        item.StackSize > 0 && owned is >= 0 ? $"{count:N0}/{item.StackSize:N0} (Total: {owned:N0})" : "";

    public static IReadOnlyList<string> MenuEntries(bool canLink, bool canSearch) =>
        canSearch ? [canLink ? "Link" : "Copy name", "Search for Item"] : [canLink ? "Link" : "Copy name"];

    public static uint BaseId(uint id) => id is >= 1_000_000 and < 2_000_000 ? id - 1_000_000 : id;

    public static string Bonus(string name, int value, bool relative = false, int maximum = 0) =>
        string.IsNullOrWhiteSpace(name) || value == 0 ? "" : relative
            ? $"{name} {value:+0;-0}% (Max {maximum})" : $"{name} {value:+0;-0}";
}
