using System;
using System.Collections.Generic;

namespace Gratis.Rendering;

/// <summary>One timed window of a gathering node: Eorzea start (hhmm) and length in Eorzea minutes.</summary>
public readonly record struct GatherSlot(int StartHhmm, int DurationEtMinutes);

/// <summary>
/// Pure, offline-testable Eorzea-time window maths for timed gathering nodes. No game types here: the sheet reads
/// live in Controllers/GatherWindowsGame.cs and hand us <see cref="GatherSlot"/> values.
/// </summary>
public static class GatherWindow {
    /// <summary>Sheet sentinel for "no window" (0 is a valid hhmm, midnight).</summary>
    public const int NoWindow = 65535;

    /// <summary>
    /// UNVERIFIED: GatheringRarePopTimeTable.Duration is assumed to be in Eorzea MINUTES (table 1 = start 900,
    /// duration 300 = 5 ET hours). If an in-game legendary/unspoiled node shows a window that is 60x too long or
    /// short, change this one constant (1 = Eorzea minutes as assumed; the unit would be wrong otherwise).
    /// TEST HOOK: stand at a legendary node and compare the hover line's "ends in" with the in-game timer;
    /// the selftest `gather-windows` pins the maths to this unit.
    /// </summary>
    public const int RarePopDurationEtMinutesPerUnit = 1;

    private const int EtDayMinutes = 1440;
    // Work in 1/35 ET-minute units so integer maths is exact: one real second = 12 units (3.5 s per ET minute).
    private const int UnitsPerEtMinute = 35;
    private const int UnitsPerRealSecond = 12;
    private const int DayUnits = EtDayMinutes * UnitsPerEtMinute;
    private const int RealDaySeconds = 4200;

    public static bool IsValidHhmm(int hhmm) => hhmm is >= 0 and < 2400 && hhmm % 100 < 60;

    private static int ToEtMinutes(int hhmm) => hhmm / 100 * 60 + hhmm % 100;

    /// <summary>Slot for an ephemeral node (start/end hhmm, may wrap past midnight). Null when no window.</summary>
    public static GatherSlot? Ephemeral(int startHhmm, int endHhmm) {
        if (!IsValidHhmm(startHhmm) || !IsValidHhmm(endHhmm))
            return null;
        var length = (ToEtMinutes(endHhmm) - ToEtMinutes(startHhmm) + EtDayMinutes) % EtDayMinutes;
        return length == 0 ? null : new GatherSlot(startHhmm, length);
    }

    /// <summary>Slot for a rare-pop table row (start hhmm, duration in sheet units). Null for unused slots.</summary>
    public static GatherSlot? RarePop(int startHhmm, int duration) {
        if (!IsValidHhmm(startHhmm) || duration <= 0)
            return null;
        return new GatherSlot(startHhmm, duration * RarePopDurationEtMinutesPerUnit);
    }

    /// <summary>
    /// Slots for one GatheringPointTransient row: the ephemeral pair plus every used rare-pop table slot. Null when
    /// the row has no window at all (the vast majority of nodes).
    /// </summary>
    public static GatherSlot[]? FromRow(int ephemeralStart, int ephemeralEnd, int[] rareStarts, int[] rareDurations) {
        var slots = new List<GatherSlot>(4);
        if (Ephemeral(ephemeralStart, ephemeralEnd) is { } ephemeral)
            slots.Add(ephemeral);
        for (var i = 0; i < rareStarts.Length && i < rareDurations.Length; i++) {
            if (RarePop(rareStarts[i], rareDurations[i]) is { } rare)
                slots.Add(rare);
        }
        return slots.Count == 0 ? null : slots.ToArray();
    }

    /// <summary>
    /// State at <paramref name="unixSeconds"/>. Up now: the window with the latest end. Otherwise the soonest start.
    /// Seconds round up, so a window never reads "0:00 left" while still open. Boundaries: the start instant is
    /// up, the end instant is not.
    /// </summary>
    public static (bool Up, long Seconds)? Evaluate(long unixSeconds, ReadOnlySpan<GatherSlot> slots) {
        if (slots.IsEmpty)
            return null;
        var now = (int)(((unixSeconds % RealDaySeconds) + RealDaySeconds) % RealDaySeconds) * UnitsPerRealSecond;
        var up = false;
        long best = long.MaxValue;
        var bestUp = long.MinValue;
        foreach (var slot in slots) {
            if (!IsValidHhmm(slot.StartHhmm) || slot.DurationEtMinutes <= 0)
                continue;
            var start = ToEtMinutes(slot.StartHhmm) * UnitsPerEtMinute;
            var length = (long)slot.DurationEtMinutes * UnitsPerEtMinute;
            var elapsed = ((now - start) % DayUnits + DayUnits) % DayUnits;
            if (elapsed < length) {
                up = true;
                bestUp = Math.Max(bestUp, length - elapsed);
            } else
                best = Math.Min(best, DayUnits - elapsed);
        }
        if (up)
            return (true, CeilSeconds(bestUp));
        return best == long.MaxValue ? null : (false, CeilSeconds(best));
    }

    private static long CeilSeconds(long units) => (units + UnitsPerRealSecond - 1) / UnitsPerRealSecond;

    /// <summary>"Up now, ends in 12:05" or "Next up in 3:09".</summary>
    public static string Format(bool up, long seconds) {
        if (seconds < 0)
            seconds = 0;
        var text = global::System.FormattableString.Invariant($"{seconds / 60:00}:{seconds % 60:00}");
        return up ? "Up now, ends in " + text : "Next up in " + text;
    }

    public static string? Describe(long unixSeconds, ReadOnlySpan<GatherSlot> slots) =>
        Evaluate(unixSeconds, slots) is { } r ? Format(r.Up, r.Seconds) : null;
}

/// <summary>
/// Per-node cache for the hover line: slots are loaded once per node, the text is rebuilt at most once per real
/// second per node, so hovering allocates nothing between refreshes.
/// </summary>
public sealed class GatherWindowCache(Func<uint, GatherSlot[]?> loader) {
    private sealed class Entry(GatherSlot[]? slots) {
        public readonly GatherSlot[]? Slots = slots;
        public long Second = long.MinValue;
        public string? Text;
    }

    private readonly Dictionary<uint, Entry> entries = [];

    public string? Text(uint nodeId, long unixSeconds) {
        if (!entries.TryGetValue(nodeId, out var entry))
            entries[nodeId] = entry = new Entry(loader(nodeId));
        if (entry.Slots is not { Length: > 0 })
            return null;
        if (entry.Second != unixSeconds) {
            entry.Second = unixSeconds;
            entry.Text = GatherWindow.Describe(unixSeconds, entry.Slots);
        }
        return entry.Text;
    }
}
