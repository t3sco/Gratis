using System.Text.Json;

namespace Gratis.Preview;

internal static partial class Program {
    private static bool TryMoveHandleScene(string[] args) {
        var index = Array.IndexOf(args, "--scene");
        if (index < 0 || index + 1 >= args.Length
            || !args[index + 1].StartsWith("movehandle-", StringComparison.Ordinal))
            return false;
        var scene = args[index + 1];
        if (scene is not ("movehandle-hidden" or "movehandle-hover" or "movehandle-drag" or "movehandle-circular"))
            throw new ArgumentException($"Unknown move handle scene '{scene}'.");
        for (var argument = 0; argument < args.Length; argument += 2)
            if (argument + 1 >= args.Length || args[argument] is not ("--scene" or "--out"))
                throw new ArgumentException("Move handle scenes accept --scene <name> and optional --out <path.png>.");
        var outputIndex = Array.IndexOf(args, "--out");
        var output = Path.GetFullPath(outputIndex >= 0 ? args[outputIndex + 1]
            : Path.Combine("_preview", scene + ".png"));
        if (!Path.GetExtension(output).Equals(".png", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Move handle scene output must be a PNG.");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var config = MoveHandleVerification.Config();
        config.MapCircle = scene == "movehandle-circular";
        using var driver = new ScriptedFrameDriver(config);
        driver.Step(new(0));
        driver.Step(new(.02));
        driver.Step(new(2));
        var frame = driver.Step(new(2.2));
        if (scene != "movehandle-hidden") {
            driver.Step(new(2.22, Mouse: frame.HandleCenter));
            frame = driver.Step(new(2.36));
        }
        if (scene == "movehandle-drag") {
            driver.Step(new(2.38, Left: true));
            frame = driver.Step(new(2.4, Mouse: frame.HandleCenter + new System.Numerics.Vector2(60, 35)));
        }
        driver.Save(output);
        File.WriteAllText(Path.ChangeExtension(output, ".state.json"),
            JsonSerializer.Serialize(frame, ScriptedFrameDriver.Json));
        Console.WriteLine($"{output}: shared window frame, handle visibility={frame.HandleVisibility:0.00}, "
            + $"moving={frame.MovingWindow}, chrome={frame.ChromeProgress:0.00}");
        return true;
    }
}