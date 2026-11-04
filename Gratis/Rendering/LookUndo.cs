using System;
using System.Runtime.CompilerServices;
using Gratis.Data;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

/// <summary>A short-lived snapshot of just the settings owned by a look.</summary>
public sealed class LookUndo {
    private static readonly ConditionalWeakTable<SystemConfig, LookUndo> Pending = new();
    private object[]? values, applied;
    private double until;
    public const double Duration = 8;
#if GRATIS_PREVIEW
    public static System.Numerics.Vector4 TestBounds { get; private set; }
#endif

    // Undo is only offered while the settings still hold what the look wrote: a profile switch, a reset or a hand edit
    // in between would otherwise be overwritten by the older snapshot.
    public static bool Available(SystemConfig config, double now) => Pending.TryGetValue(config, out var undo)
        && undo.values != null && now < undo.until && undo.StillApplied(config);

    private bool StillApplied(SystemConfig config) {
        var entries = QuickSetupLooks.OwnedEntries;
        for (var i = 0; i < entries.Length; i++)
            if (!Equals(entries[i].Value(config), applied![i]))
                return false;
        return true;
    }

    public static bool Apply(SystemConfig config, int index, double now) {
        var previous = Array.ConvertAll(QuickSetupLooks.OwnedEntries, entry => entry.Value(config));
        if (QuickSetupLooks.Apply(config, index) <= 0)
            return false;
        var undo = Pending.GetOrCreateValue(config);
        undo.values = previous;
        undo.applied = Array.ConvertAll(QuickSetupLooks.OwnedEntries, entry => entry.Value(config));
        undo.until = now + Duration;
        return true;
    }

    public static bool Restore(SystemConfig config, double now) {
        if (!Available(config, now))
            return false;
        var undo = Pending.GetOrCreateValue(config);
        var entries = QuickSetupLooks.OwnedEntries;
        for (var i = 0; i < entries.Length; i++)
            entries[i].Set(config, undo.values![i]);
        undo.values = null;
        return true;
    }

    public static bool Draw(SystemConfig config, float scale) {
#if GRATIS_PREVIEW
        TestBounds = default;
#endif
        if (!Available(config, ImGui.GetTime()))
            return false;
        ImGui.TextUnformatted("Look applied");
        ImGui.SameLine(0, GratisTheme.Gap * scale);
        var clicked = ChromeControls.TextButton("Undo", scale: scale);
#if GRATIS_PREVIEW
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        TestBounds = new(min.X, min.Y, max.X, max.Y);
#endif
        return clicked && Restore(config, ImGui.GetTime());
    }
}
