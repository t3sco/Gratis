using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Gratis.Rendering;

public static class PinShareCode {
    public const string Prefix = "gratis-pin:";
    public const int MaxLength = 4096;
    public static string Encode(UserPin pin) {
        pin = PinStore.Sanitize(pin);
        if (!PinStore.Valid(pin))
            throw new ArgumentException("Invalid pin.", nameof(pin));
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })) {
            json.WriteStartObject();
            json.WriteNumber("v", 1);
            json.WriteNumber("m", pin.MapId);
            json.WriteNumber("x", pin.X);
            json.WriteNumber("y", pin.Y);
            json.WriteString("t", pin.Title);
            json.WriteString("n", pin.Note);
            json.WriteNumber("c", pin.Colour);
            json.WriteEndObject();
        }
        var bytes = stream.ToArray();
        return Prefix + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_') + "."
            + Convert.ToHexString(SHA256.HashData(bytes).AsSpan(0, 8)).ToLowerInvariant();
    }

    public static bool TryDecode(string? code, out UserPin? pin) {
        pin = null;
        if (code == null || code.Length > MaxLength || !code.StartsWith(Prefix, StringComparison.Ordinal))
            return false;
        try {
            var dot = code.LastIndexOf('.');
            if (dot <= Prefix.Length || code.Length - dot != 17)
                return false;
            var payload = code[Prefix.Length..dot];
            foreach (var c in payload)
                if (!(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'))
                    return false;
            var base64 = payload.Replace('-', '+').Replace('_', '/');
            var bytes = Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '='));
            if (bytes.Length > 2048)
                return false;
            if (Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_') != payload)
                return false;
            var checksum = Convert.FromHexString(code[(dot + 1)..]);
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes).AsSpan(0, 8), checksum))
                return false;
            using var document = JsonDocument.Parse(bytes, new() { MaxDepth = 2 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;
            var fields = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject()) {
                if (property.Name is not ("v" or "m" or "x" or "y" or "t" or "n" or "c") || !fields.Add(property.Name))
                    return false;
            }
            if (fields.Count != 7 || !root.GetProperty("v").TryGetInt32(out var version) || version != 1
                || !root.GetProperty("m").TryGetUInt32(out var map) || !root.GetProperty("x").TryGetSingle(out var x)
                || !root.GetProperty("y").TryGetSingle(out var y) || !root.GetProperty("c").TryGetInt32(out var colour)
                || root.GetProperty("t").ValueKind != JsonValueKind.String
                || root.GetProperty("n").ValueKind != JsonValueKind.String)
                return false;
            var title = root.GetProperty("t").GetString()!;
            var note = root.GetProperty("n").GetString()!;
            if (title.Length > 80 || note.Length > 280)
                return false;
            var decoded = new UserPin {
                MapId = map,
                X = x,
                Y = y,
                Title = title,
                Note = note,
                Colour = colour,
                Version = version
            };
            if (!PinStore.Valid(decoded))
                return false;
            pin = PinStore.Sanitize(decoded);
            return true;
        }
        catch { return false; }
    }
}