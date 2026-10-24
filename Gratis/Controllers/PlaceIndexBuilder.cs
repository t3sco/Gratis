using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Gratis.Controllers;

public sealed record PlaceMapRow(uint Id, uint Territory, string Name, string Region, string Layer = "",
    string Aliases = "", uint Expansion = 0, string ExpansionName = "");
public sealed record PlaceAetheryteRow(uint Id, uint Map, uint Territory, string Name, bool IsHub,
    uint Group, Vector2? World = null, string Aliases = "");

public static class PlaceIndexBuilder
{
    public static Vector2 MarkerWorld(Vector2 texture, float sizeFactor, Vector2 offset) =>
        (texture - new Vector2(1024)) / sizeFactor + offset;

    private static string Group(PlaceMapRow map) =>
        (map.ExpansionName.Length > 0 ? map.ExpansionName + " > " : "") + map.Region + " > " + map.Name;
    public static IEnumerable<PlaceEntry> Build(IEnumerable<PlaceMapRow> maps, IEnumerable<PlaceAetheryteRow> aetherytes)
    {
        var mapRows = maps.Where(m => m.Id != 0 && m.Name.Length > 0).ToDictionary(m => m.Id);
        foreach (var map in mapRows.Values)
            yield return new PlaceEntry($"z:{map.Id}", PlaceKind.Zone, map.Name + (map.Layer.Length > 0 ? " · " + map.Layer : ""),
                Group(map), map.Id, map.Territory, Aliases: map.Aliases, Expansion: map.Expansion);
        var rows = aetherytes.ToArray();
        var hubs = rows.Where(a => a.IsHub && a.Group != 0).GroupBy(a => a.Group).ToDictionary(g => g.Key, g => g.First());
        foreach (var row in rows) {
            hubs.TryGetValue(row.Group, out var hub);
            var mapId = row.Map != 0 ? row.Map : hub?.Map ?? 0;
            if (!mapRows.TryGetValue(mapId, out var map) || row.Name.Length == 0) continue;
            yield return new PlaceEntry($"a:{row.Id}", PlaceKind.Aetheryte, row.Name,
                Group(map) + (!row.IsHub && hub != null ? " > " + hub.Name : ""),
                mapId, row.Territory != 0 ? row.Territory : map.Territory, row.World,
                row.Id, row.IsHub ? row.Id : hub?.Id ?? row.Id, row.Aliases, map.Expansion);
        }
    }
}
