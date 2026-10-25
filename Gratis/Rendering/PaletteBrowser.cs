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

    public static PaletteExpansion[] Expansions(IEnumerable<PlaceEntry> entries) =>
        entries.Where(p => p.Kind is PlaceKind.Zone or PlaceKind.Aetheryte).GroupBy(p => p.Expansion).OrderBy(g =>
            g.Key).Select(g =>
            new PaletteExpansion(g.Key, Expansion(g.First()),
                g.OrderByDescending(p => p.ArtPath.Length > 0).ThenBy(p => p.Kind != PlaceKind.Zone).ThenBy(p => p.Id,
                    StringComparer.Ordinal).First(),
                g.Count(), g.Select(p => p.MapId).Distinct().Count())).ToArray();

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
        var generic = place.SearchName is "aetheryte" or "aethernet" or "neutral" or "auto";
        if (!generic && !results.Any(p => p.Place.Id != place.Id && p.Place.Name == place.Name))
            return place.Name;
        var area = place.Subtitle.Split('>', StringSplitOptions.TrimEntries
            | StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        var name = generic && area is { Length: > 0 } && area != "Current map" ? area + " · " + place.Name : place.Name;
        var suffix = result.Distance is { } d ? $" · {d:0}y" : "";
        var equalDistance = result.Distance == null
            || results.Any(p => p.Place.Id != place.Id && p.Place.Name == place.Name && p.Distance is { } other
                && MathF.Round(other) == MathF.Round(result.Distance.Value));
        if (equalDistance)
            suffix += place.World is { } w ? $" · {w.X:0}, {w.Y:0}" : $" · Map {place.MapId} · {place.Id}";
        return name + suffix;
    }

    public static string Detail(PlaceEntry place) {
        var detail = place.Subtitle;
        var expansion = Expansion(place);
        if (detail.StartsWith(expansion + " > ", StringComparison.Ordinal))
            detail = detail[(expansion.Length + 3)..];
        return detail + "  ·  " + expansion;
    }
}