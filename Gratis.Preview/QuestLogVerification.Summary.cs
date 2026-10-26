using System.Numerics;
using Gratis.Rendering;
using Gratis.Windows;
using ImGuiNET;

namespace Gratis.Preview;

internal static partial class QuestLogVerification {
    private static void SummaryModel() {
        Require(QuestLogText.Available == "Available" && QuestLogText.NearbySubtitle == "Quests available nearby",
            "Available wording is concise and stable");
        Require(QuestLogText.TabLabel(QuestLogTab.Unaccepted) == "Available"
            && QuestLogText.TabLabel(QuestLogTab.Accepted) == "Accepted"
            && QuestLogText.TabLabel(QuestLogTab.Completed) == "Completed"
            && QuestLogText.EmptyTab(QuestLogTab.Unaccepted) == "No available quests.",
            "Tab, header and empty-state labels keep internal enum names out of the UI");
        Require(SettingsLayout.Entries.Single(e => e.Path == "QuestLogShowLocked").Tooltip is { } availableTip
            && availableTip.Contains("Available") && !availableTip.Contains("Unaccepted")
            && availableTip.Contains("without checking every requirement"), "Available settings explain client-known limits");
        ItemHoverAndPrefetch();
        var empty = new ItemTooltipRecord(1, "Empty item", 1);
        Require(ItemTooltipModel.Sections(empty).Count == 0 && ItemTooltipModel.Stack(empty, 2, null) == "",
            "Unavailable item details and inventory totals are hidden");
        var item = empty with { Effects = "Spell Speed +8", ItemLevel = 15, Jobs = "All Classes", EquipLevel = 1,
            StackSize = 999, ShopPrice = 120, SellPrice = 0, Unique = true, Untradable = true };
        var sections = ItemTooltipModel.Sections(item);
        Require(sections.Select(s => s.Heading).SequenceEqual(["Effects", "Requirements", "Base Item"])
            && sections[0].Text == "Spell Speed +8" && sections[1].Text == "Item Level 15\nAll Classes\nLv. 1"
            && sections[2].Text.Contains("Shop selling price: 120 gil") && sections[2].Text.Contains("Unsellable")
            && sections[2].Text.Contains("Untradable · Unique"), "Item sections retain reliable game fields");
        Require(ItemTooltipModel.Stack(item, 2, 0) == "2/999 (Total: 0)"
            && ItemTooltipModel.Stack(item, 2, null) == "" && ItemTooltipModel.Stack(item, 2, -1) == "",
            "Owned count is only displayed when read successfully");
        Require(ItemTooltipModel.MenuEntries(true, true).SequenceEqual(["Link", "Search for Item"])
            && ItemTooltipModel.MenuEntries(false, false).SequenceEqual(["Copy name"])
            && ItemTooltipModel.MenuEntries(false, true).SequenceEqual(["Copy name", "Search for Item"]),
            "Item actions follow source capabilities without unsupported entries");
        Require(ItemTooltipModel.BaseId(1_000_123) == 123 && ItemTooltipModel.BaseId(123) == 123,
            "HQ reward ids resolve their base item");
        Require(ItemTooltipModel.Bonus("Spell Speed", 8) == "Spell Speed +8"
            && ItemTooltipModel.Bonus("Vitality", 10, true, 105) == "Vitality +10% (Max 105)"
            && ItemTooltipModel.Bonus("", 8) == "" && ItemTooltipModel.Bonus("Spell Speed", 0) == "",
            "Consumable bonuses preserve flat and capped relative values and hide empty stats");
    }

    private static void ItemHoverAndPrefetch() {
        var hover = new ItemTooltipHover();
        Require(!hover.Observe(1, 0) && !hover.Observe(1, .10) && hover.Observe(1, .12),
            "First item hover waits 120 ms without a longer presentation delay");
        Require(hover.Visible(.20) && hover.Observe(2, .21) && hover.Visible(.30) && hover.Observe(3, .31),
            "Visible item details bridge slot gaps and switch immediately");
        Require(!hover.Visible(.5) && !hover.Observe(4, .5) && hover.Observe(4, .62),
            "Leaving the reward grid restores hover intent");
        hover.Reset();
        Require(!hover.Visible(.63) && !hover.Observe(4, .63), "Closing or switching summaries resets hover intent");
        hover.EndFrame(false, .70);
        Require(!hover.Observe(4, .78), "An interrupted cold hover does not accumulate intent across separate visits");

        var source = new CountingItemSource();
        var cache = new ItemTooltipCache();
        var rewards = Enumerable.Range(1, ItemTooltipCache.Capacity + 10)
            .Select(i => new QuestLogRewardItem((uint)i, "Item " + i, 1, 1)).ToArray();
        cache.Prepare(rewards, source);
        Require(source.Reads.Count == ItemTooltipCache.LoadsPerFrame && cache.Get(1) != null && cache.Get(3) == null,
            "Summary prefetch bounds work per frame and tolerates unavailable item rows");
        for (var i = 0; i < ItemTooltipCache.Capacity; i++) cache.Prepare(rewards, source);
        Require(source.Reads.Count == ItemTooltipCache.Capacity && source.Reads.Distinct().Count() == source.Reads.Count,
            "Prefetch caps total work and does not retry cached or missing rows");
        for (var i = 0; i < 100; i++) cache.Get((uint)(i % rewards.Length + 1));
        Require(source.Reads.Count == ItemTooltipCache.Capacity, "Hover cache lookups never invoke the data provider");
        var replacement = new CountingItemSource();
        QuestLogRewardItem[] duplicates = [rewards[0], rewards[0], rewards[2]];
        cache.Prepare(duplicates, replacement);
        Require(replacement.Reads.SequenceEqual([1u, 3u]) && cache.Get(5) == null,
            "Changing the item source drops stale details and duplicate slots share one lookup");
    }

