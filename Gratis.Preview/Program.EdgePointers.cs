using Gratis.Rendering;

namespace Gratis.Preview;

internal static partial class Program
{
    private static bool TryEdgePointerScene(string[] args)
    {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || args[at + 1] is not ("edge-pointers" or "edge-pointers-circular" or "edge-pointers-crowded")) return false;
        var scene = args[at + 1]; var outAt = Array.IndexOf(args, "--out");
        var output = Path.GetFullPath(outAt < 0 ? Path.Combine("_preview", scene + ".png") : args[outAt + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var config = PaletteVerification.Config(); config.MapCircle = scene == "edge-pointers-circular";
        config.WindowSize = config.MapCircle ? new(680) : new(820, 620);
        using var driver = new ScriptedFrameDriver(config, EdgePointerVerification.Scene(scene == "edge-pointers-crowded"));
        for (var i = 0; i < 8; i++) driver.Step(new(i * .02));
        driver.Save(output); Console.WriteLine(output); return true;
    }
}
