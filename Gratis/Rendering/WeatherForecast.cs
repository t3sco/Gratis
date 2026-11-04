using System;
using System.Collections.Generic;

namespace Gratis.Rendering;

public static class WeatherForecast {
    public const long WindowSeconds = 1400;
    public static long WindowStart(long unix) => unix - ((unix % WindowSeconds + WindowSeconds) % WindowSeconds);
    public static uint Target(long unix) {
        if (unix < 0) throw new ArgumentOutOfRangeException(nameof(unix));
        var bell = unix / 175;
        var increment = (bell + 8 - bell % 8) % 24;
        unchecked {
            var basis = (uint)(unix / 4200 * 100 + increment);
            var step = (basis << 11) ^ basis;
            return ((step >> 8) ^ step) % 100;
        }
    }

    // Zero slots are padding; populated sheet rates are cumulative, not weights.
    public static bool ValidRates(IReadOnlyList<(byte Rate, uint WeatherId)> rows) {
        byte previous = 0;
        foreach (var (rate, id) in rows) {
            if (rate == 0) continue;
            if (id == 0 || rate < previous || rate > 100) return false;
            previous = rate;
        }
        return previous == 100;
    }

    public static uint Pick(IReadOnlyList<(byte Rate, uint WeatherId)> rows, uint target) {
        if (target >= 100 || !ValidRates(rows)) return 0;
        foreach (var (rate, id) in rows)
            if (rate != 0 && target < rate) return id;
        return 0;
    }

    public static string Line(string current, string next, long seconds) =>
        $"Now: {current}, next: {next} in {seconds / 60}m {seconds % 60:00}s";

    // A bounded forecast: at most 100 windows (about 39 real hours), including zones with fixed weather.
    public static (uint Weather, long At)? NextChange(IReadOnlyList<(byte Rate, uint WeatherId)> rows, long unix) {
        var current = Pick(rows, Target(unix));
        if (current == 0) return null;
        var start = WindowStart(unix);
        for (var i = 1; i <= 100; i++) {
            if (start > long.MaxValue - i * WindowSeconds) return null;
            var at = start + i * WindowSeconds;
            var candidate = Pick(rows, Target(at));
            if (candidate != current) return (candidate, at);
        }
        return null;
    }
}
