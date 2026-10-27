using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

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
    private static readonly ConditionalWeakTable<ItemTooltipRecord, Presentation> presentations = new();

    internal sealed class Presentation {
        private readonly Dictionary<string, SectionHeaderCache> headings = [];
        public string Heading(string label) {
            if (!headings.TryGetValue(label, out var header)) headings.Add(label, header = new());
            return header.Upper(label);
        }

        private CultureInfo? culture;
        private uint count;
        private int? owned;
        private string? stack;
        public IReadOnlyList<ItemTooltipSection> Sections { get; private set; } = [];
        public string Title { get; private set; } = "";

        public void Update(ItemTooltipRecord item) {
            var current = CultureInfo.CurrentCulture;
            if (ReferenceEquals(culture, current) && current.IsReadOnly && current.NumberFormat.IsReadOnly)
                return;
            culture = current;
            Sections = BuildSections(item);
            Title = item.Name + (item.HighQuality ? "  HQ" : "");
            stack = null;
        }

        public string Stack(ItemTooltipRecord item, uint nextCount, int? nextOwned) {
            if (stack == null || count != nextCount || owned != nextOwned) {
                count = nextCount;
                owned = nextOwned;
                stack = ItemTooltipModel.Stack(item, count, owned);
            }
            return stack;
        }
    }

    internal static Presentation For(ItemTooltipRecord item) {
        var result = presentations.GetValue(item, static _ => new());
        result.Update(item);
        return result;
    }

    public static IReadOnlyList<ItemTooltipSection> Sections(ItemTooltipRecord item) => For(item).Sections;

    private static IReadOnlyList<ItemTooltipSection> BuildSections(ItemTooltipRecord item) {
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
