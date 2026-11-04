#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

/// <summary>
/// The two-click guard for an action that cannot be undone. The first click arms it, the control then reads as
/// confirming, and a second click on the same thing within a few seconds does it. Waiting too long, or clicking
/// something else, disarms it. One instance guards one place, so keep it beside the control.
/// </summary>
public sealed class ArmedConfirm {
    public const double Seconds = 4;
    private string? armed;
    private double until;

    /// <summary>True while the given thing is armed and its confirming click is still welcome.</summary>
    public bool IsArmed(string key) => armed == key && ImGui.GetTime() < until;

    /// <summary>Call on a click. Returns true when this click is the confirming one and the action should run.</summary>
    public bool Click(string key) {
        if (IsArmed(key)) {
            armed = null;
            return true;
        }
        armed = key;
        until = ImGui.GetTime() + Seconds;
        return false;
    }

    public void Clear() => armed = null;
}
