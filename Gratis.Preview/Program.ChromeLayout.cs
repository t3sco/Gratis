using System.Numerics;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static partial class Program
{
    private static bool TryChromeLayoutScene(string[] args)
    {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || args[at + 1] is not ("chrome-layout-small" or "chrome-layout-normal" or "chrome-layout-large" or "chrome-layout-anchors" or "chrome-quest-travel" or "filters-collapsed" or "filters-expanded" or "filters-small-collapsed" or "filters-small-expanded" or "filters-always-expanded" or "filters-small-always-expanded")) return false;
        var scene = args[at + 1];
        var outAt = Array.IndexOf(args, "--out");
        var output = Path.GetFullPath(outAt < 0 ? Path.Combine("_preview", scene + ".png") : args[outAt + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var size = scene.Contains("small") ? new Vector2(360, 280) : scene switch { "chrome-layout-small" => new Vector2(360, 280), "chrome-layout-large" => new Vector2(1280, 800), _ => new Vector2(780, 600) };
        var config = ChromeLayoutVerification.Config(size);
        if (scene.Contains("always-expanded")) config.FilterOptions.Mode = FilterBarMode.AlwaysExpanded;
        if (scene == "chrome-layout-anchors") ChromeLayoutVerification.SetAnchors(config, ChromeAnchor.TopRight);
        using var driver = new ScriptedFrameDriver(config, new FrameScript {
            Width = (int)size.X + 64, Height = (int)size.Y + 64, PlayerCoordinates = new(18.4f, 23.7f),
            Markers = [new(71203, new(120, 80), Objective: true), new(60453, new(-150, 20)), new(60412, new(-60, -100)), new(60501, new(-200, 150))],
        });
        if (scene is "chrome-quest-travel" or "chrome-layout-anchors") {
            var travel = driver.Context.Assistance = new MapAssistance(NullTravelProvider.Instance, new TravelFixture(driver), () => true, () => { });
            travel.OfferContextTarget(new(new(120, 80), 1, SpotlightSource.Quest), 0);
        }
        for (var i = 0; i < 12; i++) driver.Step(new(i * .02));
        if (scene.StartsWith("filters-", StringComparison.Ordinal)) {
            driver.Context.Visibility.Overrides[(int)MarkerGroup.Services] = MarkerVisibilityState.Hidden;
            driver.Step(new(.24));
            if (scene.EndsWith("-expanded", StringComparison.Ordinal) && !scene.Contains("always-expanded")) {
                var button = FilterBarDrawing.OverflowBounds;
                var mouse = new Vector2((button.X + button.Z) / 2, (button.Y + button.W) / 2);
                driver.Step(new(.26, Mouse: mouse)); driver.Step(new(.28, Left: true)); driver.Step(new(.30, Left: false));
                driver.Step(new(.32, Mouse: new(-1000)));
            }
        }
        driver.Save(output); Console.WriteLine(output); return true;
    }
}
