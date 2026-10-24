using System.Numerics;

namespace Gratis.Preview;

internal static partial class Program
{
    private static bool TryContextMenuScene(string[] args)
    {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || args[at + 1] is not ("context-menu-map" or "context-menu-marker" or "context-menu-circular" or "context-menu-edge")) return false;
        var scene = args[at + 1]; var outAt = Array.IndexOf(args, "--out");
        var output = Path.GetFullPath(outAt < 0 ? Path.Combine("_preview", scene + ".png") : args[outAt + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var config = PaletteVerification.Config(); config.WindowSize = new(680, 640);
        config.MapCircle = scene == "context-menu-circular";
        using var driver = new ScriptedFrameDriver(config, new FrameScript { Width = 960, Height = 800,
            Markers = scene == "context-menu-marker" ? [new(71203, Vector2.Zero)] : [] });
        driver.Context.ContextMenu.Actions = new ContextMenuFixture(); PinVerification.Attach(driver);
        if (driver.Markers.Count > 0) driver.Markers[0].PrimaryText = () => "The next adventure";
        for (var i = 0; i < 5; i++) driver.Step(new(i * .02));
        var point = scene == "context-menu-marker" ? driver.Renderer.WorldToScreen(Vector2.Zero, driver.Window.MapDrawOffset) :
            config.WindowPosition + (scene == "context-menu-edge" ? config.WindowSize - new Vector2(12) : config.WindowSize / 2);
        driver.Step(new(.12, Mouse: point)); driver.Step(new(.14, Right: true)); driver.Step(new(.16, Right: false)); driver.Step(new(.18));
        driver.Save(output); Console.WriteLine(output); return true;
    }
}
