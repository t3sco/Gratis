using System.Text.Json;

namespace Gratis.Preview;

internal static partial class Program {
    private static (string Name, Action Run)[] Selftests() => [
        ("visual-baseline", () => { if (VisualBaseline(update: false) != 0)
            throw new InvalidOperationException("Visual baseline changed; inspect visual-diff artifacts."); }),
        ("frames-viewport-cover", ViewportCoverVerification.Run),
        ("frames-auto-fit", AutoFitVerification.Run),
        ("frames-pvp", PvpVerification.Frames),
        ("interface-motion", InterfaceMotionVerification.Maths),
        ("interface-scroll", InterfaceMotionVerification.Scroll),
        ("interface-motion-config", InterfaceMotionVerification.Config),
        ("frames-interface-motion", InterfaceMotionVerification.Frames),
        ("frames-unfade", InterfaceMotionVerification.Unfade),
        ("corner-radii", RadiusVerification.Run),
        ("action-buttons", ActionButtonVerification.Run),
        ("segmented-chips", SegmentedChipVerification.Run),
        ("menu-rows", MenuRowVerification.Run),
        ("popup-surfaces", PopupSurfaceVerification.Run),
        ("text-fields", TextFieldVerification.Run),
        ("chrome-three", ChromeThreeVerification.State),
        ("frames-bottom-cards", ChromeThreeVerification.BottomRow),
        ("frames-location-menu", LocationMenuVerification.Run),
        ("quest-log-model", QuestLogVerification.Model),
        ("performance-caches", PerformanceCacheVerification.Run),
        ("frames-quest-log", QuestLogVerification.Frames),
        ("frames-quest-log-phase1", QuestLogPhaseOneVerification.Run),
        ("frames-travel-input", ChromeThreeVerification.TravelInput),
        ("window-presets", WindowPresetVerification.State),
        ("frames-window-presets", WindowPresetVerification.Frames),
        ("tweaks", TweakVerification.Run),
        ("arrow-smoothing", ArrowSmoothingVerification.Run),
        ("camera", CameraVerification.Run),
        ("report", ReportVerification.Run),
        ("small-instance", SmallInstanceVerification.Run),
        ("font-safety", FontSafetyVerification.Run),
        ("quest-glyphs", QuestGlyphVerification.Run),
        ("marker-icon-fallback", MarkerFallbackVerification.Run),
        ("chrome-layout", ChromeLayoutVerification.Layout),
        ("label-layout", LabelLayoutVerification.Run),
        ("frames-update-notice", UpdateNoticeVerification.Run),
        ("frames-stacked-hover", StackedHoverVerification.Run),
        ("frames-hover-opacity", ChromeLayoutVerification.HoverFade),
        ("frames-quest-travel", ChromeLayoutVerification.QuestTravel),
        ("chrome-corner-clip", ChromeCornerVerification.Run),
        ("pan-inertia", InteractionLegibilityVerification.Inertia),
        ("programmatic-bounds", InteractionLegibilityVerification.Bounds),
        ("text-pixels", InteractionLegibilityVerification.Text),
        ("imgui-errors", ImGuiErrorVerification.Run),
        ("aetheryte-attunement", AttunementVerification.State), ("frames-attunement", AttunementVerification.Frames),
        ("measure-tool", MeasureVerification.Maths), ("frames-measure", MeasureVerification.Frames),
        ("main-story", StoryVerification.Selection), ("frames-main-story", StoryVerification.Frames),
        ("place-chips", PlaceChipVerification.State), ("frames-place-chips", PlaceChipVerification.Frames),
        ("ghost-mode", GhostVerification.State), ("frames-ghost", GhostVerification.Frames),
        ("nearest-queries", NearestVerification.Ranking), ("frames-nearest", NearestVerification.Frames),
        ("flag-arrival", FlagArrivalVerification.State), ("frames-flag-arrival", FlagArrivalVerification.Frames),
        ("frames-context-menu", ContextMenuVerification.Frames),
        ("frames-context-nearest", ContextMenuVerification.Nearest),
        ("coordinate-conversions", CoordinateVerification.Run),
            ("frames-coordinate-copy", CoordinateVerification.Frames),
        ("edge-pointers", EdgePointerVerification.Maths), ("frames-edge-pointers", EdgePointerVerification.Frames),
        ("map-view-memory", MapViewMemoryVerification.State),
            ("frames-map-view-memory", MapViewMemoryVerification.Frames),
        ("travel-provider", TravelVerification.Provider), ("frames-travel", TravelVerification.Frames),
        ("aetheryte-selection", AetheryteVerification.Selection), ("frames-aetheryte", AetheryteVerification.Frames),
        ("pin-share-codes", PinVerification.ShareCodes),
            ("pin-persistence", PinVerification.Persistence),
            ("frames-pins", PinVerification.Frames),
        ("diff", ImageDiff.Verify), ("live", VerifyLiveScene),
        ("palette-ranking", PaletteVerification.Ranking), ("palette-index", PaletteVerification.Index),
        ("frames-palette", PaletteVerification.Frames),
        ("palette-browser", PaletteBrowserVerification.Run),
        ("heading", HeadingVerification.Run), ("marker-classification", MarkerClassificationVerification.Run),
        ("markers", MarkerDeclutterVerification.Run), ("settings", SettingsVerification.Run),
        ("marker-visibility", MarkerVisibilityVerification.Run),
        ("frames-filters", FilterBarVerification.Run),
        ("config", ConfigVerification.Run), ("viewport", ViewportVerification.Run),
        ("zone", ZoneSwitchVerification.Run), ("spotlight-fit", SpotlightVerification.Fit),
        ("frames-spotlight", SpotlightVerification.Frames),
        ("motion", MotionVerification.Run), ("follow", FollowVerification.Run),
        ("motion-timeline", ViewportMotionVerification.Run), ("map-colour", MapColourVerification.Run),
        ("chrome", () => Run(Options.Parse(["--scene", "chrome-hidden"])!, chromeSelftest: true)),
        ("drawing", () => Run(Options.Parse(["--scene", "map-default", "--verify"])!)),
        ("frames-autohide", FrameRegressionVerification.AutoHide),
        ("frames-zoom", FrameRegressionVerification.Zoom), ("frames-follow", FrameRegressionVerification.Follow),
        ("frames-pan-return", FrameRegressionVerification.PanReturn),
            ("frames-glass", FrameRegressionVerification.Glass),
        ("frames-markers", FrameRegressionVerification.Markers),
            ("frames-allocations", FrameRegressionVerification.Allocations),
        ("frames-instrumentation", FrameRegressionVerification.Instrumentation),
        ("movehandle", MoveHandleVerification.Maths), ("frames-movehandle", MoveHandleVerification.Frames),
    ];

