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
    public static string Expansion(PlaceEntry place) => place.ExpansionName.Length > 0 ? place.ExpansionName
        : place.Expansion < names.Length ? names[place.Expansion] : $"Expansion {place.Expansion}";

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
        var names = entries.Where(p => p.Kind == PlaceKind.Zone).GroupBy(p => p.MapId)
            .ToDictionary(g => g.Key, g => g.First().Name);
        return results.GroupBy(r => r.Place.MapId).Select(g => new PaletteZone(g.Key,
            names.GetValueOrDefault(g.Key) ?? g.First().Place.Subtitle.Split('>', StringSplitOptions.TrimEntries
                | StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? $"Map {g.Key}",
            g.ToArray())).OrderBy(g => g.Name, StringComparer.Ordinal).ThenBy(g => g.MapId).ToArray();
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
        return results.Where(r => r.Place.Kind != PlaceKind.Marker || r.Place.World is not { } world
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
