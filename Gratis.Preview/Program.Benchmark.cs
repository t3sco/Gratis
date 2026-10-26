using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Gratis.Classes;
using Gratis.Controllers;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;
using ImGuiNET;

namespace Gratis.Preview;

internal static partial class Program {
    private static readonly string[] BenchmarkCases = [
        "few-idle", "few-interaction", "busy-default", "busy-labels", "busy-drag",
        "panzoom-idle", "panzoom-inertia", "hover-idle", "hover-card", "hover-tooltip",
        "search-open", "search-typing", "quest-list", "quest-scroll", "quest-summary", "quest-item-tooltip",
        "settings-idle", "settings-interaction", "glass-visible", "glass-faded", "glass-reveal", "closed-baseline",
    ];

    private readonly record struct BenchSample(double CpuMs, long Bytes, int Gen0, int Gen1, int Gen2,
        int Vertices, int Indices, int DrawCalls, int Markers, int Labels, float Scale, Vector2 Offset,
        bool Dragging, bool Card, bool Tooltip, bool Item, float Chrome, uint FirstVisibleQuest, string Query);
    private sealed record BenchStats(double Mean, double P50, double P95, double P99, double Max);
    private sealed record BenchSection(string Name, long Calls, double InclusiveMsPerFrame, double SelfMsPerFrame,
        double BytesPerFrame, double SelfBytesPerFrame);
    private sealed record BenchResult(string Scenario, int Warmup, int Frames, double StartupMs,
        double FontRegistrationMs, double FontAtlasMs, BenchSample FirstFrame, BenchStats CpuMs,
        BenchStats AllocatedBytes, int[] Collections, double Vertices, double Indices, double DrawCalls,
        double Markers, double Labels, string Evidence, BenchSample[] Samples, BenchSection[] Profile);