    private static int RunSelftests(string selected) {
        if (selected is "imgui-error-injected" or "imgui-assert-injected") {
            ImGuiErrorVerification.Inject(selected == "imgui-assert-injected");
            ImGuiErrors.ThrowIfNewErrors(0);
            return 0;
        }
        var tests = Selftests().Where(test => selected == "all" || test.Name == selected
            || selected == "frames" && test.Name.StartsWith("frames-", StringComparison.Ordinal)).ToArray();
        if (tests.Length == 0)
            throw new ArgumentException($"Unknown selftest '{selected}'. Choose all, frames, or "
                + $"{string.Join(", ", Selftests().Select(test => test.Name))}.");
        var results = new List<(string Name, string Result)>();
        foreach (var test in tests) {
            var errorsBefore = ImGuiErrors.Count;
            try {
                test.Run();
                ImGuiErrors.ThrowIfNewErrors(errorsBefore);
                results.Add((test.Name, "PASS"));
            }
            catch (Exception error) { results.Add((test.Name, "FAIL: " + error.Message)); }
        }
        Console.WriteLine("Selftest                   Result");
        Console.WriteLine("-------------------------  ------");
        foreach (var result in results)
            Console.WriteLine($"{result.Name,-25}  {result.Result}");
        var failed = results.Count(result => result.Result != "PASS");
        Console.WriteLine($"{results.Count - failed}/{results.Count} passed; {failed} failed.");
        return failed == 0 ? 0 : 1;
    }

    private static bool TryFrameScript(string[] args) {
        if (args.Length == 0 || args[0] != "--script")
            return false;
        if (args.Length is not (2 or 4) || args.Length == 4 && args[2] != "--out")
            throw new ArgumentException("Usage: --script timeline.json [--out directory]");
        var scriptPath = Path.GetFullPath(args[1]);
        var script = JsonSerializer.Deserialize<FrameScript>(File.ReadAllText(scriptPath), ScriptedFrameDriver.Json)
            ?? throw new ArgumentException("Empty script.");
        if (script.Frames.Count == 0)
            throw new ArgumentException("Script must contain Frames.");
        var directory = Path.GetDirectoryName(scriptPath)!;
        if (script.Config is not null)
            script.Config = Path.GetFullPath(script.Config, directory);
        if (script.Texture is not null)
            script.Texture = Path.GetFullPath(script.Texture, directory);
        var config = script.Config is null ? null
            : JsonSerializer.Deserialize<Gratis.Data.SystemConfig>(File.ReadAllText(script.Config),
                ScriptedFrameDriver.Json)
            ?? throw new ArgumentException("Empty config.");
        var output = Path.GetFullPath(args.Length == 4 ? args[3] : Path.Combine("_preview", "frames"));
        foreach (var frame in script.Frames)
            if (frame.Screenshot is { } filename
                && !Path.GetExtension(filename).Equals(".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Script screenshot names must end in .png.");
        if (script.Config is not null
            && Path.Combine(output, "snapshots.json").Equals(script.Config, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Script snapshots must not overwrite the saved config.");
        Directory.CreateDirectory(output);
        using var driver = new ScriptedFrameDriver(config, script);
        var snapshots = new List<FrameSnapshot>();
        foreach (var frame in script.Frames) {
            snapshots.Add(driver.Step(frame));
            if (frame.Screenshot is { } filename)
                driver.Save(Path.Combine(output, Path.GetFileName(filename)));
        }
        File.WriteAllText(Path.Combine(output, "snapshots.json"),
            JsonSerializer.Serialize(snapshots, ScriptedFrameDriver.Json));
        Console.WriteLine($"Executed {snapshots.Count} shared MapWindow/MapRenderer frames; snapshots: "
            + $"{Path.Combine(output, "snapshots.json")}");
        return true;
    }
}
