using System.Linq;
using Gratis.Data;
using Gratis.Rendering;

namespace Gratis.Windows;

public static class ProfileManager {
    public static int Save(SystemConfig config, string? name) {
        var set = config.ProfileOptions;
        var slot = set.FreeSlot();
        if (slot == 0)
            return 0;
        var clean = SettingsProfileSet.CleanName(name, $"Profile {slot}");
        set.Profiles.Add(new() { Slot = slot, Name = clean,
            Data = ProfileCodec.Encode(clean, ProfileCodec.Capture(config)) });
        set.Profiles.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        return slot;
    }

    public static int Import(SystemConfig config, ProfileData data, string? name = null) {
        var set = config.ProfileOptions;
        var slot = set.FreeSlot();
        if (slot == 0)
            return 0;
        var clean = SettingsProfileSet.CleanName(name ?? data.Name, $"Profile {slot}");
        set.Profiles.Add(new() { Slot = slot, Name = clean, Data = ProfileCodec.Encode(clean, data.Values) });
        set.Profiles.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        return slot;
    }

    /// <summary>Applies a stored profile and keeps the previous settings for one undo. -1 if unreadable.</summary>
    public static int Apply(SystemConfig config, int slot) {
        var set = config.ProfileOptions;
        if (set.Find(slot) is not { } profile || !ProfileCodec.TryDecode(profile.Data, out var data, out _))
            return -1;
        var before = ProfileCodec.Encode("Before profile", ProfileCodec.Capture(config));
        var changed = ProfileCodec.Apply(config, data!);
        // A no-op apply must not replace the undo point with the already-applied state.
        if (changed > 0)
            set.Undo = before;
        return changed;
    }

    public static bool Undo(SystemConfig config) {
        var set = config.ProfileOptions;
        if (set.Undo.Length == 0 || !ProfileCodec.TryDecode(set.Undo, out var data, out _))
            return false;
        ProfileCodec.Apply(config, data!);
        set.Undo = "";
        return true;
    }

    public static bool Rename(SystemConfig config, int slot, string? name) {
        if (config.ProfileOptions.Find(slot) is not { } profile
            || !ProfileCodec.TryDecode(profile.Data, out var data, out _))
            return false;
        profile.Name = SettingsProfileSet.CleanName(name, $"Profile {slot}");
        profile.Data = ProfileCodec.Encode(profile.Name, data!.Values);
        return true;
    }

    public static string Export(SystemConfig config, int slot) => config.ProfileOptions.Find(slot)?.Data ?? "";

    public static bool Delete(SystemConfig config, int slot) => config.ProfileOptions.Delete(slot);
}
