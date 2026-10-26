using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Gratis.Rendering;
using SkiaSharp;

namespace Gratis.Preview;

internal static partial class Program {
    private sealed record VisualCase(string Name, string Scene, string[] Args, string Size);
    private sealed record VisualReference(string Name, string Scene, string[] Args, string Size, string Sha256);
    private sealed record VisualManifest(int Version, VisualReference[] Scenes);
    private sealed record VisualCapture(string Path, Dictionary<string, Vector4> Regions);
    private static Dictionary<string, VisualCapture>? visualCaptures;
    private static string BaselineRoot => Path.Combine(Options.ProjectRoot(), "Gratis.Preview", "baselines");

    private static VisualCase[] VisualCases() {
        var cases = new List<VisualCase>();
        void Add(string scene, string size = "960x740", params string[] args) =>
            cases.Add(new(scene + (args.Length == 0 ? "" : "-" + args[^1]), scene, args, size));
        foreach (var scene in new[] { "map-default", "map-circular", "hover-tooltip", "markers", "toolbar",
            "glass-on", "glass-off", "glass-toolbar", "glass-card", "glass-tinted", "glass-unavailable", "glass-circle" }) {
            Add(scene);
            Add(scene, "960x740", "--style", "classic");
        }
        foreach (var scene in new[] { "library-toolbar", "library-toolbar-hover", "library-bottom-cards", "library-move" }) {
            Add(scene);
            Add(scene, "960x740", "--style", "classic");
        }
        foreach (var scene in new[] { "library-location", "library-feedback", "library-update", "library-teleport",
            "library-teleport-disabled", "library-popup", "library-popup-exit", "library-location-exit",
            "library-feedback-exit", "library-filters", "library-filters-quiet", "library-filters-hidden",
            "library-filters-solo", "library-filters-loud", "library-ghost" }) Add(scene);
        foreach (var scene in new[] { "palette-home", "palette-query-limsa", "palette-quests", "palette-empty" })
            cases.Add(new(scene, scene, ["--settle"], "960x800"));
        Add("context-menu-map", "960x800");
        Add("pin-editor", "960x800");
        foreach (var scene in new[] { "quest-log", "quest-log-detail", "quest-log-search" }) Add(scene, "1100x700");
        Add("quest-log", "1100x700", "--variant", "completed");
        Add("quest-log", "1100x700", "--variant", "available");
        Add("quest-log", "1100x700", "--variant", "available-empty");
        Add("quest-log-detail", "1100x700", "--variant", "tooltip");
        Add("quest-log-detail", "1100x700", "--variant", "menu");
        Add("quest-log-detail", "1100x700", "--variant", "completed");
        foreach (var scene in new[] { "settings-appearance", "settings-map", "settings-markers", "settings-chrome",
            "settings-tools", "settings-advanced", "settings-filters", "tweaks-page", "settings-search" }) {
            Add(scene, "1000x800");
            Add(scene, "1000x800", "--style", "classic");
        }
        Add("ui-gallery", "1800x1");
        Add("ui-gallery-classic", "1800x1");
        return cases.ToArray();
    }

