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
        if (index < 0 || index + 1 >= args.Length || args[index + 1] is not ("quest-log" or "quest-log-circle" or "quest-log-detail" or "quest-log-search"))
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
        var variantIndex = Array.IndexOf(args, "--variant");
        var variant = variantIndex >= 0 ? args[variantIndex + 1] : "default";
        if (args[index + 1] == "quest-log-detail")
            SummaryFixture.Apply((QuestLogFixture)driver.Context.QuestSource!, variant);
        if (variant == "available-empty") {
            var fixture = (QuestLogFixture)driver.Context.QuestSource!;
            fixture.Snapshot = fixture.Snapshot with { Revision = fixture.Snapshot.Revision + 1, Unaccepted = [] };
        }
        for (var frame = 0; frame < 16; frame++)
            driver.Step(new(frame * .04));
        var time = .64;
        void Click(Vector4 bounds) {
            var mouse = new Vector2((bounds.X + bounds.Z) / 2, (bounds.Y + bounds.W) / 2);
            driver.Step(new(time += .04, Mouse: mouse, Left: true));
            driver.Step(new(time += .04, Mouse: mouse, Left: false));
        }
        var panel = driver.Context.QuestLog;
        if (variant is "available" or "available-empty") Click(panel.TabBounds[2]);
        if (args[index + 1] == "quest-log-detail")
            Click(panel.Rows.First().Bounds);
        if (args[index + 1] == "quest-log-search" || args.Contains("--search"))
            Click(panel.SearchToggleBounds);
        for (var frame = 0; frame < 16; frame++)
            driver.Step(new(time += .04, Mouse: new(-1000)));
        if (variant is "tooltip" or "menu") {
            var bounds = panel.RewardBounds.First();
            var mouse = new Vector2((bounds.X + bounds.Z) / 2, (bounds.Y + bounds.W) / 2);
            for (var frame = 0; frame < 18; frame++)
                driver.Step(new(time += .04, Mouse: mouse));
            if (variant == "menu") {
                driver.Step(new(time += .04, Mouse: mouse, Right: true));
                for (var frame = 0; frame < 12; frame++)
                    driver.Step(new(time += .04, Mouse: mouse, Right: false));
            }
        }
        var timeIndex = Array.IndexOf(args, "--time");
        if (timeIndex >= 0)
            driver.Step(new(Math.Max(time + .04, double.Parse(args[timeIndex + 1], global::System.Globalization.CultureInfo.InvariantCulture))));
        var scrollIndex = Array.IndexOf(args, "--scroll");
        if (scrollIndex >= 0) {
            var b = panel.DetailBounds;
            var mouse = new Vector2((b.X + b.Z) / 2, (b.Y + b.W) / 2);
            driver.Step(new(time += .04, Mouse: mouse, Wheel: float.Parse(args[scrollIndex + 1])));
            for (var frame = 0; frame < 16; frame++) driver.Step(new(time += .04, Mouse: mouse));
        }
        driver.Save(output);
        Console.WriteLine($"{output}: {width}x{height}, accepted={driver.Context.QuestLog.Results.Count}, "
            + $"progress={driver.Context.QuestSource!.Snapshot.Progress!.Percentage}%, "
            + $"compact={driver.Context.QuestLog.Layout.Compact}");
        return true;
    }
}
