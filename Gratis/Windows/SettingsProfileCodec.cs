using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Gratis.Data;
using Gratis.Rendering;

namespace Gratis.Windows;

public sealed record ProfileData(string Name, Dictionary<string, JsonElement> Values);

/// <summary>
/// A profile is the set of portable settings that differ from the defaults, shared as a GRATIS1: string.
/// Only settings in the settings catalog travel, and every value is checked against the live setting's type
/// and range when read, so a pasted string can never write anything else.
/// </summary>
public static class ProfileCodec {
    public const string Prefix = "GRATIS1:";
    private const int MaxJson = 65536, MaxValues = 1024;
    private static readonly HashSet<string> Excluded = [
        "Profiles", "QuickSetupDone", "WindowPresets", "ActiveLens", "CustomLenses", "LensCycleKey", "TextPixelSnap",
        "TextOversampleH", "PvpMapOnly", "DebugMode", "WindowPosition", "WindowSize",
    ];
    private static SettingsEntry[]? portable;

    public static IReadOnlyList<SettingsEntry> Portable => portable ??= Build();

    private static SettingsEntry[] Build() {
        var defaults = new SystemConfig();
        return SettingsLayout.Entries.Where(e => !Excluded.Contains(e.Path) && !e.Path.StartsWith("Filters.", StringComparison.Ordinal)).GroupBy(e => e.Path)
            .Select(g => g.First()).Where(e => e.Value(defaults) is bool or int or float or string or Enum or Vector2
                or Vector4).ToArray();
    }

    public static Dictionary<string, JsonElement> Capture(SystemConfig config) {
        var defaults = new SystemConfig();
        var values = new Dictionary<string, JsonElement>();
        foreach (var entry in Portable) {
            var value = entry.Value(config);
            if (Equals(value, entry.Value(defaults)) || !Finite(value))
                continue;
            values[entry.Path] = ToElement(value);
        }
        return values;
    }

