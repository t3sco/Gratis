using System.Text.Json;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static partial class Program {
    private static bool TryToolbarSearchScene(string[] args) {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || !args[at + 1].StartsWith("toolbar-search-", StringComparison.Ordinal))
            return false;
        var scene = args[at + 1];
        if (scene is not ("toolbar-search-collapsed" or "toolbar-search-home" or "toolbar-search-results"
            or "toolbar-search-dividers" or "toolbar-search-narrow" or "toolbar-search-tooltip"))
            throw new ArgumentException("Unknown toolbar search scene " + scene);
        var outputAt = Array.IndexOf(args, "--out");
        var output = Path.GetFullPath(outputAt < 0 ? Path.Combine("_preview", scene + ".png") : args[outputAt + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var config = PaletteVerification.Config();
        config.PinChrome = true;
        config.WindowSize = scene == "toolbar-search-narrow" ? new(280, 580) : new(960, 720);
        using var driver = new ScriptedFrameDriver(config,
            new FrameScript { Palette = true, Width = 1040, Height = 800 });
        double time = 0;
        driver.Step(new(time));
        driver.Step(new(time += .04));
        if (scene is "toolbar-search-home" or "toolbar-search-results" or "toolbar-search-narrow") {
            ToolbarSearchVerification.Click(driver, MapToolbarDrawing.ButtonBounds[0], ref time);
            if (scene == "toolbar-search-results")
                driver.Step(new(time += .04, Text: "limsa"));
        }
        if (scene == "toolbar-search-tooltip") {
            driver.Step(new(time += .04, Mouse: ToolbarSearchVerification.Center(MapToolbarDrawing.ButtonBounds[5])));
            driver.Step(new(time += .45));
        }
        var frame = driver.Step(new(time += .04));
        driver.Save(output);
        File.WriteAllText(Path.ChangeExtension(output, ".state.json"), JsonSerializer.Serialize(new {
            Frame = frame,
            Toolbar = MapToolbarDrawing.LastBounds,
            Buttons = MapToolbarDrawing.ButtonBounds,
            Glyphs = MapToolbarDrawing.GlyphBounds,
            Dividers = MapToolbarDrawing.DividerBounds,
            driver.Palette!.FieldBounds,
            driver.Palette.PanelMin,
            driver.Palette.PanelMax,
            Tooltip = ChromeControls.LastTooltipBounds
        }, ScriptedFrameDriver.Json));
        Console.WriteLine(output);
        return true;
    }
}