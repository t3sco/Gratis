using System.Numerics;
using System.Text.Json;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static partial class Program
{
    private static bool TryPaletteScene(string[] args)
    {
        if (TryToolbarSearchScene(args)) return true;
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || !args[at + 1].StartsWith("palette-", StringComparison.Ordinal)) return false;
        var scene = args[at + 1];
        if (scene is not ("palette-home" or "palette-expansion" or "palette-recent" or "palette-small" or "palette-scaled" or "palette-empty" or "palette-query-limsa" or "palette-unattuned" or "palette-scoped" or "palette-narrow" or "palette-circular" or "palette-glass-dark-recolour"))
            throw new ArgumentException("Unknown palette scene " + scene);
        var outputAt = Array.IndexOf(args, "--out");
        var output = Path.GetFullPath(outputAt < 0 ? Path.Combine("_preview", scene + ".png") : args[outputAt + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var config = PaletteVerification.Config();
        config.WindowSize = scene is "palette-narrow" or "palette-small" ? new(330, 480) : scene == "palette-circular" ? new(720) : new(820, 650);
        config.MapCircle = scene == "palette-circular";
        config.PaletteHintSeen = true;
        if (scene == "palette-scaled") config.ChromeScale = 1.5f;
        config.MapColour.Preset = scene == "palette-glass-dark-recolour" ? MapColourPreset.Midnight : MapColourPreset.Original;
        using var driver = new ScriptedFrameDriver(config, new FrameScript { Palette = true, Width = 960, Height = 800 });
        driver.Step(new(0)); driver.Step(new(.02));
        driver.Palette!.Open(scene switch { "palette-empty" => "no-such-place-zzzz", "palette-query-limsa" => "limsa", "palette-unattuned" => "a:summer", "palette-scoped" => "a:", _ => "" });
        driver.Step(new(.04));
        if (scene == "palette-expansion") driver.Palette.BrowseExpansion(0);
        if (scene == "palette-recent") {
            driver.Palette.Open("a:limsa"); driver.Step(new(.045)); driver.Step(new(.05));
            driver.Step(new(.055, Keys: [ImGuiNET.ImGuiKey.Enter])); driver.Palette.Open();
        }
        var frame = driver.Step(new(.06));
        driver.Save(output);
        File.WriteAllText(Path.ChangeExtension(output, ".state.json"), JsonSerializer.Serialize(new { Frame = frame,
            driver.Palette.PanelMin, driver.Palette.PanelMax, driver.Palette.BrowsedExpansion, driver.Palette.BrowserSelection,
            Imagery = "Deterministic illustrated placeholders. Game textures require a live check.", Results = driver.Palette.Results }, ScriptedFrameDriver.Json));
        Console.WriteLine($"{output}: shared palette, {driver.Palette.Results.Count} results, {config.WindowSize.X}x{config.WindowSize.Y}, circle={config.MapCircle}");
        return true;
    }
}
