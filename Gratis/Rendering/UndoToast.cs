using Gratis.Data;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

/// <summary>One short-lived undo per settings window; later edits invalidate its snapshot.</summary>
public sealed class UndoToast {
    private static readonly global::System.Runtime.CompilerServices.ConditionalWeakTable<SystemConfig, UndoToast> Pending = new();
    public const double Duration = 8;
    private global::System.Func<bool>? valid;
    private global::System.Action? restore;
    private string message = "";
    private double until;
#if GRATIS_PREVIEW
    public static global::System.Numerics.Vector4 TestBounds { get; private set; }
#endif

    public static void Offer(SystemConfig config, string message, double now,
        global::System.Func<bool> valid, global::System.Action restore) {
        var undo = Pending.GetOrCreateValue(config);
        undo.message = message;
        undo.valid = valid;
        undo.restore = restore;
        undo.until = now + Duration;
    }

    public static bool Available(SystemConfig config, double now) {
        if (!Pending.TryGetValue(config, out var undo) || undo.restore == null)
            return false;
        if (now < undo.until && undo.valid!())
            return true;
        undo.restore = null;
        undo.valid = null;
        return false;
    }

    public static bool Restore(SystemConfig config, double now) {
        if (!Available(config, now)) return false;
        var undo = Pending.GetOrCreateValue(config);
        var action = undo.restore!;
        undo.restore = null;
        undo.valid = null;
        action();
        return true;
    }

    public static bool Draw(SystemConfig config, float scale) {
#if GRATIS_PREVIEW
        TestBounds = default;
#endif
        if (!Available(config, ImGui.GetTime())) return false;
        ImGui.TextUnformatted(Pending.GetOrCreateValue(config).message);
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
