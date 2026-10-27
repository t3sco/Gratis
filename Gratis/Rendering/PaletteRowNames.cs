using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Gratis.Controllers;

namespace Gratis.Rendering;

internal sealed class PaletteRowNames {
    private sealed class Names {
        public CultureInfo? Culture;
        public readonly Dictionary<PlaceResult, string> Rows = [];
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

    public string Get(PlaceResult result, PlaceResult[] results) {
        var names = For(results);
        if (!names.Rows.TryGetValue(result, out var name)) {
            name = PaletteBrowser.Name(result, results);
            names.Rows.Add(result, name);
        }
        return name;
    }
}
