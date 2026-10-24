using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Gratis.Controllers;

namespace Gratis.Rendering;

public sealed record UserPin
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public uint MapId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public string Title { get; set; } = "New pin";
    public string Note { get; set; } = "";
    public int Colour { get; set; }
    public int Version { get; set; } = 1;
    public Vector2 World => new(X, Y);
}

public enum PinChange { Saved, Duplicate, Limit, Invalid }

public sealed class PinStore
{
    private readonly MapDrawingConfig config;
    private readonly Action save;
    private readonly Func<uint, bool> validMap;
    public int Revision { get; private set; }
    public int Limit => Math.Clamp(config.PinMaxPerMap, 1, 200);
    public PinStore(MapDrawingConfig config, Action save, Func<uint, bool>? validMap = null)
    {
        this.config = config; this.save = save;
        this.validMap = validMap ?? (id => id is > 0 and <= 100000);
        var clean = new Dictionary<uint, List<UserPin>>();
        var ids = new HashSet<Guid>();
        if (config.PinData != null) foreach (var pair in config.PinData.Take(2048)) {
            if (!this.validMap(pair.Key) || pair.Value == null) continue;
            var pins = new List<UserPin>();
            foreach (var raw in pair.Value) {
                if (raw == null) continue;
                var pin = Sanitize(raw);
                if (pin.MapId != pair.Key || !Valid(pin) || !ids.Add(pin.Id)) continue;
                if (pins.Count == Limit) break;
                pins.Add(pin);
            }
            if (pins.Count > 0) clean[pair.Key] = pins;
        }
        config.PinData = clean;
        if (!Enum.IsDefined(config.PinLabels)) config.PinLabels = PinLabelMode.OnHover;
    }
    public IReadOnlyList<UserPin> ForMap(uint mapId) => config.PinData.TryGetValue(mapId, out var list) ? list : Array.Empty<UserPin>();
    public IEnumerable<PlaceEntry> Entries => !config.Pins ? [] : config.PinData.Values.SelectMany(p => p).Select(Entry);
    public static PlaceEntry Entry(UserPin pin) => new("pin:" + pin.Id.ToString("N"), PlaceKind.Pin,
        pin.Title, "Saved pin" + (pin.Note.Length == 0 ? "" : " > " + pin.Note.Replace('\n', ' ')), pin.MapId, 0, pin.World);
    public bool Contains(UserPin pin) => ForMap(pin.MapId).Any(p => Equivalent(p, Sanitize(pin)));
    public PinChange Put(UserPin raw)
    {
        var pin = Sanitize(raw);
        if (!Valid(pin) || !validMap(pin.MapId)) return PinChange.Invalid;
        if (!config.PinData.TryGetValue(pin.MapId, out var list)) config.PinData[pin.MapId] = list = [];
        var index = list.FindIndex(p => p.Id == pin.Id);
        if (list.Any(p => p.Id != pin.Id && Equivalent(p, pin))) return PinChange.Duplicate;
        if (index < 0 && list.Count >= Limit) return PinChange.Limit;
        if (index < 0) list.Add(pin); else list[index] = pin;
        Revision++; save(); return PinChange.Saved;
    }
    public bool Delete(UserPin pin)
    {
        if (!config.PinData.TryGetValue(pin.MapId, out var list) || list.RemoveAll(p => p.Id == pin.Id) == 0) return false;
        if (list.Count == 0) config.PinData.Remove(pin.MapId);
        Revision++; save(); return true;
    }
    public static bool Valid(UserPin? pin) => pin != null && pin.Id != Guid.Empty && pin.Version == 1 &&
        pin.MapId is > 0 and <= 100000 && float.IsFinite(pin.X) && float.IsFinite(pin.Y) &&
        MathF.Abs(pin.X) <= 100000 && MathF.Abs(pin.Y) <= 100000 && pin.Colour is >= 0 and < 8 &&
        pin.Title is { Length: > 0 and <= 80 } && pin.Note is { Length: <= 280 };
    public static UserPin Sanitize(UserPin pin) => pin with {
        Id = pin.Id == Guid.Empty ? Guid.NewGuid() : pin.Id,
        Title = Text(pin.Title, 80, false) is { Length: > 0 } title ? title : "Untitled pin",
        Note = Text(pin.Note, 280, true),
    };
    public static string Text(string? value, int limit, bool lines)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var result = new StringBuilder(Math.Min(value.Length, limit));
        foreach (var rune in value.EnumerateRunes()) {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format) {
                if (lines && rune.Value == '\n' && result.Length < limit) result.Append('\n');
                continue;
            }
            if (result.Length + rune.Utf16SequenceLength > limit) break;
            result.Append(rune.ToString());
        }
        return result.ToString().Trim();
    }
    private static bool Equivalent(UserPin a, UserPin b) => a.MapId == b.MapId &&
        Vector2.DistanceSquared(a.World,b.World) < .0001f && a.Title == b.Title && a.Note == b.Note && a.Colour == b.Colour;
}
