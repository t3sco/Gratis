using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Gratis.Controllers;

namespace Gratis.Rendering;

internal readonly record struct PaletteRowText(string Name, string Detail, string Expansion, string Distance,
    string Coordinates, string Identifier);

internal sealed class PaletteRowNames {
    private readonly Dictionary<(int, PlaceKind), string> counts = [];
    private readonly Dictionary<(string, bool, float), ActionLabelCache> fitted = [];

    public string Fit(string text, float width, bool secondary = false) {
        if (!fitted.TryGetValue((text, secondary, width), out var cache)) {
            cache = new();
            fitted.Add((text, secondary, width), cache);
        }
        return cache.Fit(text, width);
    }

    public string Count(int count, PlaceKind kind) {
        if (!counts.TryGetValue((count, kind), out var text)) {
            text = PaletteBrowser.Count(count, kind);
            counts.Add((count, kind), text);
        }
        return text;
    }

    private sealed class Names {
        public CultureInfo? Culture;
        public readonly Dictionary<PlaceResult, PaletteRowText> Rows = [];
        public readonly Dictionary<uint, string> Costs = [];
    }

    private readonly ConditionalWeakTable<PlaceResult[], Names> snapshots = new();

    private Names For(PlaceResult[] results) {
        var names = snapshots.GetValue(results, static _ => new());
        var culture = CultureInfo.CurrentCulture;
        if (!ReferenceEquals(names.Culture, culture) || !culture.IsReadOnly || !culture.NumberFormat.IsReadOnly) {
            names.Culture = culture;
            names.Rows.Clear();
            names.Costs.Clear();
        }
        return names;
    }

    public string ActionLabel(PlaceResult result, PlaceResult[] results) {
        if (result.Access.Cost is not { } cost) return "Teleport";
        var names = For(results);
        if (!names.Costs.TryGetValue(cost, out var label)) {
            label = $"{cost:N0} gil";
            names.Costs.Add(cost, label);
        }
        return label;
    }

    public string Get(PlaceResult result, PlaceResult[] results) => Text(result, results).Name;

    public PaletteRowText Text(PlaceResult result, PlaceResult[] results) {
        var names = For(results);
        if (!names.Rows.TryGetValue(result, out var name)) {
            var coordinates = PaletteBrowser.Coordinates(result, results);
            name = new(PaletteBrowser.Name(result, results), PaletteBrowser.Detail(result.Place),
                PaletteBrowser.Expansion(result.Place), result.Distance is { } distance ? $"{distance:0}y" : "",
                coordinates, result.Place.World == null && coordinates.Length > 0 ? result.Place.Id : "");
            names.Rows.Add(result, name);
        }
        return name;
    }
}
