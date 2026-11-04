using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Gratis.Controllers;

#if GRATIS_PREVIEW
using PaletteTexture = System.IntPtr;
#else
using PaletteTexture = Dalamud.Bindings.ImGui.ImTextureID;
#endif

namespace Gratis.Rendering;

public sealed record PaletteExpansion(uint Id, string Name, PlaceEntry Cover, int Places, int Zones);
public sealed record PaletteZone(uint MapId, string Name, PlaceResult[] Places);
public readonly record struct PaletteImage(PaletteTexture Handle, int Width, int Height, uint Seed = 0) {
    public bool Ready => !Handle.Equals(default(PaletteTexture)) && Width > 0 && Height > 0;
}

public static class PaletteBrowser {
    private static readonly string[] names = ["A Realm Reborn",
        "Heavensward",
        "Stormblood",
        "Shadowbringers",
        "Endwalker",
        "Dawntrail"];
    internal static IReadOnlyList<string> ExpansionNames => names;

    /// <summary>
    /// Runs the palette's searching and grouping once on throwaway results. The first call of this LINQ-heavy code
    /// costs about 15 ms of JIT compilation, which otherwise lands in the frame where the palette is first opened.
    /// Everything it calls is a pure function of immutable entries, so it is safe on a background thread. Any failure
    /// is ignored: this only saves time, and the real search still runs when the palette opens.
    /// </summary>
    public static void Warm(PlaceEntry[] entries) {
        PrepareCode(typeof(PaletteOverlay), typeof(PaletteRowDrawing), typeof(PaletteBrowser),
            typeof(PlaceSearch), typeof(NearestPlaces), typeof(QuestLogPanel));
        try {
            if (entries.Length == 0)
                return;
            // The nearby cards search with a player position; without one the search returns at once and warms nothing.
            var nearby = new PlaceSearchContext(0, 0, Vector2.Zero);
            foreach (var query in new[] { "aetheryte", "market board", "inn" })
                NearestPlaces.Find(entries, query, nearby, _ => new PlaceAccess(true), true);
            var context = new PlaceSearchContext(0, 0, null);
            var recent = new Dictionary<string, long>();
            PlaceAccess Access(PlaceEntry _) => new(true);
            foreach (var (query, scope) in new[] { ("", PlaceKind.All), ("a", PlaceKind.All), ("z:a", PlaceKind.All),
                    ("a", PlaceKind.Quest) }) {
                var results = Distinct(PlaceSearch.Find(entries, query, scope, context, recent, Access, true, false));
                if (results.Length == 0)
                    continue;
                Expansions(entries, results.Select(r => r.Place));
                Zones(results, entries);
            }
        }
        catch (Exception) { }
    }
    public static string Expansion(PlaceEntry place) => place.ExpansionName.Length > 0 ? place.ExpansionName
        : place.Expansion < names.Length ? names[place.Expansion] : $"Expansion {place.Expansion}";

    /// <summary>
    /// Compiles every method of these types, and of the closures and iterators nested in them, without running any.
    /// The palette's first draw otherwise compiles its drawing code in that one frame (tens of milliseconds); this
    /// does the same work on a background thread. A method that cannot be prepared is skipped.
    /// </summary>
    private static void PrepareCode(params Type[] types) {
        const global::System.Reflection.BindingFlags all = global::System.Reflection.BindingFlags.Instance
            | global::System.Reflection.BindingFlags.Static | global::System.Reflection.BindingFlags.Public
            | global::System.Reflection.BindingFlags.NonPublic | global::System.Reflection.BindingFlags.DeclaredOnly;
        var pending = new Queue<Type>(types);
        while (pending.Count > 0) {
            var type = pending.Dequeue();
            if (type.ContainsGenericParameters)
                continue;
            foreach (var nested in type.GetNestedTypes(all))
                pending.Enqueue(nested);
            foreach (var method in type.GetMethods(all)) {
                if (method.IsAbstract || method.ContainsGenericParameters)
                    continue;
                try { global::System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(method.MethodHandle); }
                catch (Exception) { }
            }
        }
    }

