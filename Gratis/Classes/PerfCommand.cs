using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using Gratis.Compat.CommandManager;
using Gratis.Rendering;

namespace Gratis.Classes;

// /gratis perf [seconds]: times Gratis's own draw while the map is open. The first third times the whole map window
// alone, which is close to what Dalamud's plugin stats show; the rest times each part, which costs a little extra.
internal sealed class PerfCommand : IDisposable {
    [PluginService] private IChatGui ChatGui { get; set; } = null!;

    private enum Phase { Idle, Total, Detail }

    private Phase phase;
    private long phaseEnds, totalFrames;
    private double totalMs;
    private int detailSeconds, totalSeconds;
    private static readonly int SectionCount = Enum.GetValues<BenchmarkSection>().Length;
    private readonly global::System.Collections.Generic.List<double> totalFrameMs = [], detailFrameMs = [];
    private readonly long[] lastSelf = new long[SectionCount], lastCalls = new long[SectionCount];
    private long lastWindowTicks, lastWindowCalls;
    private int lastGc0, lastGc1, lastGc2;
    private double worstMs;
    private string gcNoTimers = "";
    // Frames over this are 'slow'; each part's average in slow frames is compared with its average in the rest.
    private const double SlowMs = 1.0;
    private readonly double[] slowSelf = new double[SectionCount], fastSelf = new double[SectionCount];
    private int slowFrames, fastFrames;
    private (int Gen0, int Gen1, int Gen2, double PauseMs) gcAtStart, gcAtSwitch;
    private string worstParts = "";
    private string worstGc = "";