    private static int RunBenchmarks(string[] args) {
        int frames = 900, warmup = 240;
        var output = "_artifacts/bench/baseline.json";
        string? only = null;
        for (var i = 1; i < args.Length; i++) {
            if (i + 1 == args.Length) throw new ArgumentException("Benchmark option requires a value.");
            switch (args[i++]) {
                case "--frames": frames = int.Parse(args[i]); break;
                case "--warmup": warmup = int.Parse(args[i]); break;
                case "--out": output = args[i]; break;
                case "--scenario": only = args[i]; break;
                default: throw new ArgumentException("Unknown benchmark option.");
            }
        }
        if (frames < 600 || warmup < 120) throw new ArgumentException("Use at least 600 frames and 120 warm-up frames.");
        var names = only == null ? BenchmarkCases : only.Split(',');
        if (names.Any(n => !BenchmarkCases.Contains(n))) throw new ArgumentException("Unknown benchmark scenario.");
        var results = new List<BenchResult>();
        Console.WriteLine($"Benchmark: {RuntimeInformation.FrameworkDescription}, {RuntimeInformation.OSDescription}, "
            + $"{RuntimeInformation.ProcessArchitecture}, CPUs={Environment.ProcessorCount}, "
            + $"StopwatchHz={Stopwatch.Frequency}, GC-server={(global::System.Runtime.GCSettings.IsServerGC)}; "
            + $"{warmup} warmup / {frames} measured / 600 separately profiled frames; 1280x900, 60Hz script.");
        Console.WriteLine("Scenario | mean/p50/p95/p99/max ms | mean/p95/max B | GC 0/1/2 | vtx/idx/calls | markers/labels");
        foreach (var name in names) {
            var config = QuestLogConfig(1280, 900);
            config.WindowPosition = new(16);
            config.WindowSize = new(1248, 868);
            config.MapScale = .55f;
            config.FadeMode = 0;
            config.ShowUpdateNotice = false;
            config.ShowFeedbackButton = false;
            config.QuestLogOpenWithMap = name.StartsWith("quest-", StringComparison.Ordinal);
            config.GlassEnabled = name.StartsWith("glass-", StringComparison.Ordinal);
            config.PinChrome = name is not ("glass-faded" or "glass-reveal");
            config.HideOverlappingLabels = name == "busy-default";
            config.Markers.Declutter = name == "busy-default";
            var busy = name.StartsWith("busy-", StringComparison.Ordinal);
            var script = new FrameScript { Width = 1280, Height = 900,
                Palette = name.StartsWith("search-", StringComparison.Ordinal), QuestLog = config.QuestLogOpenWithMap };
            for (var i = 0; i < (busy ? 600 : 6); i++)
                script.Markers.Add(new(60401, busy ? new((i % 30 - 14.5f) * 38, (i / 30 - 9.5f) * 36)
                    : new(260 + i * 55, 40)));
            using var driver = new ScriptedFrameDriver(config, script, benchmark: true);
            if (driver.PlaceSource is { } places)
                for (var i = 0; i < 1000; i++)
                    places.AdditionalPlaces.Add(new($"bench:{i}", PlaceKind.Marker, $"Limsa location {i:0000}",
                        "La Noscea > Limsa Lominsa", 1, 1, new(i % 40 * 10, i / 40 * 10)));
            for (var i = 0; i < driver.Markers.Count; i++) {
                var label = $"Location {i:000}";
                driver.Markers[i].PrimaryText = () => label;
                driver.Markers[i].SecondaryText = () => "Benchmark marker · Middle La Noscea";
            }
            if (busy)
                for (var i = 0; i < 300; i++) {
                    var world = new Vector2((i % 20 - 9.5f) * 55, (i / 20 - 7) * 47);
                    driver.BenchmarkLabels.Add((new MarkerInfo { Position = world, Offset = default, Scale = 1 },
                        $"P{i:000}", world));
                }
            if (driver.Context.QuestSource is QuestLogFixture quests) {
                SummaryFixture.Apply(quests, "default");
                var originals = quests.Snapshot.Quests;
                quests.Snapshot = quests.Snapshot with { Revision = 2,
                    Quests = Enumerable.Range(0, 240).Select(i => originals[i % originals.Count] with {
                        RowId = (uint)(100000 + i), Name = i == 0 ? originals[0].Name : $"{originals[i % originals.Count].Name} {i:000}"
                    }).ToArray() };
            }
            driver.BenchmarkSettingsVisible = name.StartsWith("settings-", StringComparison.Ordinal);
            driver.BenchmarkSettings.SelectedSection = 3;
            driver.BenchmarkMapVisible = name != "closed-baseline";
            var first = MeasureBenchmarkFrame(driver, new(1d / 60, SettingsOpen: driver.BenchmarkSettingsVisible));
            for (var i = 0; i < 30; i++) driver.Step(new(driver.Now + 1d / 60), snapshot: false);
            if (driver.Palette != null) driver.Palette.Open("li");
            if (name is "quest-summary" or "quest-item-tooltip") {
                var bounds = driver.Context.QuestLog.Rows[0].Bounds;
                var center = BenchCenter(bounds);
                driver.Step(new(driver.Now + 1d / 60, Mouse: center, Left: true), snapshot: false);
                driver.Step(new(driver.Now + 1d / 60, Mouse: center, Left: false), snapshot: false);
            }
            for (var i = 0; i < 60; i++) driver.Step(new(driver.Now + 1d / 60, Mouse: new(-1000)), snapshot: false);
            var inputs = BenchmarkInputs(driver, name, warmup + frames + 600);
            for (var i = 0; i < warmup; i++) driver.Step(inputs[i], snapshot: false);
            var samples = new BenchSample[frames];
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            for (var i = 0; i < frames; i++) samples[i] = MeasureBenchmarkFrame(driver, inputs[warmup + i]);
            var evidence = BenchmarkEvidence(driver, name, samples);
            BenchmarkProbe.Reset();
            BenchmarkProbe.Enabled = true;
            try {
                for (var i = 0; i < 600; i++) driver.Step(inputs[warmup + frames + i], snapshot: false);
            }
            finally { BenchmarkProbe.Enabled = false; }
            var profile = Enum.GetValues<BenchmarkSection>().Select(section => {
                var c = BenchmarkProbe.Counters[(int)section];
                return new BenchSection(section.ToString(), c.Calls,
                    c.Ticks * 1000d / Stopwatch.Frequency / 600, c.SelfTicks * 1000d / Stopwatch.Frequency / 600,
                    c.Bytes / 600d, c.SelfBytes / 600d);
            }).Where(p => p.Calls > 0).OrderByDescending(p => p.SelfMsPerFrame).ToArray();
            if (busy && profile.Single(p => p.Name == nameof(BenchmarkSection.CollectMarkers)).SelfBytesPerFrame != 0)
                throw new InvalidOperationException("Debug-off marker submission allocated outside its nested drawing sections.");
            var result = new BenchResult(name, warmup, frames, driver.StartupMs, driver.FontRegistrationMs,
                driver.FontAtlasMs, first, BenchmarkStats(samples.Select(s => s.CpuMs)),
                BenchmarkStats(samples.Select(s => (double)s.Bytes)),
                [samples.Sum(s => s.Gen0), samples.Sum(s => s.Gen1), samples.Sum(s => s.Gen2)],
                samples.Average(s => s.Vertices), samples.Average(s => s.Indices), samples.Average(s => s.DrawCalls),
                samples.Average(s => s.Markers), samples.Average(s => s.Labels), evidence, samples, profile);
            results.Add(result);
            var t = result.CpuMs; var a = result.AllocatedBytes;
            Console.WriteLine($"{name} | {t.Mean:F4}/{t.P50:F4}/{t.P95:F4}/{t.P99:F4}/{t.Max:F4} | "
                + $"{a.Mean:F0}/{a.P95:F0}/{a.Max:F0} | {string.Join('/', result.Collections)} | "
                + $"{result.Vertices:F0}/{result.Indices:F0}/{result.DrawCalls:F1} | {result.Markers:F0}/{result.Labels:F0} [{evidence}]");
            ImGuiErrors.ThrowIfNewErrors(0);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(new { CreatedUtc = DateTime.UtcNow,
            Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(), CPUs = Environment.ProcessorCount,
            StopwatchFrequency = Stopwatch.Frequency, ServerGC = global::System.Runtime.GCSettings.IsServerGC,
            TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "runtime default",
            Notes = "CPU-only ImGui submission. No software rasterizer, snapshots or GPU. Profiles are separate inclusive/self sections; closed-baseline excludes unavailable Dalamud hooks.",
            Results = results }, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
        Console.WriteLine($"Raw per-frame samples, startup, and separate profiles: {Path.GetFullPath(output)}");
        return 0;
    }

    private static Vector2 BenchCenter(Vector4 b) => new((b.X + b.Z) / 2, (b.Y + b.W) / 2);

    private static ScriptFrame[] BenchmarkInputs(ScriptedFrameDriver driver, string name, int count) {
        var inputs = new ScriptFrame[count];
        var origin = driver.Now;
        var hover = driver.Renderer.WorldToScreen(new(260, 40), driver.Window.MapDrawOffset);
        var reward = driver.Context.QuestLog.RewardBounds.Count > 0 ? BenchCenter(driver.Context.QuestLog.RewardBounds[0]) : new Vector2(-1000);
        var list = BenchCenter(driver.Context.QuestLog.ListBounds);
        for (var i = 0; i < count; i++) {
            var phase = i % 180;
            Vector2 mouse = new(-1000);
            bool left = false, control = false;
            float wheel = 0;
            string? text = null;
            ImGuiKey[]? keys = null;
            if (name == "few-interaction") mouse = new(600 + 100 * MathF.Sin(i / 30f), 500);
            if (name is "panzoom-inertia" or "busy-drag") {
                mouse = new(700 + (phase < 30 ? phase * 2 : 60), 500 + (phase < 30 ? phase : 30));
                left = phase is >= 1 and < 30;
                wheel = phase == 75 ? 1 : phase == 135 ? -1 : 0;
            }
            if (name == "hover-card") mouse = hover + new Vector2(MathF.Sin(i / 20f), 0);
            if (name == "hover-tooltip") mouse = BenchCenter(MapToolbarDrawing.ButtonBounds[4]);
            if (name is "search-open" or "search-typing") {
                mouse = new(-1000);
                if (name == "search-typing" && i % 30 == 0) {
                    control = true;
                    keys = [ImGuiKey.A];
                }
                if (name == "search-typing" && i % 30 == 1) text = i % 60 == 1 ? "lim" : "li";
            }
            if (name == "quest-scroll") {
                mouse = list;
                wheel = i % 12 == 0 ? (phase < 90 ? -2 : 2) : 0;
            }
            if (name == "quest-item-tooltip") mouse = reward;
            if (name == "settings-interaction") {
                mouse = BenchCenter(driver.BenchmarkSettings.TestContent);
                wheel = i % 12 == 0 ? (phase < 90 ? -1 : 1) : 0;
            }
            if (name == "glass-reveal") mouse = i % 360 < 30 ? new(700, 500) : new(-1000);
            inputs[i] = new(origin + (i + 1d) / 60, Mouse: mouse, Left: left, Wheel: wheel,
                Control: control, Keys: keys, Text: text, SettingsOpen: driver.BenchmarkSettingsVisible);
        }
        return inputs;
    }

    private static string BenchmarkEvidence(ScriptedFrameDriver d, string name, BenchSample[] samples) {
        if (name == "hover-card" && d.Context.HoverCardFrame != d.FrameId)
            throw new InvalidOperationException("Hover card scenario did not draw a card.");
        if (name == "quest-item-tooltip" && ItemTooltipDrawing.LastTooltipFrame != d.FrameId)
            throw new InvalidOperationException("Quest item scenario did not draw its tooltip.");
        if (name.StartsWith("quest-", StringComparison.Ordinal) && d.Context.QuestLog.Results.Count < 200)
            throw new InvalidOperationException("Quest fixture did not expose 200 quests.");
        if (name.StartsWith("search-", StringComparison.Ordinal) && d.Palette?.IsOpen != true)
            throw new InvalidOperationException("Search scenario was closed.");
        if (name == "hover-tooltip" && !samples.Any(s => s.Tooltip))
            throw new InvalidOperationException("Toolbar scenario did not draw a tooltip.");
        if ((name is "panzoom-inertia" or "busy-drag")
            && (!samples.Any(s => s.Dragging) || samples.Max(s => s.Scale) - samples.Min(s => s.Scale) < .001f))
            throw new InvalidOperationException("Pan/zoom input did not drive the viewport.");
        if (name == "quest-scroll" && samples.Select(s => s.FirstVisibleQuest).Distinct().Count() < 2)
            throw new InvalidOperationException("Quest list did not scroll.");
        if (name == "search-typing" && samples.Select(s => s.Query).Distinct().Count() < 2)
            throw new InvalidOperationException("Search typing did not change the query.");
        if (name == "settings-interaction" && samples.Max(s => s.Scale) - samples.Min(s => s.Scale) > .001f)
            throw new InvalidOperationException("Settings wheel input leaked through to the map.");
        return $"chrome={d.Context.Chrome.Progress:F2}; query={d.Palette?.Query ?? "-"}; "
            + $"quests={d.Context.QuestLog.Results.Count}; detail={d.Context.QuestLog.DetailOpen}; "
            + $"card={d.Context.HoverCardFrame == d.FrameId}; item={ItemTooltipDrawing.LastTooltipFrame == d.FrameId}; "
            + $"dragFrames={samples.Count(s => s.Dragging)}; zoomRange={samples.Min(s => s.Scale):F3}-{samples.Max(s => s.Scale):F3}; "
            + $"tooltipFrames={samples.Count(s => s.Tooltip)}; listRows={samples.Select(s => s.FirstVisibleQuest).Distinct().Count()}";
    }

    private static BenchSample MeasureBenchmarkFrame(ScriptedFrameDriver driver, ScriptFrame frame) {
        var g0 = GC.CollectionCount(0); var g1 = GC.CollectionCount(1); var g2 = GC.CollectionCount(2);
        var bytes = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        driver.Step(frame, snapshot: false);
        var ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
        var data = ImGui.GetDrawData();
        var calls = 0;
        for (var i = 0; i < data.CmdListsCount; i++) {
            var commands = data.CmdLists[i].CmdBuffer;
            for (var j = 0; j < commands.Size; j++)
                if (commands[j].ElemCount > 0 && commands[j].UserCallback == 0) calls++;
        }
        return new(ms, allocated, GC.CollectionCount(0) - g0, GC.CollectionCount(1) - g1,
            GC.CollectionCount(2) - g2, data.TotalVtxCount, data.TotalIdxCount, calls,
            driver.BenchmarkMapVisible ? driver.Trace.DrawnMarkers.Count : 0, driver.BenchmarkLabelsDrawn,
            driver.Config.MapScale, driver.Renderer.DrawOffset, driver.Window.Dragging,
            driver.Context.HoverCardFrame == driver.FrameId,
            Math.Abs(ChromeControls.LastTooltipTime - ImGui.GetTime()) < .001 && ChromeControls.LastTooltipBounds != Vector4.Zero,
            ItemTooltipDrawing.LastTooltipFrame == driver.FrameId, driver.Context.Chrome.Progress,
            driver.Context.QuestLog.Rows.Count > 0 ? driver.Context.QuestLog.Rows[0].Id : 0,
            driver.Palette?.Query ?? "");
    }

    private static BenchStats BenchmarkStats(IEnumerable<double> source) {
        var values = source.Order().ToArray();
        double Percentile(double p) => values[Math.Clamp((int)Math.Ceiling(p * values.Length) - 1, 0, values.Length - 1)];
        return new(values.Average(), Percentile(.50), Percentile(.95), Percentile(.99), values[^1]);
    }
}
