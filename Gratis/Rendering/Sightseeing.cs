using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Gratis.Controllers;

namespace Gratis.Rendering;

public readonly record struct SightseeingRow(uint Id, string Name, uint MapId, uint TerritoryId,
    Vector2 World, int MinTime, int MaxTime, uint Icon);

public static class Sightseeing {
    // Skip corrupt/missing references individually; preserve good rows if sheet enumeration itself fails.
    public static SightseeingRow[] Load<T>(Func<IEnumerable<T>> source, Func<T, SightseeingRow?> read,
        Action<Exception> failed) {
        var rows = new List<SightseeingRow>();
        try {
            foreach (var item in source()) {
                try { if (read(item) is { } row) rows.Add(row); }
                catch (Exception ex) { failed(ex); }
            }
        }
        catch (Exception ex) { failed(ex); }
        return rows.ToArray();
    }

    public static int MinuteFromHhmm(int time) => time / 100 * 60 + time % 100;
    // Equal endpoints mean unrestricted. Start inclusive, end exclusive, including midnight wrap.
    public static bool InWindow(int minute, int min, int max) => min == max
        || (min < max ? minute >= min && minute < max : minute >= min || minute < max);
    public static string WindowText(int min, int max) => min == max ? "Available at any ET"
        : $"Available ET {min / 60:00}:{min % 60:00}-{max / 60:00}:{max % 60:00}";
    public static IEnumerable<PlaceEntry> Entries(IEnumerable<SightseeingRow> rows, uint territory,
        Func<uint, bool> completed) => rows.Where(r => r.Id != 0 && r.MapId != 0
            && r.TerritoryId == territory && !string.IsNullOrWhiteSpace(r.Name) && !completed(r.Id))
        .Select(r => new PlaceEntry($"sightseeing:{r.Id}", PlaceKind.Marker, "Sightseeing: " + r.Name,
            WindowText(MinuteFromHhmm(r.MinTime), MinuteFromHhmm(r.MaxTime)), r.MapId, r.TerritoryId,
            r.World, Aliases: "sightseeing vista adventure") { Icon = r.Icon });
}
