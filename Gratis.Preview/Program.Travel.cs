using Gratis.Rendering;

namespace Gratis.Preview;

internal static partial class Program {
    private static bool TryTravelScene(string[] args) {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || args[at + 1] is not ("travel-hover" or "travel-progress"))
            return false;
        var scene = args[at + 1];
        var outAt = Array.IndexOf(args, "--out");
        var output = Path.GetFullPath(outAt < 0 ? Path.Combine("_preview", scene + ".png") : args[outAt + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var config = PaletteVerification.Config();
        config.WindowSize = new(820, 620);
        using var driver = new ScriptedFrameDriver(config, new FrameScript {
            Width = 960, Height = 740,
            Markers = [new(60453, TravelFixture.Hub.World!.Value), new(60430, TravelFixture.Shard.World!.Value)]
        });
        var ipc = new FakeTravelIpc();
        var ui = driver.Context.Assistance = new MapAssistance(ipc.Provider(), new TravelFixture(driver), () => true,
            () => { });
        for (var i = 0; i < 5; i++)
            driver.Step(new(i * .02));
        if (scene == "travel-hover")
            ui.HoverPlace(TravelFixture.Shard, new(355, 280));
        else
            ui.Request(TravelFixture.Shard);
        driver.Step(new(.10));
        driver.Step(new(.12));
        driver.Save(output);
        Console.WriteLine(output);
        return true;
    }
}