    public static PaletteExpansion[] Expansions(IEnumerable<PlaceEntry> entries,
        IEnumerable<PlaceEntry>? counted = null) {
        var snapshot = entries.ToArray();
        var covers = snapshot.Where(p => p.Kind is PlaceKind.Zone or PlaceKind.Aetheryte)
            .GroupBy(p => p.Expansion).ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.ArtPath.Length > 0)
                .ThenBy(p => p.Kind != PlaceKind.Zone).ThenBy(p => p.Id, StringComparer.Ordinal).First());
        return (counted ?? snapshot.Where(p => p.Kind is PlaceKind.Zone or PlaceKind.Aetheryte))
            .GroupBy(p => p.Expansion).OrderBy(g => g.Key).Select(g => {
                var cover = covers.GetValueOrDefault(g.Key) ?? g.First();
                return new PaletteExpansion(g.Key, Expansion(cover), cover, g.Count(),
                    g.Select(p => p.MapId).Distinct().Count());
            }).ToArray();
    }

    public static PaletteZone[] Zones(IEnumerable<PlaceResult> results, IEnumerable<PlaceEntry> entries) {
        var zones = entries.Where(p => p.Kind == PlaceKind.Zone).GroupBy(p => p.MapId)
            .ToDictionary(g => g.Key, g => g.First());
        // Several maps can share a name (floors, story phases); list each distinct name and layer once, under its
        // lowest map, with every one of those maps' places merged in.
        var grouped = results.GroupBy(r => r.Place.MapId).Select(g => {
            var zone = zones.GetValueOrDefault(g.Key);
            return (MapId: g.Key, Layer: zone?.Layer ?? "", Name: zone?.Name ?? g.First().Place.Subtitle.Split('>',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).LastOrDefault()
                ?? $"Map {g.Key}", Places: g);
        }).OrderBy(g => g.MapId).GroupBy(g => (g.Name, g.Layer)).ToArray();
        return grouped.Select(g => {
            var known = new HashSet<(PlaceKind, string, string, Vector2?)>();
            var places = g.SelectMany(m => m.Places).Where(r => known.Add((r.Place.Kind, r.Place.Name, r.Place.Layer,
                r.Place.World is { } w ? new Vector2(MathF.Round(w.X), MathF.Round(w.Y)) : null))).ToArray();
            var name = g.Key.Layer.Length > 0 && grouped.Count(o => o.Key.Name == g.Key.Name) > 1
                ? g.Key.Name + " \u00b7 " + g.Key.Layer : g.Key.Name;
            return new PaletteZone(g.First().MapId, name, places);
        }).OrderBy(z => z.Name, StringComparer.Ordinal).ThenBy(z => z.MapId).ToArray();
    }

    public static string Count(int count, PlaceKind kind) => $"{count} " + (kind switch {
        PlaceKind.Aetheryte => count == 1 ? "aetheryte" : "aetherytes",
        PlaceKind.Zone => count == 1 ? "zone" : "zones",
        PlaceKind.Quest => count == 1 ? "quest" : "quests",
        PlaceKind.Marker => count == 1 ? "marker" : "markers",
        _ => count == 1 ? "place" : "places",
    });

    public static int Columns(float width, float scale) => width >= 660 * scale ? 3 : width >= 400 * scale ? 2 : 1;
    public static int Navigate(int current, int count, int columns, int horizontal, int vertical) =>
        Math.Clamp(current + horizontal + vertical * columns, 0, Math.Max(0, count - 1));

    public static PlaceResult[] Distinct(PlaceResult[] results) {
        var known = new HashSet<(uint, string, Vector2)>();
        // Several maps can share a zone name (floors, story phases); keep the best-ranked row per name and layer.
        var zones = new HashSet<(string, string)>();
        return results.Where(r => r.Place.Kind == PlaceKind.Zone ? zones.Add((r.Place.Name, r.Place.Layer))
            : r.Place.Kind != PlaceKind.Marker || r.Place.World is not { } world
                || known.Add((r.Place.MapId, r.Place.SearchName, world))).ToArray();
    }

    public static string Name(PlaceResult result, IReadOnlyList<PlaceResult> results) {
        var place = result.Place;
        var area = Detail(place);
        return place.SearchName is "aetheryte" or "aethernet" or "neutral" or "auto"
            && area.Length > 0 && area != "Current map" ? area : place.Name;
    }

    public static string Coordinates(PlaceResult result, IReadOnlyList<PlaceResult> results) {
        var place = result.Place;
        var duplicate = results.Any(p => p.Place.Id != place.Id && p.Place.Name == place.Name
            && (result.Distance == null || p.Distance is { } other
                && MathF.Round(other) == MathF.Round(result.Distance.Value)));
        if (!duplicate && (result.Distance != null || place.World == null)) return "";
        return place.World is { } w ? $"X {w.X:0}, Z {w.Y:0}" : $"Map {place.MapId}";
    }

    public static string Detail(PlaceEntry place) {
        if (place.Layer.Length > 0) return place.Layer;
        var parts = place.Subtitle.Split('>', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (var i = parts.Length - 1; i >= 0; i--)
            if (parts[i] != place.Name && parts[i] != Expansion(place)) return parts[i];
        return "";
    }
}
