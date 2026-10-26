using System.Diagnostics;

namespace Gratis.Rendering;

internal enum BenchmarkSection {
    Window, Texture, CollectMarkers, MarkerFlush, MapLabels, TextMeasure, TextDraw,
    QuestPrepare, QuestList, QuestRow, QuestSummary, ItemTooltip, Search, Palette, Settings, Surface, HoverCard,
    Stroke, Glass, Toolbar, Coordinates, LocationPrepare, MarkerDraw, MarkerTooltip, FontPush, FontLookup, Label,
}

internal static class BenchmarkProbe {
    internal sealed class Counter {
        public long Calls, Ticks, SelfTicks, Bytes, SelfBytes;
    }
    internal static readonly Counter[] Counters = Enum.GetValues<BenchmarkSection>().Select(_ => new Counter()).ToArray();
    private static readonly long[] ChildTicks = new long[64], ChildBytes = new long[64];
    private static int depth;
    public static bool Enabled { get; set; }
    public static void Reset() {
        depth = 0;
        foreach (var c in Counters) c.Calls = c.Ticks = c.SelfTicks = c.Bytes = c.SelfBytes = 0;
    }
    public static Scope Measure(BenchmarkSection section) => new(section, Enabled);

    internal readonly struct Scope : IDisposable {
        private readonly BenchmarkSection section;
        private readonly bool active;
        private readonly long start, bytes;
        public Scope(BenchmarkSection section, bool active) {
            this.section = section;
            this.active = active;
            start = bytes = 0;
            if (!active) return;
            if (depth == ChildTicks.Length) throw new InvalidOperationException("Benchmark scope depth exceeded.");
            ChildTicks[depth] = ChildBytes[depth] = 0;
            depth++;
            bytes = GC.GetAllocatedBytesForCurrentThread();
            start = Stopwatch.GetTimestamp();
        }
        public void Dispose() {
            if (!active) return;
            var ticks = Stopwatch.GetTimestamp() - start;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
            var c = Counters[(int)section];
            c.Calls++;
            c.Ticks += ticks;
            c.Bytes += allocated;
            depth--;
            c.SelfTicks += ticks - ChildTicks[depth];
            c.SelfBytes += allocated - ChildBytes[depth];
            if (depth > 0) {
                ChildTicks[depth - 1] += ticks;
                ChildBytes[depth - 1] += allocated;
            }
        }
    }
}
