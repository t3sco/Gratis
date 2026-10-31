using System;
using System.Collections.Generic;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

// Auto-hiding scrollbars for the Gratis style. ImGui reads the scrollbar colours while it Begins the window, so a region
// pushes faded copies of those colours just around its own Begin (ScrollbarBegin/ScrollbarEnd) and ScrollbarEnd samples
// what the region did that frame. The scrollbar keeps its normal geometry and stays grabbable at any opacity.
public static partial class UiMotion {
    private const double ScrollbarHold = .6;
    private const float ScrollbarAttack = .1f, ScrollbarRelease = .3f;
    private static readonly Dictionary<uint, ScrollbarFade> fades = new(64);

    private struct ScrollbarFade {
        public double Stamp, LastActive;
        public float Alpha, Scroll;
        public bool Grabbing;
    }

    // Fade state is stored as Alpha at the last evaluation; tests read it through this to avoid depending on pixels.
    internal static float ScrollbarAlpha(uint key) => fades.TryGetValue(key, out var fade) ? fade.Alpha : 0;
    internal static int ScrollbarRegions => fades.Count;

    // Returns a token for ScrollbarEnd: 0 when the region is not a Gratis scroller (Classic, or it has no scrollbar).
    public static uint ScrollbarBegin(string id, ImGuiWindowFlags flags = ImGuiWindowFlags.None) =>
        (flags & ImGuiWindowFlags.NoScrollbar) != 0 || !MapDrawContext.Current.Config.IsGratis ? 0
            : ScrollbarBegin(ImGui.GetID(id));

    public static uint ScrollbarBegin(uint key, ImGuiWindowFlags flags) =>
        (flags & ImGuiWindowFlags.NoScrollbar) != 0 ? 0 : ScrollbarBegin(key);

    public static unsafe uint ScrollbarBegin(uint key) {
        if (!MapDrawContext.Current.Config.IsGratis)
            return 0;
        key |= 1;
        Begin();
        var alpha = 0f;
        if (fades.TryGetValue(key, out var fade)) {
            alpha = ScrollbarAdvance(ref fade, ImGui.GetTime());
            fades[key] = fade;
        }
        PushScrollbar(ImGuiCol.ScrollbarBg, alpha);
        PushScrollbar(ImGuiCol.ScrollbarGrab, alpha);
        PushScrollbar(ImGuiCol.ScrollbarGrabHovered, alpha);
        PushScrollbar(ImGuiCol.ScrollbarGrabActive, alpha);
        return key;
    }

    private static unsafe void PushScrollbar(ImGuiCol colour, float alpha) {
        var value = *ImGui.GetStyleColorVec4(colour);
        value.W *= alpha;
        ImGui.PushStyleColor(colour, value);
    }

    // Call straight after the region's Begin has returned, inside it when it did, with the token from ScrollbarBegin.
    public static void ScrollbarEnd(uint token, bool visible) {
        if (token == 0)
            return;
        ImGui.PopStyleColor(4);
        if (visible)
            ScrollbarTrack(token);
    }

    // For items that scroll inside a window ImGui creates itself (multiline inputs): the scroll offset is out of reach,
    // so typing, the wheel and the right edge stand in for it.
    public static void ScrollbarEndItem(uint token, bool typed) {
        if (token == 0)
            return;
        ImGui.PopStyleColor(4);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var navigating = ImGui.IsItemActive() && (ImGui.IsKeyPressed(ImGuiKey.UpArrow) || ImGui.IsKeyPressed(ImGuiKey.DownArrow)
            || ImGui.IsKeyPressed(ImGuiKey.PageUp) || ImGui.IsKeyPressed(ImGuiKey.PageDown));
        ScrollbarSample(token, min, max, float.NaN, typed || navigating);
    }

    private static void ScrollbarTrack(uint key) {
        var max = ImGui.GetScrollMaxY();
        if (max <= .5f) {
            fades.Remove(key);
            return;
        }
        var min = ImGui.GetWindowPos();
        ScrollbarSample(key, min, min + ImGui.GetWindowSize(), ImGui.GetScrollY(), false);
    }

    private static void ScrollbarSample(uint key, Vector2 min, Vector2 max, float scroll, bool activity) {
        var io = ImGui.GetIO();
        var now = ImGui.GetTime();
        if (!fades.TryGetValue(key, out var fade)) {
            if (fades.Count == 64)
                fades.Clear();
            fade = new() { Stamp = now, LastActive = double.NegativeInfinity, Scroll = scroll };
        }
        var bar = ImGui.GetStyle().ScrollbarSize;
        var hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem)
            && io.MousePos.X >= min.X && io.MousePos.X < max.X && io.MousePos.Y >= min.Y && io.MousePos.Y < max.Y;
        var edge = hovered && io.MousePos.X >= max.X - bar - 10 * GratisTheme.ChromeScale;
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            fade.Grabbing = false;
        else if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && io.MousePos.X >= max.X - bar - 2)
            fade.Grabbing = true;
        if (activity || edge || fade.Grabbing || hovered && io.MouseWheel != 0
            || !float.IsNaN(scroll) && MathF.Abs(scroll - fade.Scroll) > .01f)
            fade.LastActive = now;
        fade.Scroll = scroll;
        fades[key] = fade;
    }

    private static float ScrollbarAdvance(ref ScrollbarFade fade, double now) {
        var held = now <= fade.LastActive + ScrollbarHold;
        if (Full) {
            // Rise while activity lasts, then fall for whatever is left of the elapsed time since the last sample.
            var elapsed = (float)Math.Max(0, now - fade.Stamp);
            var live = (float)Math.Clamp(Math.Min(now, fade.LastActive + ScrollbarHold) - fade.Stamp, 0, elapsed);
            fade.Alpha = Math.Max(0, Math.Min(1, fade.Alpha + live / Duration(ScrollbarAttack))
                - (elapsed - live) / Duration(ScrollbarRelease));
        }
        else
            fade.Alpha = held ? 1 : 0;
        fade.Stamp = now;
        return fade.Alpha;
    }
}
