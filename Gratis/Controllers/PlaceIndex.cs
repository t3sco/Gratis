using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gratis.Controllers;

public enum PlaceKind { All, Aetheryte, Zone, Quest, Marker, Flag, Pin }
public readonly record struct PlaceAccess(bool Attuned, uint? Cost = null);

public sealed record PlaceEntry(string Id, PlaceKind Kind, string Name, string Subtitle,
    uint MapId, uint TerritoryId, Vector2? World = null, uint AetheryteId = 0,
    uint HubId = 0, string Aliases = "", uint Expansion = 0)
{
    public string SearchName { get; } = PlaceSearch.Normalize(Name);
    public string SearchAliases { get; } = PlaceSearch.Normalize(Aliases);
    public string SearchContext { get; } = PlaceSearch.Normalize(Subtitle);
    public bool IsShard => Kind == PlaceKind.Aetheryte && HubId != 0 && HubId != AetheryteId;
}

public sealed class PlaceIndex : IDisposable
{
    private PlaceEntry[] entries = [];
    private readonly CancellationTokenSource cancellation = new();
    private Task? worker;
    private int revision;
    public PlaceEntry[] Entries => Volatile.Read(ref entries);
    public int Revision => Volatile.Read(ref revision);
    public bool Building => worker is { IsCompleted: false };
    public string? Error { get; private set; }

    public void Start(Func<CancellationToken, IEnumerable<PlaceEntry>> build, Action<int, double>? complete = null,
        Action<Exception>? failed = null)
    {
        if (worker != null) return;
        worker = Task.Run(() => {
            var watch = global::System.Diagnostics.Stopwatch.StartNew();
            try {
                var accumulated = new List<PlaceEntry>();
                foreach (var entry in build(cancellation.Token)) {
                    cancellation.Token.ThrowIfCancellationRequested();
                    accumulated.Add(entry);
                    if (accumulated.Count % 128 == 0) Publish(accumulated.ToArray());
                }
                Publish(accumulated.ToArray());
                complete?.Invoke(accumulated.Count, watch.Elapsed.TotalMilliseconds);
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { Error = error.Message; failed?.Invoke(error); }
        }, cancellation.Token);
    }

    public void Publish(PlaceEntry[] snapshot) { Volatile.Write(ref entries, snapshot); Interlocked.Increment(ref revision); }
    public void Dispose() { cancellation.Cancel();  }
}

public readonly record struct PlaceSearchContext(uint MapId, uint TerritoryId, Vector2? Player);
public readonly record struct PlaceResult(PlaceEntry Place, double Score, PlaceAccess Access, float? Distance);

public static class PlaceSearch
{
    public static string Normalize(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (var c in value.Normalize(NormalizationForm.FormKD)) {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                result.Append(char.ToLowerInvariant(c));
        }
        return result.ToString();
    }

    public static (PlaceKind Scope, string Query) Parse(string query, PlaceKind scope)
    {
        query = query.Trim();
        if (query.Length >= 2 && query[1] == ':') {
            var prefix = char.ToLowerInvariant(query[0]) switch {
                'a' => PlaceKind.Aetheryte, 'z' => PlaceKind.Zone,
                'q' => PlaceKind.Quest, 'm' => PlaceKind.Marker, _ => PlaceKind.All,
            };
            if (prefix != PlaceKind.All) return (prefix, query[2..].Trim());
        }
        return (scope, query);
    }

    public static double Score(string needle, string haystack)
    {
        if (needle.Length == 0) return 0;
        var offset = 0; var previous = -2; double score = 0;
        foreach (var c in needle) {
            var at = haystack.IndexOf(c, offset);
            if (at < 0) return double.NegativeInfinity;
            score += 12 + (at == 0 || !char.IsLetterOrDigit(haystack[at - 1]) ? 18 : 0)
                + (at == previous + 1 ? 9 : 0) - Math.Min(at - offset, 12) * .6;
            previous = at; offset = at + 1;
        }
        if (haystack.StartsWith(needle, StringComparison.Ordinal)) score += 65;
        if (haystack == needle) score += 100;
        return score - Math.Min(haystack.Length - needle.Length, 100) * .12;
    }

    public static PlaceResult[] Find(IEnumerable<PlaceEntry> entries, string query, PlaceKind scope,
        PlaceSearchContext context, IReadOnlyDictionary<string, long> recent,
        Func<PlaceEntry, PlaceAccess> access, bool showUnattuned = true)
    {
        var parsed = Parse(query, scope);
        var tokens = Normalize(parsed.Query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var found = new List<PlaceResult>();
        foreach (var place in entries) {
            if (parsed.Scope != PlaceKind.All && place.Kind != parsed.Scope) continue;
            if (place.Kind == PlaceKind.Marker && place.MapId != context.MapId) continue;
            var state = access(place);
            if (!showUnattuned && place.Kind == PlaceKind.Aetheryte && !state.Attuned) continue;
            double score = 0;
            foreach (var token in tokens) {
                var match = Math.Max(Score(token, place.SearchName),
                    Math.Max(Score(token, place.SearchAliases) - 32, Score(token, place.SearchContext) - 45));
                if (double.IsNegativeInfinity(match)) { score = match; break; }
                score += match;
            }
            if (double.IsNegativeInfinity(score)) continue;
            float? distance = place.MapId == context.MapId && context.Player is { } player && place.World is { } world
                ? Vector2.Distance(player, world) : null;
            score += place.MapId == context.MapId ? 20 : place.TerritoryId == context.TerritoryId ? 10 : 0;
            if (distance is { } d) score += 5 / (1 + d / 100);
            if (tokens.Length == 0 && place.Kind == PlaceKind.Aetheryte) score += 4;
            if (recent.TryGetValue(place.Id, out var rank)) score += 8 + Math.Min(rank, 1000) / 1000d;
            found.Add(new PlaceResult(place, score, state, distance));
        }
        return found.OrderByDescending(result => result.Score)
            .ThenBy(result => result.Place.SearchName, StringComparer.Ordinal)
            .ThenBy(result => result.Place.Id, StringComparer.Ordinal).ToArray();
    }
}
