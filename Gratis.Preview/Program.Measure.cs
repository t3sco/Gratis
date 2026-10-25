using System.Numerics;

namespace Gratis.Preview;

internal static partial class Program {
    private static bool TryMeasureScene(string[] args) {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || args[at + 1] is not ("measure-tool" or "measure-tool-circular"))
            return false;
        var scene = args[at + 1];
        var outAt = Array.IndexOf(args, "--out");
        var output = Path.GetFullPath(outAt < 0 ? Path.Combine("_preview", scene + ".png") : args[outAt + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var config = PaletteVerification.Config();
        config.MapCircle = scene.EndsWith("circular");
        config.WindowSize = config.MapCircle ? new(680) : new(820, 620);
        using var driver = new ScriptedFrameDriver(config, new FrameScript {
            Width = 960,
            Height = 800,
            Markers = [new(60460, new(-190, -80)), new(60436, new(160, 110))]
        });
        driver.Step(new(0));
        driver.Step(new(.02));
        var a = driver.Renderer.WorldToScreen(new(-190, -80), config.WindowPosition);
        var b = driver.Renderer.WorldToScreen(new(160, 110), config.WindowPosition);
        driver.Step(new(.04, Mouse: a, Left: true, Control: true));
        driver.Step(new(.06, Mouse: b));
        driver.Step(new(.08, Left: false, Control: false));
        driver.Step(new(.1, Mouse: new Vector2(-1000)));
        driver.Save(output);
        Console.WriteLine(output);
        return true;
    }
}