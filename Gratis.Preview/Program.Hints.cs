using System.Numerics;
using Gratis.Classes;
using Gratis.Data;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static partial class Program {
    private const string HintsPlayerSecondary = "RDM\tLv. 100";

    private static bool TryHintsScene(string[] args) {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || args[at + 1] != "hints")
            return false;
        var outAt = Array.IndexOf(args, "--out");
        var directory = Path.GetFullPath(outAt < 0 ? Path.Combine("_preview", "hints") : args[outAt + 1]);
        Directory.CreateDirectory(directory);
        const int width = 960, height = 740;
        FrameScript Script(params ScriptMarker[] markers) => new() {
            Width = width, Height = height, Palette = true,
            Title = "Eorzea - La Noscea - Limsa Lominsa Lower Decks - Hawkers' Alley###GratisMapWindow",
            PlayerCoordinates = new(18.4f, 23.7f), CursorCoordinates = new(22.6f, 19.2f), Markers = [.. markers],
        };
        SystemConfig Config() {
            var config = ChromeLayoutVerification.Config(new(width - 64, height - 64));
            config.ReduceMotion = true;
            config.LocationAnchor = ChromeAnchor.TopLeft;
            config.PaletteHintSeen = true;
            config.AlwaysShowToolbar = true;
            config.MeasureTool = true;
            return config;
        }
        void Render(string name, FrameScript script, SystemConfig config, Action<ScriptedFrameDriver, Action<Vector2?, bool, bool>> run) {
            using var driver = new ScriptedFrameDriver(config, script);
            driver.PreserveWindowZOrder = true;
            double time = 0;
            void Step(Vector2? mouse, bool left, bool control) =>
                driver.Step(new(time += .04, Mouse: mouse ?? new(-1000), Left: left, Control: control));
            for (var i = 0; i < 16; i++) Step(null, false, false);
            run(driver, Step);
            driver.Save(Path.Combine(directory, name + ".png"));
            Console.WriteLine(Path.Combine(directory, name + ".png"));
        }
        Vector2 Center(Vector4 b) => new((b.X + b.Z) / 2, (b.Y + b.W) / 2);
        Render("chip", Script(), Config(), (driver, step) => {
            driver.Context.Visibility.Overrides[(int)MarkerGroup.Services] = MarkerVisibilityState.Hidden;
            step(Center(MapToolbarDrawing.ButtonBounds[2]), false, false);
            step(Center(MapToolbarDrawing.ButtonBounds[2]), true, false);
            step(Center(MapToolbarDrawing.ButtonBounds[2]), false, false);
            for (var i = 0; i < 6; i++) step(null, false, false);
            for (var i = 0; i < 16; i++) step(Center(FilterBarDrawing.ChipBounds[(int)MarkerGroup.Services]), false, false);
        });
        Render("toolbar", Script(), Config(), (driver, step) => {
            driver.Context.Visibility.Overrides[(int)MarkerGroup.Services] = MarkerVisibilityState.Hidden;
            for (var i = 0; i < 4; i++) step(null, false, false);
            for (var i = 0; i < 16; i++) step(Center(MapToolbarDrawing.ButtonBounds[2]), false, false);
        });
        Render("coords", Script(), Config(), (driver, step) => {
            for (var i = 0; i < 6; i++) step(Center(CoordinateCopy.CursorBounds), false, false);
        });
        var measure = Config();
        Render("measure", Script(new ScriptMarker(60460, new(-190, -80)), new ScriptMarker(60436, new(160, 110))), measure, (driver, step) => {
            var a = driver.Renderer.WorldToScreen(new(-190, -80), measure.WindowPosition);
            var b = driver.Renderer.WorldToScreen(new(160, 110), measure.WindowPosition);
            step(a, true, true);
            step(b, true, true);
            step(b, false, false);
            step(null, false, false);
            var chip = (driver.Context.Measure.ChipMin + driver.Context.Measure.ChipMax) / 2;
            for (var i = 0; i < 4; i++) step(chip, false, false);
        });
        var world = new Vector2(-40, 20);
        var player = Config();
        Render("player", Script(new ScriptMarker(60421, world, Source: MapMarkerSource.Party)), player, (driver, step) => {
            driver.Markers[0].PrimaryText = () => "Alisaie";
            driver.Markers[0].SecondaryText = () => HintsPlayerSecondary;
            var at = driver.Renderer.WorldToScreen(world, player.WindowPosition);
            for (var i = 0; i < 6; i++) step(at, false, false);
        });
        var cluster = Config();
        Render("cluster", Script(new ScriptMarker(60421, world, Source: MapMarkerSource.Party)), cluster, (driver, step) => {
            driver.Markers[0].PrimaryText = () => "6 ambient markers";
            driver.Markers[0].SecondaryText = () =>
                "Aethernet shard 1\nAethernet shard 2\nAethernet shard 3\n+3 more\nZoom in\tClick";
            var at = driver.Renderer.WorldToScreen(world, cluster.WindowPosition);
            for (var i = 0; i < 6; i++) step(at, false, false);
        });
        return true;
    }
}