    private static int VisualBaseline(bool update) {
        var timer = Stopwatch.StartNew();
        var cases = VisualCases();
        var manifestPath = Path.Combine(BaselineRoot, "visual-baseline.json");
        var work = Path.Combine(Options.ProjectRoot(), "_artifacts", "visual-baseline");
        var previousCaptures = visualCaptures;
        try {
            VisualReference[] Capture(string pass) {
                visualCaptures = new(StringComparer.Ordinal);
                return cases.Select(item => {
                    var sceneTimer = Stopwatch.StartNew();
                    var capture = RenderVisualCase(item, Path.Combine(work, pass));
                    using var bitmap = SKBitmap.Decode(capture.Path)
                        ?? throw new InvalidDataException($"Invalid PNG: {item.Name}");
                    var size = $"{bitmap.Width}x{bitmap.Height}";
                    if (!item.Scene.StartsWith("ui-gallery", StringComparison.Ordinal) && size != item.Size)
                        throw new InvalidOperationException($"{item.Name}: requested {item.Size}, rendered {size}.");
                    if (sceneTimer.Elapsed.TotalSeconds > 2)
                        Console.WriteLine($"{item.Name}: {sceneTimer.Elapsed.TotalSeconds:F2}s");
                    return new VisualReference(item.Name, item.Scene, item.Args, size, PngHash(capture.Path));
                }).ToArray();
            }
            if (update) {
                var first = Capture("update-a");
                var second = Capture("update-b");
                var unstable = first.Zip(second).Where(pair => pair.First.Sha256 != pair.Second.Sha256).ToArray();
                foreach (var pair in unstable) {
                    WriteVisualDiff(pair.First.Name, Path.Combine(work, "update-b", pair.First.Name + ".png"),
                        Path.Combine(work, "update-a", pair.First.Name + ".png"));
                    Console.Error.WriteLine($"NONDETERMINISTIC: {pair.First.Name}");
                }
                if (unstable.Length != 0)
                    throw new InvalidOperationException("Two complete passes differ; references were not updated.");
                Directory.CreateDirectory(Path.Combine(BaselineRoot, "images"));
                foreach (var item in second)
                    File.Copy(Path.Combine(work, "update-b", item.Name + ".png"),
                        Path.Combine(BaselineRoot, "images", item.Name + ".png"), true);
                File.WriteAllText(manifestPath, JsonSerializer.Serialize(new VisualManifest(1, second),
                    ScriptedFrameDriver.Json) + Environment.NewLine);
                Console.WriteLine($"Visual baseline updated: {second.Length} SHA256 hashes; two identical complete "
                    + $"passes in {timer.Elapsed.TotalSeconds:F2}s. References remain uncommitted.");
                return 0;
            }
            var manifest = JsonSerializer.Deserialize<VisualManifest>(File.ReadAllText(manifestPath),
                ScriptedFrameDriver.Json) ?? throw new InvalidDataException("Empty visual manifest.");
            if (manifest.Version != 1 || !manifest.Scenes.Select(x => x.Name).SequenceEqual(cases.Select(x => x.Name)))
                throw new InvalidDataException("Visual case list differs from the manifest; run --visual-baseline update.");
            var actual = Capture("actual");
            var failed = 0;
            foreach (var (expected, rendered) in manifest.Scenes.Zip(actual)) {
                var referencePath = Path.Combine(BaselineRoot, "images", expected.Name + ".png");
                if (expected.Scene != rendered.Scene || !expected.Args.SequenceEqual(rendered.Args))
                    throw new InvalidDataException($"Manifest arguments changed: {expected.Name}.");
                var haveReference = File.Exists(referencePath);
                if (haveReference && PngHash(referencePath) != expected.Sha256)
                    throw new InvalidDataException($"Reference PNG is corrupt: {expected.Name}.");
                if (expected.Sha256 == rendered.Sha256 && expected.Size == rendered.Size) continue;
                failed++;
                var diff = haveReference
                    ? WriteVisualDiff(expected.Name, visualCaptures![expected.Name].Path, referencePath)
                    : $"no reference image; run --visual-baseline update on the previous commit to create one";
                Console.Error.WriteLine($"CHANGED: {expected.Name} ({expected.Scene} {string.Join(' ', expected.Args)})"
                    + $"\n  expected {expected.Sha256}\n  actual   {rendered.Sha256}\n  {diff}");
            }
            Console.WriteLine($"Visual baseline: {actual.Length - failed}/{actual.Length} hashes match; "
                + $"{timer.Elapsed.TotalSeconds:F2}s.");
            return failed == 0 ? 0 : 1;
        }
        finally { visualCaptures = previousCaptures; }
    }

