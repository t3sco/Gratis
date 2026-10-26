using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Gratis.Controllers;

namespace Gratis.Rendering;

internal sealed class PaletteRowNames {
    private sealed class Names {
        public CultureInfo? Culture;
        public readonly Dictionary<PlaceResult, string> Rows = [];
    }

    private readonly ConditionalWeakTable<PlaceResult[], Names> snapshots = new();

    public string Get(PlaceResult result, PlaceResult[] results) {
        var names = snapshots.GetValue(results, static _ => new());
        var culture = CultureInfo.CurrentCulture;
        if (!ReferenceEquals(names.Culture, culture) || !culture.IsReadOnly || !culture.NumberFormat.IsReadOnly) {
            names.Culture = culture;
            names.Rows.Clear();
        }
        if (!names.Rows.TryGetValue(result, out var name)) {
            name = PaletteBrowser.Name(result, results);
            names.Rows.Add(result, name);
        }
        return name;
    }
}
