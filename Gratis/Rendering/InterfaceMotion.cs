using System;
using System.Collections.Generic;
using System.Numerics;

namespace Gratis.Rendering;

public enum InterfaceAnimationMode { Full, Reduced, Off }
public enum InterfaceCurve { Enter, Exit, Spring }
public readonly record struct MotionKey(uint Id, uint Channel = 0);

public static class InterfaceMotionTokens
{
    public const float Micro = .10f, Standard = .18f, Emphasized = .28f;
    public const float Scroll = .15f, HoverEnter = .14f, HoverExit = .20f;
    public const float Slide = 6, PressScale = .97f;
    public static float Ease(float t, InterfaceCurve curve) => curve == InterfaceCurve.Exit ? t * t * t : 1 - MathF.Pow(1 - t, 3);
}

public partial class MapDrawingConfig
{
    public InterfaceAnimationMode InterfaceAnimations = InterfaceAnimationMode.Full;
    public float AnimationSpeed = 1;
    public InterfaceAnimationMode EffectiveInterfaceMotion => !IsGratis || InterfaceAnimations == InterfaceAnimationMode.Off
        ? InterfaceAnimationMode.Off : ReduceMotion ? InterfaceAnimationMode.Reduced : InterfaceAnimations;
}

public sealed class InterfaceMotionStore
{
    public const int Capacity = 2048;
    private struct Entry
    {
        public MotionKey Key;
        public float Value, From, Target, Velocity;
        public double Began, Seen, Advanced;
        public int Frame;
        public bool Used;
    }
    private readonly Dictionary<MotionKey, int> slots = new(Capacity);
    private readonly Entry[] entries = new Entry[Capacity];
    private int cursor, frame = -1;
    private double now;
    public int Count => slots.Count;
    public bool Contains(MotionKey key) => slots.ContainsKey(key);
    public int Active { get; private set; }
    public int Samples { get; private set; }
    public void Begin(double time, int nextFrame)
    {
        if (frame == nextFrame) return;
        if (time < now) Reset();
        now = time; frame = nextFrame; Active = Samples = 0;
        for (var i = 0; i < 16; i++) {
            ref var old = ref entries[cursor];
            if (old.Used && now - old.Seen > 3) { slots.Remove(old.Key); old = default; }
            cursor = (cursor + 1) % Capacity;
        }
    }
    public void Reset() { slots.Clear(); Array.Clear(entries); frame = -1; now = 0; cursor = Active = Samples = 0; }
    public float Value(MotionKey key, float target, float seconds, InterfaceCurve curve = InterfaceCurve.Enter,
        bool instant = false, float? initial = null, float delay = 0)
    {
        Samples++;
        if (!float.IsFinite(target)) return 0;
        if (!slots.TryGetValue(key, out var at)) {
            while (entries[cursor].Used && slots.Count < Capacity) cursor = (cursor + 1) % Capacity;
            at = cursor; cursor = (cursor + 1) % Capacity;
            if (entries[at].Used) slots.Remove(entries[at].Key);
            slots.Add(key, at);
            var start = initial ?? target;
            entries[at] = new() { Key = key, Value = start, From = start, Target = target, Began = now + delay, Advanced = now, Seen = now, Frame = -1, Used = true };
        }
        ref var entry = ref entries[at];
        if (entry.Target != target) {
            entry.From = entry.Value; entry.Target = target; entry.Began = now + delay;
        }
        entry.Seen = now;
        if (instant || seconds <= 0) { entry.Value = target; entry.Velocity = 0; }
        else if (entry.Frame != frame && now >= entry.Began) {
            if (curve == InterfaceCurve.Spring) {
                entry.Value = MotionMath.Damp(entry.Value, target, ref entry.Velocity, (float)Math.Max(0, now - entry.Advanced), seconds);
                if (MathF.Abs(entry.Value - target) < .001f && MathF.Abs(entry.Velocity) < .01f) { entry.Value = target; entry.Velocity = 0; }
            }
            else {
                var t = (float)Math.Clamp((now - entry.Began) / seconds, 0, 1);
                entry.Value = entry.From + (target - entry.From) * InterfaceMotionTokens.Ease(t, curve);
            }
        }
        if (entry.Frame != frame && (entry.Value != target || entry.Velocity != 0)) Active++;
        entry.Frame = frame; entry.Advanced = now;
        return entry.Value;
    }
}

public struct InterfaceScroll
{
    public float Position, Target, Velocity;
    private bool initialized;
    public void Sync(float native) { Position = Target = native; Velocity = 0; initialized = true; }
    public float Advance(float native, float maximum, float wheel, bool dragging, float dt, bool instant)
    {
        maximum = MathF.Max(0, maximum);
        if (!initialized || dragging) Sync(native);
        Target = Math.Clamp(Target + wheel, 0, maximum);
        if (dragging || instant) { Position = Target; Velocity = 0; }
        else Position = MotionMath.Damp(Position, Target, ref Velocity, MathF.Max(0, dt), InterfaceMotionTokens.Scroll);
        Position = Math.Clamp(Position, 0, maximum);
        if (MathF.Abs(Position - Target) < .05f && MathF.Abs(Velocity) < .1f) { Position = Target; Velocity = 0; }
        return Position;
    }
    public void Reveal(float itemMin, float itemMax, float viewport)
    {
        if (itemMin < Target) Target = itemMin;
        else if (itemMax > Target + viewport) Target = itemMax - viewport;
    }
}