    private static string PngHash(string path) {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string WriteVisualDiff(string name, string actual, string expected) {
        var directory = Path.Combine(Options.ProjectRoot(), "_artifacts", name, "visual-diff");
        Directory.CreateDirectory(directory);
        var a = Path.Combine(directory, "actual.png");
        var b = Path.Combine(directory, "expected.png");
        File.Copy(actual, a, true);
        File.Copy(expected, b, true);
        var diff = Path.Combine(directory, "diff.png");
        using var actualImage = SKBitmap.Decode(a);
        using var expectedImage = SKBitmap.Decode(b);
        if (actualImage.Width == expectedImage.Width && actualImage.Height == expectedImage.Height)
            ImageDiff.Run(a, b, diff, false);
        else {
            var width = Math.Max(actualImage.Width, expectedImage.Width);
            var height = Math.Max(actualImage.Height, expectedImage.Height);
            string Pad(SKBitmap source, string filename) {
                using var padded = new SKBitmap(width, height);
                using var canvas = new SKCanvas(padded);
                canvas.Clear(SKColors.Magenta);
                canvas.DrawBitmap(source, 0, 0);
                SaveGalleryBitmap(padded, filename);
                return filename;
            }
            ImageDiff.Run(Pad(actualImage, Path.Combine(directory, "actual-padded.png")),
                Pad(expectedImage, Path.Combine(directory, "expected-padded.png")), diff, false);
        }
        return directory;
    }

    private static VisualCapture RenderVisualCase(VisualCase item, string directory) {
        if (visualCaptures?.TryGetValue(item.Name, out var cached) == true) return cached;
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, item.Name + ".png");
        var regions = new Dictionary<string, Vector4>(StringComparer.Ordinal);
        var previousObserver = ScriptedFrameDriver.SavedFrameObserver;
        var previousButtonObserver = ChromeControls.PreviewAfterButton;
        var previousContext = MapDrawContext.Current;
        var previousNative = ImGuiNET.ImGui.GetCurrentContext();
        var errors = ImGuiErrors.Count;
        var previousOut = Console.Out;
        try {
            ScriptedFrameDriver.SavedFrameObserver = driver => CaptureGalleryRegions(driver, regions);
            ChromeControls.PreviewAfterButton = id => {
                var min = ImGuiNET.ImGui.GetItemRectMin();
                var max = ImGuiNET.ImGui.GetItemRectMax();
                regions["button:" + id] = new(min.X, min.Y, max.X, max.Y);
            };
            Console.SetOut(TextWriter.Null);
            var args = new[] { "--scene", item.Scene, "--out", path, "--size", item.Size }.Concat(item.Args).ToArray();
            if (item.Scene.StartsWith("settings-", StringComparison.Ordinal)
                || item.Scene == "tweaks-page")
                RenderSettings(Options.Parse(args)!, (layout, _, _) => {
                    regions["settings-nav"] = layout.TestNavigation;
                    regions["settings-content"] = layout.TestContent;
                    foreach (var row in layout.TestRows) regions["setting:" + row.Path] =
                        new(row.Label.X, row.Label.Y, row.Control.Z, row.Control.W);
                }, frames: 16);
            else if (!TrySpecialScene(args)) Run(Options.Parse(args)!);
            ImGuiErrors.ThrowIfNewErrors(errors);
        }
        catch (Exception error) { throw new InvalidOperationException($"Scene {item.Name}: {error.Message}", error); }
        finally {
            Console.SetOut(previousOut);
            ScriptedFrameDriver.SavedFrameObserver = previousObserver;
            ChromeControls.PreviewAfterButton = previousButtonObserver;
            ImGuiNET.ImGui.SetCurrentContext(previousNative);
            MapDrawContext.Current = previousContext;
        }
        var capture = new VisualCapture(path, regions);
        visualCaptures?.Add(item.Name, capture);
        return capture;
    }

    private static void CaptureGalleryRegions(ScriptedFrameDriver driver, Dictionary<string, Vector4> regions) {
        var quest = driver.Context.QuestLog;
        regions["quest-panel"] = quest.Bounds;
        regions["quest-list"] = quest.ListBounds;
        regions["quest-tabs"] = quest.TabBounds.Aggregate(Vector4.Zero, UnionBounds);
        regions["quest-detail"] = quest.DetailBounds;
        regions["quest-progress"] = quest.ProgressBounds;
        regions["quest-search"] = quest.SearchBounds;
        regions["quest-map"] = quest.DetailMapBounds;
        regions["quest-row"] = quest.Rows.FirstOrDefault().Bounds;
        regions["quest-pin"] = quest.Layout.Pin;
        regions["item-tooltip"] = ItemTooltipDrawing.LastTooltipBounds;
        regions["item-menu"] = quest.RewardMenuBounds;
        foreach (var row in quest.Rows) regions["quest-row:" + row.Id] = row.Bounds;
        regions["toolbar"] = MapToolbarDrawing.LastBounds;
        regions["location"] = LocationMenuDrawing.PopoverBounds;
        regions["location-card"] = LocationMenuDrawing.CardBounds;
        regions["filter"] = FilterBarDrawing.PopoverBounds;
        regions["context"] = new(driver.Context.ContextMenu.Min.X, driver.Context.ContextMenu.Min.Y,
            driver.Context.ContextMenu.Max.X, driver.Context.ContextMenu.Max.Y);
        if (driver.Palette is { } palette) {
            regions["palette"] = new(palette.PanelMin.X, palette.PanelMin.Y, palette.PanelMax.X, palette.PanelMax.Y);
            regions["palette-search"] = palette.FieldBounds;
        }
        if (driver.Context.Assistance is { } assistance) {
            regions["travel"] = new(assistance.CardMin.X, assistance.CardMin.Y, assistance.CardMax.X, assistance.CardMax.Y);
            if (assistance.PinLayer is { } pins) regions["pin-editor"] = pins.EditorBounds;
        }
        regions["update"] = UpdateNotice.LastBounds;
        var handle = driver.Window.MoveHandle.Geometry.Center;
        regions["move"] = new(handle.X - 48, handle.Y - 22, handle.X + 48, handle.Y + 22);
        if (ReferenceEquals(PopupDiagnostics.Context, driver.Context) && PopupDiagnostics.LastFrame == driver.FrameId)
            regions["popup"] = PopupDiagnostics.LastBounds;
    }

    private static Vector4 UnionBounds(Vector4 a, Vector4 b) => b.Z <= b.X || b.W <= b.Y ? a
        : a == Vector4.Zero ? b : new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.Z, b.Z), Math.Max(a.W, b.W));
}
