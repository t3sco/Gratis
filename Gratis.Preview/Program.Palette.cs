using System.Text.Json;
using System.Numerics;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static partial class Program {
    private static bool TryPaletteScene(string[] args) {
        if (TryToolbarSearchScene(args))
            return true;
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || !args[at + 1].StartsWith("palette-", StringComparison.Ordinal))
            return false;
        var scene = args[at + 1];
        if (scene is not ("palette-home" or "palette-expansion" or "palette-recent" or "palette-small"
            or "palette-scaled" or "palette-empty" or "palette-query-limsa" or "palette-unattuned" or "palette-scoped"
            or "palette-narrow" or "palette-circular" or "palette-glass-dark-recolour"
            or "palette-aetherytes-browse" or "palette-zones-browse"
            or "palette-aetherytes-zones" or "palette-zones-zones" or "palette-quests" or "palette-quests-narrow"
            or "palette-quests-scaled" or "palette-long-name"))
            throw new ArgumentException("Unknown palette scene " + scene);
        var outputAt = Array.IndexOf(args, "--out");
        var output = Path.GetFullPath(outputAt < 0 ? Path.Combine("_preview", scene + ".png") : args[outputAt + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var config = PaletteVerification.Config();
        config.WindowSize = scene is "palette-narrow" or "palette-small" or "palette-quests-narrow"
            or "palette-quests-scaled" or "palette-long-name" ? new(330, 480) : scene == "palette-circular"
            ? new(720) : new(820, 650);
        config.MapCircle = scene == "palette-circular";
        config.PaletteHintSeen = true;
        if (scene == "palette-scaled")
            config.ChromeScale = 1.5f;
        if (scene == "palette-quests-scaled")
            config.ChromeScale = 1.6f;
        config.MapColour.Preset = scene == "palette-glass-dark-recolour" ? MapColourPreset.Midnight
            : MapColourPreset.Original;
        using var driver = new ScriptedFrameDriver(config,
            new FrameScript { Palette = true, Width = 960, Height = 800 });
        if (scene is "palette-quests" or "palette-quests-narrow" or "palette-quests-scaled")
            driver.PlaceSource!.AdditionalPlaces.AddRange(PaletteFixture.QuestPlaces);
        if (scene == "palette-long-name") {
            const string name = "The observation deck above the northern reaches of Limsa Lominsa";
            driver.PlaceSource!.AdditionalPlaces.AddRange([
                new("long:1", Gratis.Controllers.PlaceKind.Marker, name, "La Noscea > Limsa Lominsa Lower Decks",
                    1, 1, new(10, 20), Aliases: "longname"),
                new("long:2", Gratis.Controllers.PlaceKind.Marker, name, "La Noscea > Limsa Lominsa Lower Decks",
                    1, 1, new(20, 10), Aliases: "longname"),
                new("long:3", Gratis.Controllers.PlaceKind.Zone, name, "La Noscea > Limsa Lominsa Lower Decks",
                    99, 1, Aliases: "longname")
            ]);
        }
        driver.Step(new(0));
        driver.Step(new(.02));
        driver.Palette!.Open(scene switch {
            "palette-long-name" => "longname",
            "palette-empty" => "no-such-place-zzzz",
            "palette-query-limsa" => "limsa",
            "palette-unattuned" => "a:summer",
            "palette-scoped" => "a:",
            _ => ""
        });
        driver.Step(new(.04));
        var time = .04;
        if (scene is "palette-aetherytes-browse" or "palette-zones-browse"
            or "palette-aetherytes-zones" or "palette-zones-zones" or "palette-quests" or "palette-quests-narrow"
            or "palette-quests-scaled") {
            var tabs = scene.StartsWith("palette-quests", StringComparison.Ordinal) ? 3
                : scene.StartsWith("palette-zones", StringComparison.Ordinal) ? 2 : 1;
            for (var tab = 0; tab < tabs; tab++) {
                driver.Step(new(time += .02, Keys: [ImGuiNET.ImGuiKey.Tab]));
                driver.Step(new(time += .02));
            }
            if (scene.EndsWith("-zones", StringComparison.Ordinal)) {
                driver.Step(new(time += .02, Keys: [ImGuiNET.ImGuiKey.Enter]));
                driver.Step(new(time += .02));
                driver.Step(new(time += .02, Keys: [ImGuiNET.ImGuiKey.RightArrow]));
                driver.Step(new(time += .02));
            }
        }
        if (scene == "palette-expansion")
            driver.Palette.BrowseExpansion(0);
        if (scene == "palette-recent") {
            driver.Palette.Open("a:limsa");
            driver.Step(new(.045));
            driver.Step(new(.05));
            driver.Step(new(.055, Keys: [ImGuiNET.ImGuiKey.Enter]));
            driver.Palette.Open();
        }
        var frame = driver.Step(new(Math.Max(.06, time + .02)));
        time = frame.Time;
        if (args.Contains("--settle") || args.Contains("--state"))
            for (var i = 0; i < 12; i++) frame = driver.Step(new(time += .04));
        var stateAt = Array.IndexOf(args, "--state");
        if (stateAt >= 0) {
            var state = args[stateAt + 1];
            var targetAt = Array.IndexOf(args, "--target");
            var target = targetAt < 0 ? "row" : args[targetAt + 1];
            var id = target == "quest-filter" ? "##quest-bucket-1" : "##scope-1";
            var bounds = Vector4.Zero;
            var previousProbe = ChromeControls.PreviewAfterButton;
            try {
                ChromeControls.PreviewAfterButton = candidate => {
                    previousProbe?.Invoke(candidate);
                    if (candidate == id) {
                        var a = ImGuiNET.ImGui.GetItemRectMin(); var b = ImGuiNET.ImGui.GetItemRectMax();
                        bounds = new(a.X, a.Y, b.X, b.Y);
                    }
                };
                driver.Step(new(time += .04));
                if (target != "row" && state == "focused") FocusGalleryButton(driver, id, ref time);
                else if (state is "hover" or "pressed") {
                    var mouse = target == "row" ? driver.Palette.FirstResultCenter : CenterBounds(bounds);
                    if (mouse == Vector2.Zero) throw new InvalidOperationException("Palette target missing.");
                    for (var i = 0; i < 6; i++) driver.Step(new(time += .04, Mouse: mouse));
                    if (state == "pressed") driver.Step(new(time += .04, Mouse: mouse, Left: true));
                }
            }
            finally { ChromeControls.PreviewAfterButton = previousProbe; }
        }
        driver.Save(output);
        File.WriteAllText(Path.ChangeExtension(output, ".state.json"), JsonSerializer.Serialize(new {
            Frame = frame,
            driver.Palette.PanelMin,
            driver.Palette.PanelMax,
            driver.Palette.BrowsedExpansion,
            driver.Palette.BrowserSelection,
            Imagery = "Deterministic illustrated placeholders. Game textures require a live check.",
            Results = driver.Palette.Results
        }, ScriptedFrameDriver.Json));
        Console.WriteLine($"{output}: shared palette, {driver.Palette.Results.Count} results, "
            + $"{config.WindowSize.X}x{config.WindowSize.Y}, circle={config.MapCircle}");
        return true;
    }
}
