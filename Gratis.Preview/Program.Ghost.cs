using System.Numerics;
using System.Text.Json;

namespace Gratis.Preview;

internal static partial class Program {
    private static bool TryGhostScene(string[] args) {
        var index = Array.IndexOf(args, "--scene");
        if (index < 0 || index + 1 >= args.Length || args[index + 1] is not ("ghost-active" or "ghost-restoring"))
            return false;
        var scene = args[index + 1];
        var outputIndex = Array.IndexOf(args, "--out");
        var output = Path.GetFullPath(outputIndex >= 0 ? args[outputIndex + 1]
            : Path.Combine("_preview", scene + ".png"));
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        using var d = new ScriptedFrameDriver(GhostVerification.Config());
        d.Step(new(0, Focused: false, Moving: true));
        d.Step(new(.3, Moving: true));
        var frame = d.Step(new(.4, Moving: true));
        if (scene == "ghost-restoring") {
            d.Step(new(.41, Mouse: new Vector2(350, 250), Moving: true));
            frame = d.Step(new(.47, Moving: true));
        }
        d.Save(output);
        File.WriteAllText(Path.ChangeExtension(output, ".state.json"),
            JsonSerializer.Serialize(frame, ScriptedFrameDriver.Json));
        Console.WriteLine($"{output}: {d.Window.Ghost.Diagnostics()}");
        return true;
    }
}