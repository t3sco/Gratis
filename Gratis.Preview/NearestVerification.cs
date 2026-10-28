using System.Numerics;
using Gratis.Controllers;
using ImGuiNET;

namespace Gratis.Preview;

internal static class NearestVerification {
    private static void Require(bool yes, string message) {
        if (!yes)
            throw new InvalidOperationException(message);
    }

    internal static readonly (string Query, uint Icon)[] Cases = [
        ("inn",60436),("innkeeper",60559),("inn room",60988),
        ("market board",60460),("marketboard",60570),("mrkt",60460),("market",60570),
        ("mender",60434),("repair",60910),("repairs",60434),
        ("aetheryte",60453),("crystal",60959),("teleport",60453),
        ("aethernet shard",60430),("aethernet",60452),("shard",60430),
        ("retainer bell",60425),("summoning bell",60560),("bell",60425),("retainer",60560),
        ("mailbox",60426),("mail",60426),("delivery moogle",60426),
        ("vendor",60412),("shop",61731),("merchant",60412),
        ("chocobo stabler",60581),("chocobo stable",60581),("stable",60581),("stabler",60581),
        ("fishing",60445),("treasure",60913),("housing",60427),("party",60421),
    ];
    public static void Ranking() {
        var context = new PlaceSearchContext(1, 1, Vector2.Zero);
        foreach (var item in Cases) {
            var near = new PlaceEntry("near", PlaceKind.Marker, "Localized NPC", "", 1, 1,
                new(20, 0)) { Icon = item.Icon };
            PlaceEntry[] entries = [near with { Id = "far", World = new(200, 0) }, near,
                near with { Id = "other-map", MapId = 2, World = new(1, 0) },
                    near with { Id = "missing", World = null }];
            foreach (var prefix in new[] { "nearest ", "n:" }) {
                var result = PlaceSearch.Find(entries, prefix + item.Query, PlaceKind.Zone, context,
                    new Dictionary<string, long>(), _ => new(true));
                Require(result.Length == 2 && result[0].Place.Id == "near" && result[1].Distance == 200,
                    item.Query + " category/ranking/map boundaries");
            }
        }
        var all = Cases.Select((c, i) =>
            new PlaceEntry(i.ToString(), PlaceKind.Marker, "Service", "", 1, 1,
                new(i * 20, 0)) { Icon = c.Icon }).ToArray();
        Require(NearestPlaces.Find(all, "service", context, _ => new(true)).Length >= 20, "Marker category name");
        Require(NearestPlaces.Find(all, "", context with {
            Player = null
        }, _ => new(true)).Length == 0, "No player: do not invent distances");
        Require(NearestPlaces.Find(all, "zzzzzz", context, _ => new(true)).Length == 0, "Unknown category");
        Require(NearestPlaces.Direction(default, default) == "Here"
            && NearestPlaces.Direction(default, new(0, -1)) == "↑"
            && NearestPlaces.Direction(default, new(1, 1)) == "↘",
            "World compass");
    }

    public static void Frames() {
        var script = new FrameScript {
            Palette = true,
            Markers = [new(60460, new(600, 0)), new(60570, new(100, 0)), new(60436, new(-60, 0))]
        };
        using var driver = new ScriptedFrameDriver(PaletteVerification.Config(), script);
        driver.Step(new(0));
        driver.Step(new(.02));
        driver.Palette!.Open("nearest mrkt");
        driver.Step(new(.04));
        Require(driver.Palette.Results.Count == 3 && driver.Palette.Results[0].Distance < 100,
            "Merge current markers + index, nearest first");
        driver.Step(new(.06, Keys: [ImGuiKey.Enter]));
        Require(!driver.Palette.IsOpen && driver.Renderer.Spotlight.Active, "Enter uses Spotlight");
        driver.Step(new(.08, Keys: []));
        driver.Palette.Open("n:inn");
        driver.Step(new(.10));
        Require(driver.Palette.Results.Count == 1 && driver.Palette.Results[0].Place.Icon == 60436,
            "n: uses icon semantics");
        driver.Palette.Close();
        driver.Palette.Open();
        driver.Step(new(.12));
        Require(driver.Palette.IsOpen, "Empty palette chips render safely");
    }
}