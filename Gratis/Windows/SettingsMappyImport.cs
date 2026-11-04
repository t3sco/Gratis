using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Unicode;
using Gratis.Data;

namespace Gratis.Windows;

public enum MappyImportStatus { Ready, NotFound, Unreadable }

/// <summary>
/// What a read of Mappy's settings found. <see cref="Changes"/> counts settings that would differ from the current
/// ones (settings with no visible effect are left out), <see cref="Adjusted"/> how many of those were clamped to
/// Gratis's range, and <see cref="Groups"/> names the kinds of setting that would change.
/// </summary>
public sealed record MappyImportScan(MappyImportStatus Status, ProfileData? Data = null, int Changes = 0,
    int Adjusted = 0, IReadOnlyList<string>? Groups = null);

/// <summary>
/// One-off, read-only import of the mapped settings in Mappy's System.config.json. Only the allowlisted keys are read,
/// each value goes through the same type and range check as a pasted profile, and only those settings are written;
/// everything else in Gratis stays as it is. Nothing runs unless the user presses the button.
/// </summary>
public static class MappyImport {
    public const int MaxBytes = 256 * 1024, MaxKeys = 128;
    public const string FolderName = "Mappy", FileName = "System.config.json";

    /// <summary>Gratis's own config directory; Mappy's sits beside it. The game sets this, the harness injects a fixture.</summary>
    public static Func<string?> ConfigDirectory { get; set; } =
#if GRATIS_PREVIEW
        () => null;
#else
        () => Service.PluginInterface.ConfigDirectory.FullName;
#endif

    // The allowlist, grouped for the preview. Everything not listed here is ignored, including window geometry and
    // the character identity fields.
    private static readonly (string Group, string[] Keys)[] Allowed = [
        ("behaviour", ["UseLinearZoom", "ShowMiscTooltips", "HideWithGameGui", "HideBetweenAreas", "HideInCombat",
            "KeepOpen", "FollowOnOpen", "FollowPlayer", "ScalePlayerCone", "ShowRadar", "ShowRadarInDuties",
            "EnableShiftDragMove", "LockWindow", "ScaleWithZoom", "CenterOnFlag", "CenterOnGathering", "CenterOnQuest",
            "LockCenterOnMap", "ZoomLocked", "ShowPlayers", "ShowPlayerIcon", "NoFocusOnAppear",
            "SuppressNativeMapOpenSound", "CenterOnOpen", "ZoomSpeed"]),
        ("window and toolbar", ["HideWindowFrame", "HideWindowBackground", "AlwaysShowToolbar", "ShowToolbarOnHover",
            "ShowCoordinateBar", "FadeMode", "FadePercent", "ToolbarFade", "CoordinateBarFade"]),
        ("labels", ["ShowRegionLabel", "ShowMapLabel", "ShowAreaLabel", "ShowSubAreaLabel", "ShowTextLabels",
            "ScaleTextWithZoom", "LargeAreaTextScale", "SmallAreaTextScale"]),
        ("colours", ["RadarColor", "RadarOutlineColor", "AreaColor", "AreaOutlineColor", "PlayerConeColor",
            "PlayerConeOutlineColor", "CoordinateTextColor"]),
        ("icon and cone sizes", ["IconScale", "PlayerIconScale", "ConeSize"]),
    ];

    /// <summary>Gratis ignores these in its own frames, so they are copied but never counted as a visible change.</summary>
    private static readonly HashSet<string> Quiet = ["ToolbarFade", "CoordinateBarFade"];
    private static readonly HashSet<string> Colours = new(Allowed.First(g => g.Group == "colours").Keys);

    private static readonly HashSet<string> AllowedSet = new(Allowed.SelectMany(g => g.Keys));

    public static IEnumerable<string> AllowedKeys => AllowedSet;

    /// <summary>Where Mappy keeps its settings, or null when Gratis's own directory is unknown.</summary>
    public static string? SourcePath() {
        try {
            var own = ConfigDirectory();
            if (string.IsNullOrWhiteSpace(own))
                return null;
            var parent = Directory.GetParent(Path.TrimEndingDirectorySeparator(own));
            return parent == null ? null : Path.Combine(parent.FullName, FolderName, FileName);
        }
        catch (Exception) {
            return null;
        }
    }

