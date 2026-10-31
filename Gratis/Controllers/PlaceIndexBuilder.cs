using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Gratis.Controllers;

public sealed record PlaceMapRow(uint Id, uint Territory, string Name, string Region, string Layer = "",
    string Aliases = "", uint Expansion = 0, string ExpansionName = "", string ArtPath = "", uint ExpansionIcon = 0);
public sealed record PlaceAetheryteRow(uint Id, uint Map, uint Territory, string Name, bool IsHub, uint Group,
    Vector2? World = null, string Aliases = "", uint AethernetNameId = 0);

public static class PlaceIndexBuilder {
    public static Vector2 MarkerWorld(Vector2 texture, float sizeFactor, Vector2 offset) =>
        (texture - new Vector2(1024))
        / sizeFactor + offset;

    public static bool IsUnnamed(string name) => string.IsNullOrWhiteSpace(name) || name.Trim().All(c => c == '?');

    public static PlaceMapRow[] WithFallbackArt(IEnumerable<PlaceMapRow> maps) {
        var rows = maps.ToArray();
        var covers = rows.Where(m => m.Id != 0 && !IsUnnamed(m.Name) && m.ArtPath.Length > 0)
            .GroupBy(m => m.Expansion).ToDictionary(g => g.Key,
                g => g.OrderBy(m => $"z:{m.Id}", StringComparer.Ordinal).First().ArtPath);
        return rows.Select(m => m.ArtPath.Length == 0 && covers.TryGetValue(m.Expansion, out var art)
            ? m with { ArtPath = art } : m).ToArray();
    }

    private static string Group(PlaceMapRow map) => (map.ExpansionName.Length > 0 ? map.ExpansionName + " > " : "")
        + map.Region + " > " + map.Name;
    public static IEnumerable<PlaceEntry> Build(IEnumerable<PlaceMapRow> maps,
        IEnumerable<PlaceAetheryteRow> aetherytes) {
        var mapRows = maps.Where(m => m.Id != 0 && !IsUnnamed(m.Name)).ToDictionary(m => m.Id);
        foreach (var map in mapRows.Values)
            yield return new PlaceEntry($"z:{map.Id}", PlaceKind.Zone,
                map.Name, Group(map), map.Id, map.Territory,
                Aliases: map.Aliases + " " + map.Layer, Expansion: map.Expansion) {
                Layer = map.Layer,
                ExpansionName = map.ExpansionName,
                ArtPath = map.ArtPath, ExpansionIcon = map.ExpansionIcon
            };
        var rows = aetherytes.ToArray();
        var hubs = rows.Where(a => a.IsHub && a.Group != 0).GroupBy(a => a.Group).ToDictionary(g => g.Key,
            g => g.First());
        foreach (var row in rows) {
            hubs.TryGetValue(row.Group, out var hub);
            var mapId = row.Map != 0 ? row.Map : hub?.Map ?? 0;
            if (!mapRows.TryGetValue(mapId, out var map) || row.Name.Length == 0)
                continue;
            // A shard in another zone (Old Sharlayan's "The Hall of Artifice (Labyrinthos)") is located on its own
            // map; naming the hub as its place made the subtitle say Old Sharlayan while Show opened Labyrinthos.
            var network = !row.IsHub && hub != null ? hub : null;
            var elsewhere = network != null && network.Map != 0 && network.Map != mapId;
            yield return new PlaceEntry($"a:{row.Id}", PlaceKind.Aetheryte, row.Name,
                Group(map) + (network != null && !elsewhere ? " > " + network.Name : ""), mapId,
                row.Territory != 0 ? row.Territory : map.Territory, row.World, row.Id,
                row.IsHub ? row.Id : hub?.Id ?? row.Id, row.Aliases, map.Expansion) {
                SearchContext = PlaceSearch.Normalize(Group(map) + (network != null ? " > " + network.Name : "")),
                AethernetNameId = row.AethernetNameId,
                ExpansionName = map.ExpansionName,
                ArtPath = map.ArtPath,
                ExpansionIcon = map.ExpansionIcon
            };
        }
    }
}