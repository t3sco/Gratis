using System.Numerics;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static partial class Program
{
    private static bool TryChromeThreeScene(string[] args)
    {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length) return false;
        var name = args[at + 1];
        if (!name.StartsWith("bottom-cards", StringComparison.Ordinal) && !name.StartsWith("travel-popover", StringComparison.Ordinal)) return false;
        if (name.Contains("settings")) {
            var outIndex = Array.IndexOf(args, "--out");
            var output = outIndex < 0 ? Path.Combine("_preview", name + ".png") : args[outIndex + 1];
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            RenderSettings(Options.Parse(["--scene", "settings-chrome", "--size", "840x640", "--out", output])!,
                (layout, _, _) => layout.Search = name.Contains("coordinates") ? "Gratis coordinates" : "Gratis title");
            return true;
        }
        var size = name.Contains("small") ? new Vector2(360, 300) : name.Contains("large") ? new Vector2(1280, 800) : new Vector2(780, 600);
        var c = ChromeLayoutVerification.Config(size); c.ReduceMotion = false;
        if (name.Contains("region")) c.LocationCardShows = LocationCardContent.RegionZoneArea;
        var offset = name.Contains("left") ? new Vector2(-size.X / 2 + 36, 0) : name.Contains("right") ? new Vector2(size.X / 2 - 36, 0) :
            name.Contains("top") ? new Vector2(-size.X / 3, -size.Y / 2 + 52) : name.Contains("bottom") && name.StartsWith("travel") ? new Vector2(-size.X / 3, size.Y / 2 - 36) : Vector2.Zero;
        var shard = name.Contains("aethernet"); var place = shard ? TravelFixture.Shard : TravelFixture.Hub;
        var world = offset / c.MapScale;
        using var d = new ScriptedFrameDriver(c, new FrameScript { Width = (int)size.X + 64, Height = (int)size.Y + 64,
            Title = "Eorzea - La Noscea - Limsa Lominsa Lower Decks - Hawkers' Alley###GratisMapWindow", PlayerCoordinates = new(18.4f, 23.7f),
            Markers = [new(shard ? 60454u : 60453u, world)] });
        d.Markers[0].PrimaryText = () => place.Name; d.Markers[0].DataId = place.AetheryteId;
        d.Markers[0].IconFamily = shard ? MarkerIconFamily.Aethernet : MarkerIconFamily.Aetheryte;
        var ui = d.Context.Assistance = new MapAssistance(new FakeTravelIpc().Provider(), new TravelFixture(d), () => true, () => { });
        for (var i = 0; i < 8; i++) d.Step(new(i * .04));
        var icon = d.Renderer.WorldToScreen(world, d.Window.MapDrawOffset);
        var moving = name.Contains("moving");
        if (name.StartsWith("travel")) {
            d.Step(new(.32, Mouse: icon, Moving: moving)); d.Step(new(.36, Left: true, Moving: moving));
            d.Step(new(.40, Left: false, Moving: moving)); d.Step(new(.44, Moving: moving));
            if (moving ? ui.CardMax != Vector2.Zero : ui.CardMax == Vector2.Zero)
                throw new InvalidOperationException("Travel scene did not reach its expected open/blocked state: " + name);
        }
        else { d.Step(new(.32, Mouse: name.Contains("hover") ? c.WindowPosition + size / 2 : new(-1000))); d.Step(new(.36)); }
        var outAt = Array.IndexOf(args, "--out"); var path = Path.GetFullPath(outAt < 0 ? Path.Combine("_preview", name + ".png") : args[outAt + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); d.Save(path); Console.WriteLine(path); return true;
    }
}
