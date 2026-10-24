using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static partial class Program
{
    private static bool IsLensScene(string scene) => scene is "lens-explore" or "lens-quests" or "lens-minimal";
    private static FrameScript LensFixture() => new() {
        Width = 1280, Height = 800,
        Markers = [
            new(71001, new Vector2(-480, -260)), new(71021, new Vector2(-190, -240)), new(71141, new Vector2(70, -200)),
            new(71025, new Vector2(190, 150)), new(60501, new Vector2(410, -240)),
            new(60453, new Vector2(-360, 20)), new(60452, new Vector2(-40, 190)), new(60446, new Vector2(390, 230)),
            new(60412, new Vector2(-150, -40)), new(60434, new Vector2(-110, -20)), new(60460, new Vector2(-70, -60)),
            new(60427, new Vector2(230, -70)), new(60437, new Vector2(410, 30)), new(63000, new Vector2(480, 110)),
            new(60003, new Vector2(-480, 240)), new(60421, new Vector2(130, 270)), new(60450, new Vector2(60, -100)),
        ],
    };
    private static void RenderLensScene(Options options)
    {
        var script = LensFixture();
        script.Width = options.Width; script.Height = options.Height;
        var config = new SystemConfig { AcceptedSpoilerWarning = true, FollowPlayer = false, PinChrome = true,
            WindowPosition = new Vector2(32), WindowSize = new Vector2(Math.Min(options.Width - 64, 960), Math.Min(options.Height - 64, 640)),
            ActiveLens = options.Scene == "lens-quests" ? "Quests" : options.Scene == "lens-minimal" ? "Minimal" : "Explore",
            MapCircle = options.Circle ?? false, ReduceMotion = true, MapScale = .5f };
        using var driver = new ScriptedFrameDriver(config, script);
        for (var i = 0; i < 4; i++) driver.Step(new ScriptFrame(.1 + i * .1));
        driver.Save(options.Output);
        Console.WriteLine($"{options.Scene}: {options.Output}; lens={config.ActiveLens}; counts={string.Join(',', driver.Context.Visibility.Counts)}");
    }
}
