using System;
using Gratis.Data;

namespace Gratis.Rendering;

/// <summary>A short-lived snapshot of just the settings owned by a look.</summary>
public sealed class LookUndo {
    public const double Duration = UndoToast.Duration;
#if GRATIS_PREVIEW
    public static global::System.Numerics.Vector4 TestBounds => UndoToast.TestBounds;
#endif
    public static bool Available(SystemConfig config, double now) => UndoToast.Available(config, now);
    public static bool Restore(SystemConfig config, double now) => UndoToast.Restore(config, now);
    public static bool Draw(SystemConfig config, float scale) => UndoToast.Draw(config, scale);

    public static bool Apply(SystemConfig config, int index, double now) {
        var entries = QuickSetupLooks.OwnedEntries;
        var previous = Array.ConvertAll(entries, entry => entry.Value(config));
        if (QuickSetupLooks.Apply(config, index) <= 0) return false;
        var applied = Array.ConvertAll(entries, entry => entry.Value(config));
        UndoToast.Offer(config, "Look applied", now, () => {
            for (var i = 0; i < entries.Length; i++)
                if (!Equals(entries[i].Value(config), applied[i])) return false;
            return true;
        }, () => {
            for (var i = 0; i < entries.Length; i++) entries[i].Set(config, previous[i]);
        });
        return true;
    }
}
