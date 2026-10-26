using System.Numerics;
using Gratis.Controllers;
using Gratis.Rendering;

namespace Gratis.Preview;

internal sealed class QuestLogFixture : IQuestLogSource {
    private readonly ScriptedFrameDriver driver;
    public QuestLogSnapshot Snapshot { get; set; }
    public uint? Shown { get; private set; }
    public QuestLogFixture(ScriptedFrameDriver driver) {
        this.driver = driver;
        QuestLogQuest Quest(uint id, string name, ushort level, QuestBucket bucket, string objective,
            string duty = "", bool target = true) => new(id, name, level, bucket,
                bucket == QuestBucket.Scenario ? 71001u : 71141u, objective, duty,
                target ? [new PlaceEntry("quest-log:" + id, PlaceKind.Quest, name, "", driver.MapId,
                    driver.TerritoryId, new Vector2(280, -160)) { QuestRowId = id, Icon = 71001 }] : []);
        Snapshot = new(1, QuestLogStatus.Ready, [
            Quest(70964, "Beyond the Mountains", 100, QuestBucket.Scenario, "Speak with Wuk Lamat at the overlook."),
            Quest(69703, "Cutting the Cheese", 80, QuestBucket.SideQuests, "Deliver the cheese to the waiting scholar."),
            Quest(66233, "Hallo Halatali", 20, QuestBucket.SideQuests, "Speak with Fafajoni.", "Halatali · Duty unlock"),
            Quest(70371, "A Hunter True", 100, QuestBucket.JobClass, "Meet your companions in Tuliyollal."),
            Quest(68628, "Annihilation", 70, QuestBucket.Chronicles, "Speak with the company dramaturge."),
            Quest(70501, "A Little Kindness Goes a Long Way", 100, QuestBucket.AlliedSocieties,
                "Deliver the supplies to the Pelupelu."),
            Quest(67060, "An Unusually Long Quest Name from a Distant Corner of Eorzea", 50, QuestBucket.Other,
                "", target: false),
        ], "Beyond the Mountains", new(5, "Dawntrail", 61880, 127, 143), 1);
    }

    public bool Show(QuestLogQuest quest) {
        if (quest.Targets.FirstOrDefault()?.World is not { } world)
            return false;
        Shown = quest.RowId;
        if (!driver.Renderer.BeginQuestSpotlight(world, driver.MapId, true, quest.Icon)) {
            driver.Config.FollowPlayer = false;
            driver.Renderer.DrawOffset = driver.CoordinateOffset(world);
        }
        return true;
    }
}