    public static PerfCommand Register() {
        var command = new PerfCommand();
        Service.PluginInterface.Inject(command);
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/perf",
            Delegate = args => command.Start(args.Length > 0 && int.TryParse(args[0], out var seconds)
                ? Math.Clamp(seconds, 6, 60) : 12),
        });
        return command;
    }

    private void Start(int seconds) {
        if (phase != Phase.Idle) {
            ChatGui.Print("[Gratis] perf is already sampling.");
            return;
        }
        totalSeconds = Math.Max(2, seconds / 3);
        detailSeconds = seconds - totalSeconds;
        totalFrameMs.Clear();
        detailFrameMs.Clear();
        worstMs = 0;
        worstParts = worstGc = "";
        Array.Clear(slowSelf);
        Array.Clear(fastSelf);
        slowFrames = fastFrames = 0;
        ResetDeltas();
        gcAtStart = GcNow();
        BenchmarkProbe.Reset();
        BenchmarkProbe.Mask = 1UL << (int)BenchmarkSection.Window;
        BenchmarkProbe.Enabled = true;
        phase = Phase.Total;
        phaseEnds = Environment.TickCount64 + totalSeconds * 1000L;
        Service.Framework.Update += OnUpdate;
        ChatGui.Print($"[Gratis] perf: sampling for {seconds}s. Keep the map open and use it as you normally would.");
    }

    private static (int, int, int, double) GcNow() => (GC.CollectionCount(0), GC.CollectionCount(1),
        GC.CollectionCount(2), GC.GetTotalPauseDuration().TotalMilliseconds);

    private static string GcSince((int Gen0, int Gen1, int Gen2, double PauseMs) from) {
        var now = GcNow();
        return $"{now.Item1 - from.Gen0} gen0 / {now.Item2 - from.Gen1} gen1 / {now.Item3 - from.Gen2} gen2, "
            + $"{now.Item4 - from.PauseMs:0.0} ms paused";
    }

    private void ResetDeltas() {
        Array.Clear(lastSelf);
        Array.Clear(lastCalls);
        lastWindowTicks = lastWindowCalls = 0;
        lastGc0 = GC.CollectionCount(0);
        lastGc1 = GC.CollectionCount(1);
        lastGc2 = GC.CollectionCount(2);
    }

    // Runs once per game frame, so the change in the counters since the last call is the previous frame's draw.
    private void SampleFrame() {
        var scale = 1000.0 / Stopwatch.Frequency;
        var window = BenchmarkProbe.Counters[(int)BenchmarkSection.Window];
        var drawn = window.Calls - lastWindowCalls;
        var gc0 = GC.CollectionCount(0);
        var gc1 = GC.CollectionCount(1);
        var gc2 = GC.CollectionCount(2);
        var collected = gc2 > lastGc2 ? "gen2" : gc1 > lastGc1 ? "gen1" : gc0 > lastGc0 ? "gen0" : "";
        lastGc0 = gc0;
        lastGc1 = gc1;
        lastGc2 = gc2;
        var ms = (window.Ticks - lastWindowTicks) * scale;
        lastWindowTicks = window.Ticks;
        lastWindowCalls = window.Calls;
        var deltas = new (string Name, double Self, long Calls)[SectionCount];
        for (var i = 0; i < SectionCount; i++) {
            var counter = BenchmarkProbe.Counters[i];
            deltas[i] = (((BenchmarkSection)i).ToString(), (counter.SelfTicks - lastSelf[i]) * scale,
                counter.Calls - lastCalls[i]);
            lastSelf[i] = counter.SelfTicks;
            lastCalls[i] = counter.Calls;
        }
        if (drawn != 1)
            return;
        (phase == Phase.Total ? totalFrameMs : detailFrameMs).Add(ms);
        if (phase == Phase.Detail) {
            var bucket = ms > SlowMs ? slowSelf : fastSelf;
            for (var i = 0; i < SectionCount; i++)
                bucket[i] += deltas[i].Self;
            if (ms > SlowMs) slowFrames++;
            else fastFrames++;
        }
        if (phase != Phase.Detail || ms <= worstMs)
            return;
        worstMs = ms;
        worstGc = collected;
        worstParts = string.Join(", ", deltas.Where(d => d.Calls > 0 && d.Name != nameof(BenchmarkSection.Window))
            .OrderByDescending(d => d.Self).Take(5).Select(d => $"{d.Name} {d.Self:0.00}ms x{d.Calls}"));
    }

    private static string Spread(global::System.Collections.Generic.List<double> frames) {
        if (frames.Count == 0)
            return "no frames";
        var sorted = frames.Order().ToArray();
        double At(double q) => sorted[Math.Min(sorted.Length - 1, (int)(q * sorted.Length))];
        return $"mean {sorted.Average():0.000} / p50 {At(.5):0.000} / p95 {At(.95):0.000} / p99 {At(.99):0.000} / "
            + $"max {sorted[^1]:0.000} ms over {sorted.Length} frames";
    }

    private void OnUpdate(IFramework framework) {
        if (phase != Phase.Idle)
            SampleFrame();
        if (Environment.TickCount64 < phaseEnds)
            return;
        var window = BenchmarkProbe.Counters[(int)BenchmarkSection.Window];
        if (phase == Phase.Total) {
            totalFrames = window.Calls;
            totalMs = window.Calls == 0 ? 0 : window.Ticks * 1000.0 / Stopwatch.Frequency / window.Calls;
            gcNoTimers = GcSince(gcAtStart);
            gcAtSwitch = GcNow();
            BenchmarkProbe.Reset();
            ResetDeltas();
            BenchmarkProbe.Mask = ulong.MaxValue;
            phase = Phase.Detail;
            phaseEnds = Environment.TickCount64 + detailSeconds * 1000L;
            return;
        }
        BenchmarkProbe.Enabled = false;
        BenchmarkProbe.Mask = ulong.MaxValue;
        phase = Phase.Idle;
        Service.Framework.Update -= OnUpdate;
        Report(window);
    }

    private void Report(BenchmarkProbe.Counter window) {
        if (totalFrames == 0) {
            ChatGui.Print("[Gratis] perf: the map was not drawn while sampling. Open the map and run /gratis perf again.");
            return;
        }
        var scale = 1000.0 / Stopwatch.Frequency;
        var frames = Math.Max(1, window.Calls);
        var rows = BenchmarkProbe.Counters.Select((counter, index) => (Name: ((BenchmarkSection)index).ToString(),
                Self: counter.SelfTicks * scale / frames, Incl: counter.Ticks * scale / frames,
                Calls: counter.Calls / (double)frames, Bytes: counter.SelfBytes / (double)frames))
            .Where(row => row.Calls > 0 && row.Name != nameof(BenchmarkSection.Window))
            .OrderByDescending(row => row.Self).ToArray();
        var config = System.SystemConfig;
        var detail = new StringBuilder();
        foreach (var row in rows)
            detail.Append($"; {row.Name} self={row.Self:0.000}ms incl={row.Incl:0.000}ms calls/frame={row.Calls:0.#} "
                + $"alloc={row.Bytes:0}B");
        var context = $"glass={config.GlassEnabled}/{config.GlassBlur} enemies={config.ShowEnemies} "
            + $"labels={config.HideOverlappingLabels} circle={config.MapCircle} territory={Service.ClientState.TerritoryType} "
            + System.MapRenderer.EnemyDiagnostics();
        var windowSelf = window.SelfTicks * scale / frames;
        var summary = $"perf: map draw {totalMs:0.000} ms/frame over {totalFrames} frames (whole window, no section "
            + $"timers); with section timers {window.Ticks * scale / frames:0.000} ms/frame (the timers add some). "
            + "Top: " + string.Join(", ", rows.Take(6).Select(row => $"{row.Name} {row.Self:0.000}"))
            + $", window itself {windowSelf:0.000}.";
        var slowParts = slowFrames == 0 || fastFrames == 0 ? "" : $" Slow frames (over {SlowMs:0} ms): {slowFrames} of "
            + $"{slowFrames + fastFrames}. What is bigger in them than in other frames: "
            + string.Join(", ", Enumerable.Range(0, SectionCount)
                .Select(i => (Name: i == (int)BenchmarkSection.Window ? "window itself" : ((BenchmarkSection)i).ToString(),
                    Extra: slowSelf[i] / slowFrames - fastSelf[i] / fastFrames))
                .OrderByDescending(part => part.Extra)
                .Take(5).Select(part => $"{part.Name} +{part.Extra:0.000} ms")) + ".";
        var worst = worstMs > 0
            ? $" Slowest timed frame {worstMs:0.00} ms{(worstGc.Length > 0 ? $" (a {worstGc} GC ran in it)" : "")}: {worstParts}."
            : "";
        var spread = $" Frames without section timers: {Spread(totalFrameMs)}. With: {Spread(detailFrameMs)}."
            + $" GC while sampling, first phase: {gcNoTimers}; second phase: {GcSince(gcAtSwitch)}.";
        Service.Log.Information("[Gratis] " + summary + spread + slowParts + worst + " Context: " + context + detail);
        ChatGui.Print("[Gratis] " + summary + spread + slowParts + worst + " Full breakdown is in dalamud.log.");
    }

    public void Dispose() {
        if (phase != Phase.Idle) {
            BenchmarkProbe.Enabled = false;
            BenchmarkProbe.Mask = ulong.MaxValue;
            phase = Phase.Idle;
            Service.Framework.Update -= OnUpdate;
        }
    }
}
