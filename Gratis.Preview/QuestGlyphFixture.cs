using Gratis.Controllers;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class QuestGlyphFixture {
    public const string Strange = "A §tr\u03b1nge New World";
    public const string Victory = "Vic\u2528\u03bfry  \u0308 \u0308 \u0308\u2573, \u2502\u0306\u2502\u2502\u03b5 Lost";
    public const string NoFound = "\u2523\u2528\u0308//\u0308 No\u2528\u03a6oun\u0394•••";
    public const string Knowledge = "Where Kn\u221ewledge Leads";

    public static QuestLogQuest[] Completed() => [
        Quest(69989, Strange),
        Quest(69994, Knowledge),
        Quest(69995, Victory) with { Prerequisites = Knowledge },
        Quest(69996, NoFound) with { Prerequisites = Victory },
        Quest(0, "A Bargain Struck"),
    ];

    private static QuestLogQuest Quest(uint id, string name) =>
        new(id, name, id == 69989 || id == 69994 ? (ushort)89 : (ushort)90,
            QuestBucket.Scenario, 71003, "", "", []) {
            Completed = true, ClientKnown = true, Expansion = 4, ExpansionIcon = 61879,
            StartNpc = "Ultima Thule", Jobs = "Disciple of War or Magic",
        };
}
