using System;
using System.Numerics;

namespace Gratis.Rendering;

public static class MotionMath {
    public static Vector2 Glide(ref Vector2 velocity, float seconds, float strength) {
        if (seconds <= 0)
            return Vector2.Zero;
        strength = float.IsFinite(strength) ? Math.Clamp(strength, .1f, 1) : .35f;
        var decayRate = 4.60517f / strength;
        var decay = MathF.Exp(-decayRate * seconds);
        var distance = velocity * ((1 - decay) / decayRate);
        velocity *= decay;
        return distance;
    }

    public static float Ease(float t) {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }

    public static float Damp(float value, float target, ref float velocity, float seconds, float settleSeconds) {
        if (seconds <= 0)
            return value;
        if (settleSeconds <= 0) {
            velocity = 0;
            return target;
        }
        var omega = 6.64f / MathF.Max(.02f, settleSeconds);
        var displacement = value - target;
        var decay = MathF.Exp(-omega * seconds);
        var term = (velocity + omega * displacement) * seconds;
        velocity = (velocity - omega * term) * decay;
        return target + (displacement + term) * decay;
    }

    public static Vector2 Damp(Vector2 value, Vector2 target, ref Vector2 velocity, float seconds,
        float settleSeconds) {
        var x = velocity.X;
        var y = velocity.Y;
        var result = new Vector2(Damp(value.X, target.X, ref x, seconds, settleSeconds),
            Damp(value.Y, target.Y, ref y, seconds, settleSeconds));
        velocity = new Vector2(x, y);
        return result;
    }

    public static Vector2 DampBounded(Vector2 value, Vector2 target, ref Vector2 velocity, float seconds,
        float settleSeconds) {
        var next = Damp(value, target, ref velocity, seconds, settleSeconds);
        var bounded = Vector2.Clamp(next, Vector2.Min(value, target), Vector2.Max(value, target));
        if (bounded.X != next.X)
            velocity.X = 0;
        if (bounded.Y != next.Y)
            velocity.Y = 0;
        return bounded;
    }

    public static (float Radius, float Alpha) Ring(float age, int ring, float duration = .9f) {
        duration = MathF.Max(.1f, duration);
        var progress = (age - ring * duration * .2f) / (duration * .8f);
        if (progress < 0 || progress >= 1)
            return (0, 0);
        return (8 + 34 * progress, .72f * (1 - progress) * (1 - progress));
    }

    public static float TrailAlpha(double now, double sampled, float duration) => duration <= 0 || sampled > now ? 0
        : Math.Clamp(1 - (float)(now - sampled) / duration, 0, 1);

    public static float Transition(double now, double started, float duration) => duration <= 0 ? 1
        : Ease((float)((now - started) / duration));
}

public sealed class PlayerTrail {
    public readonly record struct Sample(Vector2 Position, double Time);
    private readonly Sample[] samples = new Sample[256];
    private int first;
    public int Count { get; private set; }
    public Sample this[int index] => samples[(first + index) % samples.Length];
    public void Clear() {
        first = Count = 0;
    }

    public void Prune(double now, float duration) {
        if (duration <= 0 || Count > 0 && this[Count - 1].Time > now) {
            Clear();
            return;
        }
        while (Count > 0 && MotionMath.TrailAlpha(now, this[0].Time, duration) <= 0) {
            first = (first + 1) % samples.Length;
            Count--;
        }
    }

    public void Add(Vector2 position, double now, float duration) {
        Prune(now, duration);
        if (duration <= 0)
            return;
        if (Count > 0 && (now - this[Count - 1].Time < .04
            || Vector2.DistanceSquared(this[Count - 1].Position, position) < .25f))
            return;
        if (Count == samples.Length) {
            first = (first + 1) % samples.Length;
            Count--;
        }
        samples[(first + Count++) % samples.Length] = new Sample(position, now);
    }
}