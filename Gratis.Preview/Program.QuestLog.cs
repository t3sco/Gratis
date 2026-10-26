using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static partial class Program {
    internal static SystemConfig QuestLogConfig(int width, int height) => new() {
        AcceptedSpoilerWarning = true, PinChrome = true, FollowPlayer = false,
        WindowPosition = new(16), WindowSize = new(width - 32, height - 32),
        QuestLogOpenWithMap = true, MapScale = .45f, ShowCoordinateBar = true,
    };

    private static bool TryQuestLogScene(string[] args) {
        var index = Array.IndexOf(args, "--scene");
        if (index < 0 || index + 1 >= args.Length || args[index + 1] is not ("quest-log" or "quest-log-circle"))
            return false;
        var sizeIndex = Array.IndexOf(args, "--size");
        var size = sizeIndex >= 0 ? args[sizeIndex + 1].Split('x') : ["1100", "700"];
        var width = int.Parse(size[0]);
        var height = int.Parse(size[1]);
        var outIndex = Array.IndexOf(args, "--out");
        var output = Path.GetFullPath(outIndex >= 0 ? args[outIndex + 1] : "_preview/quest-log.png");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var config = QuestLogConfig(width, height);
        config.MapCircle = args[index + 1] == "quest-log-circle";
        using var driver = new ScriptedFrameDriver(config, new FrameScript {
            Width = width, Height = height, QuestLog = true, Palette = true,
            Title = "Yok Tural - Tuliyollal",
        });
        for (var frame = 0; frame < 16; frame++)
            driver.Step(new(frame * .04));
        driver.Save(output);
        Console.WriteLine($"{output}: {width}x{height}, accepted={driver.Context.QuestLog.Results.Count}, "
            + $"progress={driver.Context.QuestSource!.Snapshot.Progress!.Percentage}%, "
            + $"compact={driver.Context.QuestLog.Layout.Compact}");
        return true;
    }
}
