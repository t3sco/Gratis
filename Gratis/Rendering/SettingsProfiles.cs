using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Gratis.Rendering;

public sealed class SettingsProfile {
    public int Slot;
    public string Name = "";
    public string Data = "";
}

public sealed class SettingsProfileSet {
    public const int Limit = 5;
    public const int MaxData = 24000;
    public List<SettingsProfile> Profiles = [];
    // The settings as they were just before the last profile was applied.
    public string Undo = "";

    public SettingsProfile? Find(int slot) {
        if (Profiles != null)
            foreach (var profile in Profiles)
                if (profile != null && profile.Slot == slot)
                    return profile;
        return null;
    }

    public int FreeSlot() {
        for (var slot = 1; slot <= Limit; slot++)
            if (Find(slot) == null)
                return slot;
        return 0;
    }

    public void Normalize() {
        Profiles ??= [];
        Undo ??= "";
        if (Undo.Length > MaxData)
            Undo = "";
        var seen = 0;
        for (var i = 0; i < Profiles.Count;) {
            var p = Profiles[i];
            if (p == null || p.Slot is < 1 or > Limit || (seen & (1 << p.Slot)) != 0 || string.IsNullOrEmpty(p.Data)
                || p.Data.Length > MaxData) {
                Profiles.RemoveAt(i);
                continue;
            }
            seen |= 1 << p.Slot;
            p.Name = CleanName(p.Name, $"Profile {p.Slot}");
            i++;
        }
    }

    public bool Delete(int slot) => Profiles.RemoveAll(p => p.Slot == slot) > 0;

    public static string CleanName(string? name, string fallback) {
        var text = new StringBuilder();
        foreach (var c in name ?? "") {
            if (char.IsControl(c) || char.GetUnicodeCategory(c) == global::System.Globalization.UnicodeCategory.Format)
                continue;
            if (text.Length == 40)
                break;
            text.Append(c);
        }
        if (text.Length > 0 && char.IsHighSurrogate(text[^1]))
            text.Length--;
        var result = text.ToString().Trim();
        return result.Length == 0 ? fallback : result;
    }
}

public partial class MapDrawingConfig {
    public SettingsProfileSet Profiles = new();
    [JsonIgnore]
    public SettingsProfileSet ProfileOptions {
        get {
            Profiles ??= new();
            Profiles.Normalize();
            return Profiles;
        }
    }
}
