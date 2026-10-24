using System.Collections.Generic;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif
using Math = global::System.Math;

namespace Gratis.Rendering;

internal static class HoverAnimation
{
    private sealed class State { public float Value; public double Seen; public int Frame = -1; }
    private static readonly Dictionary<string, State> States = [];
    private static double lastPrune;

    public static float Progress(string key, bool hovered)
    {
        var now = ImGui.GetTime();
        if (now < lastPrune || now - lastPrune > 1) {
            foreach (var entry in new List<string>(States.Keys))
                if (now < States[entry].Seen || now - States[entry].Seen > .3) States.Remove(entry);
            lastPrune = now;
        }
        if (!States.TryGetValue(key, out var state)) {
            if (!hovered) return 0;
            States[key] = state = new State();
        }
        if (now - state.Seen > .1) state.Value = 0;
        if (state.Frame != ImGui.GetFrameCount()) {
            state.Value = Math.Clamp(state.Value + (hovered ? 1 : -1) * ImGui.GetIO().DeltaTime / GratisTheme.HoverSeconds, 0, 1);
            state.Frame = ImGui.GetFrameCount();
        }
        state.Seen = now;
        return state.Value * state.Value * (3 - 2 * state.Value);
    }
}