    /// <summary>Reads Mappy's settings file (read-only) and works out what would change. Never throws.</summary>
    public static MappyImportScan Scan(SystemConfig config) {
        try {
            var path = SourcePath();
            if (path == null || !File.Exists(path))
                return new(MappyImportStatus.NotFound);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > MaxBytes)
                return new(MappyImportStatus.Unreadable);
            var buffer = new byte[MaxBytes + 1];
            var total = 0;
            int read;
            while (total < buffer.Length && (read = stream.Read(buffer, total, buffer.Length - total)) > 0)
                total += read;
            return total > MaxBytes ? new(MappyImportStatus.Unreadable) : Parse(buffer.AsSpan(0, total), config);
        }
        catch (Exception) {
            return new(MappyImportStatus.Unreadable);
        }
    }

    /// <summary>Checks the file's bytes against the allowlist. Anything off about the file reads as unreadable.</summary>
    public static MappyImportScan Parse(ReadOnlySpan<byte> bytes, SystemConfig config) {
        try {
            if (bytes.Length > MaxBytes)
                return new(MappyImportStatus.Unreadable);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                bytes = bytes[3..];
            if (!Utf8.IsValid(bytes))
                return new(MappyImportStatus.Unreadable);
            using var document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 4 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new(MappyImportStatus.Unreadable);
            var entries = ProfileCodec.Portable.ToDictionary(e => e.Path);
            var defaults = new SystemConfig();
            var values = new Dictionary<string, JsonElement>();
            var clamped = new HashSet<string>();
            var seen = new HashSet<string>();
            var inspected = 0;
            foreach (var property in document.RootElement.EnumerateObject()) {
                if (++inspected > MaxKeys)
                    return new(MappyImportStatus.Unreadable); // a settings file never has this many keys
                if (!AllowedSet.Contains(property.Name) || !entries.TryGetValue(property.Name, out var entry))
                    continue;
                if (!seen.Add(property.Name))
                    return new(MappyImportStatus.Unreadable); // the same setting twice: nothing safe to pick
                var element = property.Value;
                if (Colours.Contains(property.Name) && !TryColour(element, out element))
                    continue;
                if (!ProfileCodec.TryRead(entry, entry.Value(defaults), element, out var value))
                    continue;
                values[property.Name] = element.Clone();
                if (WasClamped(element, value))
                    clamped.Add(property.Name);
            }
            if (values.Count == 0)
                return new(MappyImportStatus.Unreadable);
            var data = new ProfileData("Mappy import", values);
            var changed = new List<string>();
            ProfileCodec.Merge(config, data, false, changed);
            var visible = changed.Where(p => !Quiet.Contains(p)).ToList();
            var groups = Allowed.Where(g => g.Keys.Any(visible.Contains)).Select(g => g.Group).ToArray();
            return new(MappyImportStatus.Ready, data, visible.Count, visible.Count(clamped.Contains), groups);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException
            or FormatException or OverflowException) {
            return new(MappyImportStatus.Unreadable);
        }
    }

    /// <summary>
    /// Writes the scanned settings and nothing else, keeping one undo point. Returns how many visible settings changed;
    /// when that is none the settings and the earlier undo point are left alone.
    /// </summary>
    public static int Apply(SystemConfig config, ProfileData data) {
        var changed = new List<string>();
        ProfileCodec.Merge(config, data, false, changed);
        var visible = changed.Count(p => !Quiet.Contains(p));
        if (visible == 0)
            return 0;
        var before = ProfileCodec.Encode("Before Mappy import", ProfileCodec.Capture(config));
        ProfileCodec.Merge(config, data);
        config.ProfileOptions.Undo = before;
        return visible;
    }

    /// <summary>Mappy writes a colour as {"X":..,"Y":..,"Z":..,"W":..}; the profile codec reads [x,y,z,w].</summary>
    private static bool TryColour(JsonElement element, out JsonElement array) {
        array = element;
        if (element.ValueKind == JsonValueKind.Array)
            return true;
        if (element.ValueKind != JsonValueKind.Object)
            return false;
        var parts = new float[4];
        var found = 0;
        foreach (var property in element.EnumerateObject()) {
            var at = property.Name switch { "X" => 0, "Y" => 1, "Z" => 2, "W" => 3, _ => -1 };
            if (at < 0 || property.Value.ValueKind != JsonValueKind.Number
                || !property.Value.TryGetSingle(out parts[at]) || !float.IsFinite(parts[at]))
                return false;
            if ((found & 1 << at) != 0)
                return false; // a component given twice: nothing safe to pick
            found |= 1 << at;
        }
        if (found != 15)
            return false;
        array = JsonSerializer.SerializeToElement(parts);
        return true;
    }

    private static bool WasClamped(JsonElement raw, object value) => value switch {
        float f => raw.TryGetSingle(out var r) && r != f,
        int i => raw.TryGetInt32(out var r) && r != i,
        Enum e => raw.TryGetInt32(out var r) && r != Convert.ToInt32(e),
        Vector4 v => raw.ValueKind == JsonValueKind.Array && raw.EnumerateArray().Select(x => x.GetSingle()).ToArray()
            is { Length: 4 } c && (c[0] != v.X || c[1] != v.Y || c[2] != v.Z || c[3] != v.W),
        _ => false,
    };
}
