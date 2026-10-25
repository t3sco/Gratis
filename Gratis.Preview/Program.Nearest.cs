namespace Gratis.Preview;

internal static partial class Program {
    private static bool TryNearestScene(string[] args) {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || args[at + 1] != "palette-nearest")
            return false;
        var outAt = Array.IndexOf(args, "--out");
        var output = Path.GetFullPath(outAt < 0 ? Path.Combine("_preview", "palette-nearest.png") : args[outAt + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var config = PaletteVerification.Config();
        config.WindowSize = new(820, 620);
        using var driver = new ScriptedFrameDriver(config, new FrameScript {
            Width = 960, Height = 800, Palette = true,
            Markers = [new(60460, new(95, 0)),
                new(60570, new(240, 130)),
                new(60460, new(-600, 0)),
                new(60436, new(-200, -150))]
        });
        driver.Step(new(0));
        driver.Step(new(.02));
        driver.Palette!.Open("nearest mrkt");
        driver.Step(new(.04));
        driver.Step(new(.06));
        driver.Save(output);
        Console.WriteLine(output);
        return true;
    }
}