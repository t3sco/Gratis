using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Gratis.Classes;
using Gratis.Rendering;

namespace Gratis.Controllers;

public static class NearestPlaces {
    public static readonly (MarkerIconFamily Family, string[] Aliases)[] Services = [
        (MarkerIconFamily.Inn, ["inn", "innkeeper", "inn room"]),
        (MarkerIconFamily.Market, ["market board", "market", "marketboard"]),
        (MarkerIconFamily.Mender, ["mender", "repair", "repairs"]),
        (MarkerIconFamily.Aetheryte, ["aetheryte", "crystal", "teleport"]),
        (MarkerIconFamily.Aethernet, ["aethernet shard", "aethernet", "shard"]),
        (MarkerIconFamily.Bell, ["retainer bell", "summoning bell", "bell", "retainer"]),
        (MarkerIconFamily.Mail, ["mailbox", "mail", "delivery moogle"]),
        (MarkerIconFamily.Shop, ["vendor", "shop", "merchant"]),
        (MarkerIconFamily.Stable, ["chocobo stabler", "chocobo stable", "stable", "stabler"]),
    ];

    public static bool Parse(string text, out string query) {
        text = text.Trim();
        if (text.StartsWith("n:", StringComparison.OrdinalIgnoreCase)) {
            query = text[2..].Trim();
            return true;
        }
        if (text.Equals("nearest", StringComparison.OrdinalIgnoreCase)) {
            query = "";
            return true;
        }
        if (text.StartsWith("nearest ", StringComparison.OrdinalIgnoreCase)) {
            query = text[8..].Trim();
            return true;
        }
        query = "";
        return false;
    }

    public static MarkerIconFamily? Category(string query) {
        query = PlaceSearch.Normalize(query.Trim());
        if (query.Length == 0)
            return null;
        MarkerIconFamily? best = null;
        var score = double.NegativeInfinity;
        foreach (var item in Services)
            foreach (var alias in item.Aliases) {
                var candidate = PlaceSearch.Score(query, alias);
                if (candidate > score) {
                    best = item.Family;
                    score = candidate;
                }
            }
        foreach (var family in Enum.GetValues<MarkerIconFamily>()) {
            if (family is MarkerIconFamily.Auto)
                continue;
            var candidate = PlaceSearch.Score(query, PlaceSearch.Normalize(family.ToString()));
            if (candidate > score) {
                best = family;
                score = candidate;
            }
        }
        return best;
    }

    public static MarkerIconFamily Family(PlaceEntry place) {
        if (place.Kind == PlaceKind.Aetheryte)
            return place.IsShard ? MarkerIconFamily.Aethernet : MarkerIconFamily.Aetheryte;
        if (place.Icon != 0)
            return MarkerIcon.Family(new MarkerInfo {
                Position = default, Offset = default, Scale = 1,
                IconId = place.Icon
            });
        var text = place.SearchName + " " + place.SearchAliases;
        foreach (var item in Services)
            if (item.Aliases.Any(alias => text.Contains(alias, StringComparison.Ordinal)))
                return item.Family;
        return place.Kind == PlaceKind.Quest ? MarkerIconFamily.Quest : MarkerIconFamily.Neutral;
    }

    public static PlaceResult[] Find(IEnumerable<PlaceEntry> entries, string query, PlaceSearchContext context,
        Func<PlaceEntry, PlaceAccess> access, bool showUnattuned = true) {
        if (context.Player is not { } player)
            return [];
        var family = Category(query);
        var normalized = PlaceSearch.Normalize(query);
        var category = Enum.GetValues<MarkerCategory>().Where(c => c != MarkerCategory.Auto).Cast<MarkerCategory
            ?>().FirstOrDefault(c => PlaceSearch.Normalize(c!.Value.ToString()) == normalized
                || PlaceSearch.Normalize(FlatMarkerDrawing.Label(c.Value)) == normalized);
        var group = Array.FindIndex(MarkerVisibility.Names, n => PlaceSearch.Normalize(n) == normalized);
        bool MatchesQuery(PlaceEntry place) {
            if (query.Length == 0)
                return true;
            if (category is { } selectedCategory && MarkerIcon.Category(Family(place)) == selectedCategory)
                return true;
            if (group >= 0) {
                var marker = new MarkerInfo {
                    Position = default, Offset = default, Scale = 1,
                    IconFamily = Family(place), };
                if (MarkerVisibility.Group(marker) == (MarkerGroup)group)
                    return true;
            }
            if (category != null || group >= 0)
                return false;
            return family is { } selectedFamily ? Family(place) == selectedFamily
                : !double.IsNegativeInfinity(PlaceSearch.Score(normalized,
                    place.SearchName + " " + place.SearchAliases));
        }
        return entries.Where(p => p.MapId == context.MapId && p.World is { } w && float.IsFinite(w.X)
            && float.IsFinite(w.Y)
            && p.Kind != PlaceKind.Zone).Where(MatchesQuery).Select(p =>
                new PlaceResult(p, 0, access(p), Vector2.Distance(player, p.World!.Value))).Where(r => showUnattuned
            || r.Place.Kind != PlaceKind.Aetheryte
            || r.Access.Attuned).DistinctBy(r =>
                (Family(r.Place), MathF.Round(r.Place.World!.Value.X),
                    MathF.Round(r.Place.World!.Value.Y))).OrderBy(r =>
                r.Distance).ThenBy(r => r.Place.SearchName,
            StringComparer.Ordinal).ThenBy(r => r.Place.Id, StringComparer.Ordinal).ToArray();
    }

    public static string Direction(Vector2 from, Vector2 to) {
        var d = to - from;
        if (d.LengthSquared() < .01f)
            return "•";
        string[] arrows = ["→", "↘", "↓", "↙", "←", "↖", "↑", "↗"];
        return arrows[((int)MathF.Round(MathF.Atan2(d.Y, d.X) / (MathF.PI / 4)) + 8) % 8];
    }
}