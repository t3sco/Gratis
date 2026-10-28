using Gratis.Rendering;

namespace Gratis.Preview;

internal sealed class SummaryItemsFixture : IItemTooltipSource {
    public uint? Linked { get; private set; }
    public uint? Searched { get; private set; }
    public int Loads { get; private set; }
    public bool CanLink => true;
    public bool CanSearch => true;
    public int? OwnedCount(uint id) => 0;
    public void Link(uint id) => Linked = id;
    public void Search(uint id) => Searched = id;
    public ItemTooltipRecord Get(uint id) {
        Loads++;
        return new(id, id == 4 ? "Wind Shard" : "Quicktongue Materia I", id == 4 ? 20004u : 20013u) {
            Category = id == 4 ? "Crystal" : "Materia", StackSize = 999,
            Effects = id == 4 ? "A tiny crystalline manifestation of aetheric wind energy." : "Spell Speed +8",
            ItemLevel = 15, Jobs = "All Classes", EquipLevel = 1, Rarity = 2, SellPrice = 1,
            Untradable = id == 4, Unique = id == 4,
        };
    }
}

internal static class SummaryFixture {
    public static void Apply(QuestLogFixture fixture, string variant) {
        var original = fixture.Snapshot.Quests[0];
        var quest = original with { StartNpc = "Wuk Lamat", StartArea = "Tuliyollal", Jobs = "Disciple of War or Magic" };
        if (variant == "long-text")
            quest = quest with {
                Name = "An Unusually Long Journey Beyond the Mountains and the Distant Horizon",
                StartNpc = "A remarkably well-travelled companion of the Third Promise", StartArea = "The highlands beyond Tuliyollal",
                Prerequisites = "The Promise of a New Beginning\nThe Road Beyond the Horizon", MoreConditions = true,
                Description = string.Join("\n\n", Enumerable.Repeat(
                    "Your companions await you at the overlook, where the winding mountain road opens onto the distant sea. "
                    + "Join Wuk Lamat and her companions to discuss the journey ahead and the people whose stories have brought you here.", 3)),
                Objective = "Speak with the unusually well-travelled companion at the western overlook, then return to Tuliyollal.",
            };
        if (variant == "many-rewards")
            quest = quest with { Rewards = new(12500, 1840000, Enumerable.Range(0, 18).Select(i =>
                new QuestLogRewardItem((uint)(4 + i), "A long reward item name " + (i + 1), (uint)(20004 + i),
                    (uint)(i == 0 ? 999 : i + 1), i >= 10)).ToArray()) };
        if (variant == "missing")
            quest = quest with { StartNpc = "", StartArea = "", Jobs = "", Prerequisites = "", Rewards = null,
                Description = "", Objective = "", Duty = "", Targets = [] };
        fixture.Snapshot = fixture.Snapshot with { Revision = fixture.Snapshot.Revision + 1,
            Quests = [quest, .. fixture.Snapshot.Quests.Skip(1)] };
    }
}