    private static bool Finite(object value) => value switch {
        float f => float.IsFinite(f),
        Vector2 v => float.IsFinite(v.X) && float.IsFinite(v.Y),
        Vector4 v => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z) && float.IsFinite(v.W),
        _ => true,
    };

    private static JsonElement ToElement(object value) {
        object plain = value switch {
            Enum e => Convert.ToInt32(e),
            Vector2 v => new[] { v.X, v.Y },
            Vector4 v => new[] { v.X, v.Y, v.Z, v.W },
            _ => value,
        };
        return JsonSerializer.SerializeToElement(plain, plain.GetType());
    }

    public static string Encode(string name, IReadOnlyDictionary<string, JsonElement> values) {
        using var json = new MemoryStream();
        using (var writer = new Utf8JsonWriter(json)) {
            writer.WriteStartObject();
            writer.WriteString("n", name);
            writer.WriteStartObject("v");
            foreach (var pair in values.OrderBy(p => p.Key, StringComparer.Ordinal)) {
                writer.WritePropertyName(pair.Key);
                pair.Value.WriteTo(writer);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        using var packed = new MemoryStream();
        using (var deflate = new DeflateStream(packed, CompressionLevel.SmallestSize, true))
            deflate.Write(json.GetBuffer(), 0, (int)json.Length);
        return Prefix + Convert.ToBase64String(packed.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool TryDecode(string? text, out ProfileData? data, out string error) {
        data = null;
        text = text?.Trim() ?? "";
        if (text.Length == 0) {
            error = "The clipboard is empty.";
            return false;
        }
        if (!text.StartsWith(Prefix, StringComparison.Ordinal)) {
            error = "That is not a Gratis profile.";
            return false;
        }
        if (text.Length > SettingsProfileSet.MaxData) {
            error = "That profile is too large.";
            return false;
        }
        try {
            var base64 = text[Prefix.Length..].Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            using var deflate = new DeflateStream(new MemoryStream(Convert.FromBase64String(base64)),
                CompressionMode.Decompress);
            using var json = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0) {
                if (json.Length + read > MaxJson) {
                    error = "That profile is too large.";
                    return false;
                }
                json.Write(buffer, 0, read);
            }
            using var document = JsonDocument.Parse(json.ToArray(), new JsonDocumentOptions { MaxDepth = 4 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("v", out var list)
                || list.ValueKind != JsonValueKind.Object) {
                error = "That profile is damaged.";
                return false;
            }
            var values = new Dictionary<string, JsonElement>();
            foreach (var property in list.EnumerateObject()) {
                if (values.Count >= MaxValues) {
                    error = "That profile is too large.";
                    return false;
                }
                values[property.Name] = property.Value.Clone();
            }
            var name = root.TryGetProperty("n", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : "";
            data = new(SettingsProfileSet.CleanName(name, "Imported profile"), values);
            error = "";
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or JsonException
            or InvalidOperationException) {
            error = "That profile is damaged.";
            return false;
        }
    }

    /// <summary>Reads one value as the setting's own type, or fails. Numbers are clamped to the slider range.</summary>
    private static bool TryRead(SettingsEntry entry, object like, JsonElement element, out object value) {
        value = like;
        switch (like) {
            case bool when element.ValueKind is JsonValueKind.True or JsonValueKind.False:
                value = element.GetBoolean();
                return true;
            case float when element.ValueKind == JsonValueKind.Number && element.TryGetSingle(out var number)
                && float.IsFinite(number):
                value = entry.Path == "FlatMarkerSize"
                    ? Math.Clamp(number, entry.Minimum / 6, entry.Maximum / .2f) // stored value is the displayed size divided by icon scale
                    : Math.Clamp(number, entry.Minimum, entry.Maximum);
                return true;
            case int when element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var whole):
                if (entry.ChoiceCount > 0 && (whole < 0 || whole >= entry.ChoiceCount))
                    return false;
                value = entry.ChoiceCount > 0 ? whole : Math.Clamp(whole, (int)entry.Minimum, (int)entry.Maximum);
                return true;
            case Enum when element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var raw):
                return TryEnum(like.GetType(), raw, out value);
            case string when element.ValueKind == JsonValueKind.String:
                var text = element.GetString() ?? "";
                if (text.Length > 120 || text.Any(char.IsControl))
                    return false;
                value = text;
                return true;
            case Vector2 when Components(element, 2, out var pair):
                value = new Vector2(Math.Clamp(pair[0], entry.Minimum, entry.Maximum),
                    Math.Clamp(pair[1], entry.Minimum, entry.Maximum));
                return true;
            case Vector4 when Components(element, 4, out var colour):
                value = new Vector4(Math.Clamp(colour[0], 0, 1), Math.Clamp(colour[1], 0, 1),
                    Math.Clamp(colour[2], 0, 1), Math.Clamp(colour[3], 0, 1));
                return true;
        }
        return false;
    }

    private static bool Components(JsonElement element, int count, out float[] parts) {
        parts = new float[count];
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() != count)
            return false;
        var i = 0;
        foreach (var item in element.EnumerateArray())
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetSingle(out parts[i++]) || !float.IsFinite(parts[i - 1]))
                return false;
        return true;
    }

    private static bool TryEnum(Type type, int raw, out object value) {
        value = Enum.ToObject(type, raw);
        if (type.IsDefined(typeof(FlagsAttribute), false)) {
            var mask = Enum.GetValues(type).Cast<object>().Aggregate(0, (all, item) => all | Convert.ToInt32(item));
            value = Enum.ToObject(type, raw & mask);
            return true;
        }
        return Enum.IsDefined(type, value);
    }

    /// <summary>How many of the profile's settings this build recognises and accepts.</summary>
    public static int Recognised(ProfileData data) {
        var defaults = new SystemConfig();
        return Portable.Count(e => data.Values.TryGetValue(e.Path, out var element)
            && TryRead(e, e.Value(defaults), element, out _));
    }

    /// <summary>Makes the portable settings exactly defaults plus the profile. Returns how many changed.</summary>
    public static int Apply(SystemConfig config, ProfileData data) {
        var defaults = new SystemConfig();
        var changed = 0;
        foreach (var entry in Portable) {
            var fallback = entry.Value(defaults);
            var target = data.Values.TryGetValue(entry.Path, out var element) && TryRead(entry, fallback, element,
                out var parsed) ? parsed : fallback;
            if (Equals(entry.Value(config), target))
                continue;
            if (Equals(target, fallback))
                entry.Reset(config, defaults);
            else
                entry.Set(config, target);
            changed++;
        }
        return changed;
    }

    public static ProfileData Snapshot(SystemConfig config, string name) => new(name, Capture(config));
}
