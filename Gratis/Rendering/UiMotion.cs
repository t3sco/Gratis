using System;
using System.Collections.Generic;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static partial class UiMotion
{
    public static InterfaceMotionStore Store { get; } = new();
    private static MapDrawContext? context;
    private static readonly Dictionary<uint, InterfaceScroll> scrolls = new(64);
    private static readonly Dictionary<uint, string> exitNames = new(64);
    private static int frame = -1;
    private static int ghostActive, ghostKeys;
    public static void IncludeGhost(int active, int keys) { ghostActive = active; ghostKeys = keys; }
    public static uint Item { get; private set; }
    public static void ItemId(string id) => Item = ImGui.GetID(id);
    public static bool Child(string id, Vector2 size, ImGuiWindowFlags flags) =>
#if GRATIS_PREVIEW
        ImGui.BeginChild(id, size, ImGuiChildFlags.None, flags);
#else
        ImGui.BeginChild(id, size, false, flags);
#endif
    public static bool Full => PolicyConfig.EffectiveInterfaceMotion == InterfaceAnimationMode.Full;
    public static bool Off => PolicyConfig.EffectiveInterfaceMotion == InterfaceAnimationMode.Off;
    public static void Begin()
    {
        if (!ReferenceEquals(context, MapDrawContext.Current)) {
            context = MapDrawContext.Current; Store.Reset(); scrolls.Clear(); exitNames.Clear(); ResetPresentation(); frame = -1;
        }
        var next = ImGui.GetFrameCount();
        if (frame == next) return;
        frame = next; Store.Begin(ImGui.GetTime(), next);
    }
    public static float Duration(float seconds)
    {
        var c = PolicyConfig;
        return seconds / Math.Clamp(float.IsFinite(c.AnimationSpeed) ? c.AnimationSpeed : 1, .5f, 1.5f);
    }
    public static float Value(uint id, uint channel, float target, float seconds = InterfaceMotionTokens.Micro,
        InterfaceCurve curve = InterfaceCurve.Enter, float? initial = null, float delay = 0, bool opacity = false)
    {
        Begin(); var mode = PolicyConfig.EffectiveInterfaceMotion;
        if (mode == InterfaceAnimationMode.Off) return target;
        var instant = mode == InterfaceAnimationMode.Off || PolicyConfig.ReduceMotion || mode == InterfaceAnimationMode.Reduced && !opacity;
        if (mode == InterfaceAnimationMode.Reduced) seconds = MathF.Min(seconds, .06f);
        return Store.Value(new(id, channel), target, Duration(seconds), curve, instant, initial, Full ? delay : 0);
    }
    public static float Visibility(uint id, bool open) => Value(id, 0, open ? 1 : 0,
        open ? InterfaceMotionTokens.Standard : InterfaceMotionTokens.Micro, open ? InterfaceCurve.Enter : InterfaceCurve.Exit,
        initial: 0, opacity: true);
    public static float Hover(uint id, bool hovered)
    {
        Begin();
        return !hovered && !Store.Contains(new(id, 8)) ? 0 : Value(id, 8, hovered ? 1 : 0, initial: 0);
    }
    public static VisualScope Surface(string id, Vector2 pivot, bool open = true, float delay = 0)
    {
        var key = ImGui.GetID(id);
        var progress = delay > 0 ? Value(key, 0, open ? 1 : 0, InterfaceMotionTokens.Standard, initial: 0, delay: delay, opacity: true) : Visibility(key, open);
        return new(ImGui.GetWindowDrawList(), pivot, progress, Full ? new Vector2(0, (1 - progress) * InterfaceMotionTokens.Slide * GratisTheme.ChromeScale) : Vector2.Zero,
            Full ? .97f + .03f * progress : 1);
    }
    public static VisualScope Control(ImDrawListPtr list, Vector2 min, Vector2 max, int start = -1)
    {
        if (!ImGui.IsItemVisible()) return new(list, min, 1, Vector2.Zero);
        var down = Value(Item, 3, ImGui.IsItemActive() && ImGui.IsMouseDown(ImGuiMouseButton.Left) ? 1 : 0,
            InterfaceMotionTokens.Micro, InterfaceCurve.Spring, initial: 0);
        return new(list, (min + max) / 2, 1 - .08f * down, Vector2.Zero, 1 - .03f * down, start);
    }
    public static Vector2 Selection(string id, Vector2 target)
    {
        var key = ImGui.GetID(id);
        return Selection(key, target);
    }
    public static Vector2 Selection(uint key, Vector2 target)
    {
        return new(Value(key, 4, target.X, InterfaceMotionTokens.Standard, InterfaceCurve.Spring),
            Value(key, 5, target.Y, InterfaceMotionTokens.Standard, InterfaceCurve.Spring));
    }
    public static void Scroll()
    {
        if (!MapDrawContext.Current.Config.IsGratis) return;
        Begin(); var id = ImGui.GetID("##interface-scroll");
        if (!scrolls.TryGetValue(id, out var state)) {
            if (scrolls.Count == 64) scrolls.Clear();
            state.Sync(ImGui.GetScrollY());
        }
        var io = ImGui.GetIO(); var pos = ImGui.GetWindowPos(); var size = ImGui.GetWindowSize();
        var over = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
        var dragging = io.MouseDown[0] && (io.MousePos.X >= pos.X + size.X - ImGui.GetStyle().ScrollbarSize - 2 ||
            MathF.Abs(ImGui.GetScrollY() - state.Position) > 2 && ImGui.IsAnyItemActive());
        var wheel = over && !dragging ? -io.MouseWheel * ImGui.GetFontSize() * 5 : 0;
        var next = state.Advance(ImGui.GetScrollY(), ImGui.GetScrollMaxY(), wheel, dragging,
            io.DeltaTime / Duration(1), !Full);
        if (MathF.Abs(next - ImGui.GetScrollY()) > .01f) ImGui.SetScrollY(next);
        scrolls[id] = state;
    }
    public static void ScrollToItem()
    {
        var id = ImGui.GetID("##interface-scroll");
        if (!scrolls.TryGetValue(id, out var state)) { ImGui.SetScrollHereY(); return; }
        var top = ImGui.GetWindowPos().Y + ImGui.GetStyle().WindowPadding.Y;
        state.Reveal(ImGui.GetItemRectMin().Y - top + ImGui.GetScrollY(), ImGui.GetItemRectMax().Y - top + ImGui.GetScrollY(),
            ImGui.GetWindowHeight() - ImGui.GetStyle().WindowPadding.Y * 2);
        scrolls[id] = state;
    }
    public static string Diagnostics() => $"interface active={Store.Active + ghostActive} keys={Store.Count + ghostKeys}/{InterfaceMotionStore.Capacity + 1} scrolls={scrolls.Count}/64 samples={Store.Samples} costEstimateMs={(Store.Samples + ghostActive) * .00015:0.000} (operation estimate)";

    public readonly struct VisualScope : IDisposable
    {
        private readonly ImDrawListPtr list;
        private readonly int first;
        private readonly Vector2 pivot, offset;
        private readonly float alpha, scale;
        public float Alpha => alpha;
        public bool Visible => alpha > .001f;
        public VisualScope(ImDrawListPtr list, Vector2 pivot, float alpha, Vector2 offset, float scale = 1, int start = -1)
        { this.list = list; first = start < 0 ? list.VtxBuffer.Size : start; this.pivot = pivot; this.alpha = alpha; this.offset = offset; this.scale = scale; }
        public unsafe void Dispose()
        {
            if (alpha == 1 && scale == 1 && offset == Vector2.Zero) return;
            var vertices = (ImDrawVert*)list.VtxBuffer.Data;
            for (var i = first; i < list.VtxBuffer.Size; i++) {
                var position = (Vector2*)(vertices + i); *position = pivot + (*position - pivot) * scale + offset;
                var colour = (uint*)((byte*)(vertices + i) + 16);
                *colour = (*colour & 0x00ffffff) | (uint)Math.Clamp((int)((*colour >> 24) * alpha), 0, 255) << 24;
            }
        }
    }
    public static HeightScope RevealHeight(ImDrawListPtr list, Vector2 min, Vector2 max, float progress) => new(list, min, max, progress);
    public readonly struct HeightScope : IDisposable
    {
        private readonly ImDrawListPtr list;
        public HeightScope(ImDrawListPtr list, Vector2 min, Vector2 max, float progress)
        {
            this.list = list;
            var reveal = Full ? 1 - (1 - progress) * (1 - progress) : 1;
            list.PushClipRect(min, new(max.X, min.Y + (max.Y - min.Y) * reveal), true);
        }
        public void Dispose() => list.PopClipRect();
    }

    public static PopupScope Popup(string id, bool desired, Vector2 min, Vector2 size, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
    {
        Begin(); var key = ImGui.GetID(id);
        var backdrop = ImGui.GetStyle().Colors[(int)ImGuiCol.PopupBg].W > 0;
        ImGui.SetNextWindowPos(min); ImGui.SetNextWindowSize(size);
        var native = desired && ImGui.BeginPopup(id, flags | ImGuiWindowFlags.NoBackground);
        var alpha = Visibility(key, native);
        if (!native) {
            if (!desired && ImGui.BeginPopup(id, flags)) { ImGui.CloseCurrentPopup(); ImGui.EndPopup(); }
            if (alpha <= .001f) return default;
            if (!exitNames.TryGetValue(key, out var name)) {
                if (exitNames.Count == 64) exitNames.Clear();
                exitNames[key] = name = "##interface-exit-" + key;
            }
            ImGui.SetNextWindowPos(min); ImGui.SetNextWindowSize(size);
            ImGui.Begin(name, flags | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoFocusOnAppearing |
                ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBackground);
        }
        var visual = new VisualScope(ImGui.GetWindowDrawList(), min, alpha,
            Full ? new Vector2(0, (1 - alpha) * InterfaceMotionTokens.Slide * GratisTheme.ChromeScale) : Vector2.Zero,
            Full ? .97f + .03f * alpha : 1, start: 0);
        if (backdrop) MapWindowDrawing.DrawSurface(ImGui.GetWindowDrawList(), min, min + size,
            GratisTheme.CardRadius * GratisTheme.ChromeScale, surfaceOpacity: 1);
        return new(native, visual);
    }
    public readonly struct PopupScope(bool interactive, VisualScope visual) : IDisposable
    {
        private readonly bool begun = true;
        public bool Visible => begun;
        public bool Interactive => interactive;
        public void Dispose()
        {
            if (!begun) return;
            visual.Dispose();
            if (interactive) ImGui.EndPopup(); else ImGui.End();
        }
    }
}
