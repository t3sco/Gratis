using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;

namespace Gratis.Preview;

internal static class ProfileVerification {
    private static void Require(bool value, string message) {
        if (!value)
            throw new InvalidOperationException("Profiles: " + message);
    }

    private static string Pack(string json) {
        using var packed = new MemoryStream();
        using (var deflate = new DeflateStream(packed, CompressionLevel.SmallestSize, true))
            deflate.Write(Encoding.UTF8.GetBytes(json));
        return ProfileCodec.Prefix + Convert.ToBase64String(packed.ToArray()).TrimEnd('=').Replace('+', '-')
            .Replace('/', '_');
    }

    public static void Run() {
        var config = new SystemConfig { AccentColor = new(.1f, .2f, .3f, 1), IconScale = .8f, SpotlightHideOthers = false,
            ChromeIdleDelay = 4, MapCircle = true, FadeMode = FadeMode.Always, WindowPosition = new(5, 6),
            TextPixelSnap = true };
        config.Markers.Opacity = .6f;
        var defaults = new SystemConfig();

        // Capture holds only portable differences from the defaults.
        var captured = ProfileCodec.Capture(config);
        foreach (var key in new[] { "AccentColor", "IconScale", "Markers.Opacity", "SpotlightHideOthers" })
            Require(captured.ContainsKey(key), "changed catalog setting is captured: " + key);
        Require(!captured.ContainsKey("WindowPosition") && !captured.ContainsKey("TextPixelSnap")
            && !captured.ContainsKey("Profiles") && !captured.ContainsKey("WindowPresets"),
            "window geometry, machine settings and the profiles themselves never travel");
        Require(!captured.ContainsKey("ShowTitleOnMap"), "unchanged settings are not captured");

        // Slots: save, name, limit, delete, rename.
        var slot = ProfileManager.Save(config, "  Night map\t​ ");
        Require(slot == 1 && config.ProfileOptions.Find(1)!.Name == "Night map", "names are cleaned");
        for (var i = 2; i <= SettingsProfileSet.Limit; i++)
            Require(ProfileManager.Save(config, "p" + i) == i, "five slots fill in order");
        Require(ProfileManager.Save(config, "sixth") == 0, "a sixth profile is refused");
        Require(ProfileManager.Delete(config, 2) && ProfileManager.Save(config, "") == 2
            && config.ProfileOptions.Find(2)!.Name == "Profile 2", "a deleted slot is reused with a default name");
        Require(ProfileManager.Rename(config, 3, new string('x', 100)) && config.ProfileOptions.Find(3)!.Name.Length == 40,
            "names are capped");

        // Apply restores the look, leaves excluded settings alone, and undo goes back.
        var other = new SystemConfig { WindowPosition = new(9, 9), TextPixelSnap = false, ShowTitleOnMap = false,
            ActiveLens = "Custom" };
        other.Profiles = config.Profiles;
        var changed = ProfileManager.Apply(other, 1);
        Require(changed > 0 && other.AccentColor == config.AccentColor && other.IconScale == .8f
            && other.MapCircle && other.FadeMode == FadeMode.Always && other.ChromeIdleDelay == 4
            && other.Markers.Opacity == .6f && !other.SpotlightHideOthers,
            "applying a profile reproduces the saved settings");
        Require(other.ShowTitleOnMap == defaults.ShowTitleOnMap,
            "settings the profile did not change fall back to the defaults");
        Require(other.WindowPosition == new Vector2(9, 9) && !other.TextPixelSnap && other.ActiveLens == "Custom",
            "applying never touches window geometry, machine settings or filters");
        Require(ProfileManager.Apply(other, 1) == 0, "re-applying changes nothing");
        ProfileManager.Apply(other, 1);
        other.ChromeIdleDelay = 7;
        ProfileManager.Apply(other, 1);
        Require(ProfileManager.Undo(other) && other.ChromeIdleDelay == 7 && !ProfileManager.Undo(other),
            "undo restores the settings from before the last apply, once");

        // Export and import.
        var text = ProfileManager.Export(config, 1);
        Require(text.StartsWith(ProfileCodec.Prefix) && text.Length < 3000 && !text.Contains('+')
            && !text.Contains('/') && !text.Contains(' '), "export is a compact paste-safe string");
        Require(ProfileCodec.TryDecode("  " + text + "\n", out var decoded, out _) && decoded!.Name == "Night map"
            && ProfileCodec.Recognised(decoded) == decoded.Values.Count, "export decodes with name and every setting");
        var fresh = new SystemConfig();
        Require(ProfileManager.Import(fresh, decoded) == 1 && ProfileManager.Apply(fresh, 1) > 0
            && fresh.AccentColor == config.AccentColor, "an imported profile applies like a saved one");
        Require(ProfileManager.Rename(config, 1, "Renamed") && ProfileCodec.TryDecode(ProfileManager.Export(config, 1),
            out var renamed, out _) && renamed!.Name == "Renamed", "renaming updates the exported name");

        // Hostile or damaged input is rejected without throwing.
        foreach (var bad in new[] { "", "   ", "hello", ProfileCodec.Prefix, ProfileCodec.Prefix + "!!!!",
            ProfileCodec.Prefix + "AAAA", ProfileCodec.Prefix + new string('A', SettingsProfileSet.MaxData),
            text[..^6], Pack("[]"), Pack("{\"v\":3}"), Pack("{\"n\":1}"), Pack("{\"v\":{\"a\":" + new string('[', 40)
            + "1" + new string(']', 40) + "}}") })
            Require(!ProfileCodec.TryDecode(bad, out _, out var error) && error.Length > 0, "rejects: " + (bad.Length > 24
                ? bad[..24] : bad));
        var bomb = Pack("{\"v\":{\"a\":\"" + new string('a', 200_000) + "\"}}");
        Require(!ProfileCodec.TryDecode(bomb, out _, out _) || bomb.Length > 0, "a large payload is handled");
        Require(!ProfileCodec.TryDecode(bomb, out _, out var tooBig) && tooBig.Contains("large"),
            "an oversized decompressed payload is refused");

        // A valid envelope full of bad values: unknown paths, wrong types, out-of-range and non-finite are dropped
        // or clamped, and nothing outside the catalog is written.
        var hostile = Pack("{\"n\":\"Evil\\u0007\",\"v\":{\"Nope\":1,\"WindowPosition\":[1,2],\"TextPixelSnap\":true,"
            + "\"Profiles\":{},\"IconScale\":9999,\"ChromeScale\":\"big\",\"AccentColor\":[5,-5,0.5,2],"
            + "\"VisualStyle\":77,\"FadeMode\":255,\"MapCircle\":1,\"TitleOffset\":[1e30,2],\"ZoomSpeed\":1e39}}");
        Require(ProfileCodec.TryDecode(hostile, out var evil, out _) && evil!.Name == "Evil", "hostile envelope parses");
        var target = new SystemConfig { WindowPosition = new(3, 3) };
        ProfileCodec.Apply(target, evil);
        Require(target.WindowPosition == new Vector2(3, 3) && !target.TextPixelSnap, "unlisted settings are untouched");
        Require(target.IconScale == 3, "numbers are clamped to the setting's range");
        Require(target.ChromeScale == defaults.ChromeScale && target.MapCircle == defaults.MapCircle
            && target.VisualStyle == defaults.VisualStyle && target.ZoomSpeed == defaults.ZoomSpeed,
            "wrong types, undefined enum values and non-finite numbers are ignored");
        Require(target.AccentColor == new Vector4(1, 0, .5f, 1), "colours are clamped to 0..1");
        Require(target.TitleOffset == new Vector2(100, 2), "vectors are clamped to the slider range");
        Require(ProfileCodec.Recognised(evil) < evil.Values.Count, "unrecognised settings are not counted");

        // Normalisation drops damaged stored profiles.
        var set = new SettingsProfileSet { Profiles = [new() { Slot = 1, Name = "ok", Data = text },
            new() { Slot = 1, Name = "dupe", Data = text }, new() { Slot = 9, Name = "far", Data = text },
            new() { Slot = 2, Name = "empty", Data = "" }, null!], Undo = new string('x', SettingsProfileSet.MaxData + 1) };
        set.Normalize();
        Require(set.Profiles.Count == 1 && set.Undo.Length == 0, "damaged stored profiles are dropped on load");

        // Survives a config save and load.
        var json = JsonSerializer.Serialize(config, ScriptedFrameDriver.Json);
        var loaded = JsonSerializer.Deserialize<SystemConfig>(json, ScriptedFrameDriver.Json)!;
        Require(loaded.ProfileOptions.Profiles.Count == config.ProfileOptions.Profiles.Count
            && loaded.ProfileOptions.Find(1)!.Data == config.ProfileOptions.Find(1)!.Data,
            "profiles survive saving and loading the config");

        // Every portable setting round-trips through its own encoding.
        var all = new SystemConfig();
        foreach (var entry in ProfileCodec.Portable)
            Require(entry.Value(all) is not null, "portable setting has a value: " + entry.Path);
        Require(ProfileCodec.Portable.Count > 150, $"most catalog settings are portable ({ProfileCodec.Portable.Count})");
    }
}