    private sealed class CountingItemSource : IItemTooltipSource {
        public List<uint> Reads { get; } = [];
        public ItemTooltipRecord? Get(uint id) {
            Reads.Add(id);
            return id == 3 ? null : new(id, "Item " + id, 1);
        }
    }

    private static void SummaryFrames() {
        using var driver = new ScriptedFrameDriver(Program.QuestLogConfig(1100, 850),
            new FrameScript { Width = 1100, Height = 850, QuestLog = true });
        var panel = driver.Context.QuestLog;
        var fixture = (QuestLogFixture)driver.Context.QuestSource!;
        SummaryFixture.Apply(fixture, "default");
        double time = 0;
        void Step(Vector2? mouse = null, bool left = false, bool right = false, bool escape = false, float wheel = 0) =>
            driver.Step(new(time += .04, Mouse: mouse ?? new(-1000), Left: left, Right: right, Escape: escape, Wheel: wheel));
        void Settle(Vector2? mouse = null) { for (var i = 0; i < 16; i++) Step(mouse); }
        void Click(Vector4 bounds, bool right = false) {
            var p = Center(bounds);
            Step(p); Step(p, left: !right, right: right); Step(p);
        }
        Settle();
        Click(panel.Rows.First().Bounds); Settle();
        Require(panel.RewardBounds.Count == 2 && MathF.Abs(panel.RewardBounds[0].X - panel.RewardBounds[1].X) < .1f,
            "Guaranteed and optional reward grids share their left edge");
        var footer = panel.DetailMapBounds;
        var reward = Center(panel.RewardBounds[0]);
        var items = (SummaryItemsFixture)fixture.Items;
        Require(items.Loads == 2, "Opening the summary prefetches reward details before hover");
        Step(reward);
        Step(reward);
        Step(reward);
        var beforeIntent = ImGui.GetFrameCount();
        Require(ItemTooltipDrawing.LastTooltipFrame != beforeIntent, "Reward tooltip stays hidden during short incidental hover");
        Step(reward);
        Step(reward);
        Require(ItemTooltipDrawing.LastTooltipFrame == ImGui.GetFrameCount() && ItemTooltipDrawing.LastTooltipItem == 4,
            "Reward tooltip appears within 120–160 ms in actual frames");
        Step(reward + new Vector2(32, 0));
        Require(ItemTooltipDrawing.LastTooltipFrame == ImGui.GetFrameCount(), "A gap between slots retains the displayed tooltip");
        Step(Center(panel.RewardBounds[1]));
        Require(ItemTooltipDrawing.LastTooltipFrame == ImGui.GetFrameCount() && ItemTooltipDrawing.LastTooltipItem == 4551
            && items.Loads == 2, "Moving to another reward updates immediately without loading item data on hover");
        Settle(reward);
        Require(Valid(ItemTooltipDrawing.LastTooltipBounds), "Reward hover presents rich item details");
        Click(panel.RewardBounds[0], right: true); Settle(reward);
        Require(Valid(panel.RewardMenuBounds) && ((SummaryItemsFixture)fixture.Items).Linked == null,
            "Reward menu opens without executing a game action");
        var menu = panel.RewardMenuBounds;
        Click(new(menu.X + 12, menu.Y + 12, menu.Z - 12, menu.Y + 48)); Settle();
        Require(((SummaryItemsFixture)fixture.Items).Linked == 4 && !Valid(panel.RewardMenuBounds),
            "Link is dispatched only by choosing its menu entry and closes the menu");
        Click(panel.RewardBounds[0], right: true); Settle();
        Step(escape: true); Step(); Settle();
        Require(panel.DetailOpen && !Valid(panel.RewardMenuBounds), "Escape dismisses the item menu before its summary");
        Click(panel.RewardBounds[0], right: true); Settle();
        Click(panel.DetailCloseBounds); Settle();
        Require(!Valid(panel.RewardMenuBounds), "Outside click dismisses the item menu");
        if (!panel.DetailOpen) { Click(panel.Rows.First().Bounds); Settle(); }
        Step(Center(panel.DetailBounds), wheel: -8); Settle();
        Require(panel.DetailMapBounds == footer, "Scrolling detail content keeps the primary action pinned");
    }
}
