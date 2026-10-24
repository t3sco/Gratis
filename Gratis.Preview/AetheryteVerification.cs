using System.Numerics;
using Gratis.Controllers;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class AetheryteVerification
{
    public static void Selection()
    {
        var a = TravelFixture.Hub with { World = new Vector2(0, 0) };
        var b = a with { Id = "a:2", AetheryteId = 2, HubId = 2, World = new Vector2(400, 0), Name = "Far crystal" };
        var shard = TravelFixture.Shard with { World = new Vector2(500, 0) };
        var cases = new (string Name, PlaceEntry[] Entries, uint Map, Vector2 Target, uint[] Attuned, uint Active, bool Hops, uint? Expected)[] {
            ("empty", [], 1, new(20), [1], 0, false, null),
            ("origin", [a], 1, new(0), [1], 0, false, 1),
            ("target not player", [a,b], 1, new(390,0), [1,2], 0, false, 2),
            ("near origin", [a,b], 1, new(10,0), [1,2], 0, false, 1),
            ("unattuned nearest", [a,b], 1, new(390,0), [1], 0, false, 1),
            ("none attuned", [a,b], 1, new(390,0), [], 0, false, null),
            ("wrong map", [a,b], 2, new(390,0), [1,2], 0, false, null),
            ("zero map", [a,b], 0, new(390,0), [1,2], 0, false, null),
            ("nan target", [a], 1, new(float.NaN,0), [1], 0, false, null),
            ("inf target", [a], 1, new(float.PositiveInfinity,0), [1], 0, false, null),
            ("nan crystal", [a with { World = new(float.NaN,0) }], 1, new(0), [1], 0, false, null),
            ("missing position", [a with { World = null }], 1, new(0), [1], 0, false, null),
            ("tie ID", [b,a], 1, new(200,0), [1,2], 0, false, 1),
            ("shard no provider", [a,shard], 1, new(490,0), [1,10], 1, false, 1),
            ("active hub hop", [a,shard], 1, new(490,0), [1,10], 1, true, 10),
            ("active shard hop", [a,shard], 1, new(490,0), [1,10], 10, true, 10),
            ("wrong network", [a,b,shard], 1, new(490,0), [1,2,10], 2, true, 2),
            ("unattuned shard", [a,shard], 1, new(490,0), [1], 1, true, 1),
            ("other layer hub", [a with { MapId=2 },shard], 1, new(490,0), [1,10], 0, false, 1),
            ("other layer locked hub", [a with { MapId=2 },shard], 1, new(490,0), [10], 0, false, null),
            ("ignore noncrystal", [a,b with { Kind=PlaceKind.Marker }], 1, new(390,0), [1,2], 0, false, 1),
            ("negative coords", [a,b with { World=new(-400,0) }], 1, new(-390,0), [1,2], 0, false, 2),
            ("unknown active", [a,shard], 1, new(490,0), [1,10], 999, true, 1),
            ("zero sheet ID", [a with { AetheryteId=0 }], 1, new(0), [0], 0, false, null),
        };
        foreach (var c in cases) {
            var found = AetheryteSelection.Nearest(c.Entries,c.Map,c.Target,p=>new(c.Attuned.Contains(p.AetheryteId)),c.Active,c.Hops);
            TravelVerification.Require(found?.Destination.AetheryteId == c.Expected, "chip selection: " + c.Name);
        }
        TravelVerification.Require(!AetheryteSelection.Useful(1,new(100,0),1,new(0)), "near target hides chip");
        TravelVerification.Require(AetheryteSelection.Useful(1,new(200,0),1,new(0)), "far target boundary");
        TravelVerification.Require(AetheryteSelection.Useful(2,new(0),1,new(0)), "other zone");
        TravelVerification.Require(AetheryteSelection.Useful(1,new(0),1,null), "off-map player");
        Console.WriteLine($"Aetherytes: {cases.Length} selection cases plus distance/zone gates; attunement and distance are relative to target.");
    }

    public static void Frames()
    {
        var config = PaletteVerification.Config();
        using var driver = new ScriptedFrameDriver(config,new FrameScript());
        var source = new TravelFixture(driver);
        var ui = driver.Context.Assistance = new MapAssistance(NullTravelProvider.Instance,source,()=>true,()=>{});
        driver.Step(new(0)); driver.Step(new(.02));
        driver.Renderer.BeginSpotlight(new(new(550,0),1,SpotlightSource.Search,IncludePlayer:false));
        driver.Step(new(.04)); driver.Step(new(.06));
        TravelVerification.Require(ui.Chip?.Destination == TravelFixture.Hub,"nearest main crystal without Lifestream");
        driver.Step(new(.08,Mouse:ui.ChipButtonCenter)); driver.Step(new(.10,Left:true)); driver.Step(new(.12,Left:false));
        TravelVerification.Require(source.Teleports == 1,"chip invokes shared teleport boundary");
        config.AetheryteChips=false; driver.Step(new(.14));
        TravelVerification.Require(ui.Chip == null,"chip option off");
        config.AetheryteChips=true; config.VisualStyle=1; driver.Step(new(.16));
        TravelVerification.Require(source.Teleports == 1,"Classic emits no chip action");
    }
}
