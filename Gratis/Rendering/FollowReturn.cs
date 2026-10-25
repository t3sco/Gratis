using System;
using System.Numerics;

namespace Gratis.Rendering;

public enum FollowReturnTrigger {
    WhenPlayerMoves, WhenUnfocused, AfterIdle
}
public enum FollowState {
    Following, Detached, Returning
}

public sealed class FollowReturn {
    public FollowState State { get; private set; }
    public bool Snapped { get; private set; }
    private Vector2 velocity, previousPlayer, movementAnchor;
    private ulong? map;
    private bool enabled;
    private double previousTime, lastInteraction;

    public void Reset() {
        enabled = false;
        map = null;
        velocity = Vector2.Zero;
        State = FollowState.Following;
    }

    public bool Interact(MapDrawingConfig config, double now, bool drag) {
        if (!config.IsGratis || !config.FollowReturnToPlayer)
            return !drag && config.FollowPlayer;
        if (config.FollowPlayer) {
            State = FollowState.Detached;
            velocity = Vector2.Zero;
            movementAnchor = previousPlayer;
            lastInteraction = now;
        }
        return config.FollowPlayer;
    }

    public Vector2 Update(MapDrawingConfig config, ulong mapId, Vector2 player, Vector2 target, Vector2 current,
        double now, bool interacting, bool hovered, bool focused) {
        Snapped = false;
        var elapsed = now - previousTime;
        previousTime = now;
        var reset = !enabled || map != mapId || elapsed < 0
            || Vector2.DistanceSquared(previousPlayer, player) > 50 * 50;
        map = mapId;
        previousPlayer = player;
        enabled = config.FollowPlayer && config.IsGratis && config.FollowReturnToPlayer;
        if (!enabled || reset) {
            State = FollowState.Following;
            velocity = Vector2.Zero;
            movementAnchor = player;
            Snapped = enabled;
            return enabled ? target : current;
        }
        if (interacting)
            Interact(config, now, false);
        if (State == FollowState.Detached) {
            var released = !interacting && (!hovered || !focused);
            if (!released)
                movementAnchor = player;
            var trigger = config.FollowReturnTrigger switch {
                FollowReturnTrigger.WhenUnfocused => !focused,
                FollowReturnTrigger.AfterIdle => now
                    - lastInteraction >= Math.Clamp(config.FollowReturnIdleSeconds, .5f, 20),
                _ => Vector2.DistanceSquared(movementAnchor, player) > .5f * .5f,
            };
            if (!released || !trigger)
                return current;
            State = FollowState.Returning;
            velocity = Vector2.Zero;
            elapsed = 0;
        }
        if (State == FollowState.Returning) {
            var duration = config.ReduceMotion ? 0 : Math.Clamp(config.FollowReturnEaseMs, 0, 1500) / 1000;
            if (duration == 0) {
                State = FollowState.Following;
                return target;
            }
            current = MotionMath.DampBounded(current, target, ref velocity, (float)Math.Max(0, elapsed), duration);
            if (Vector2.DistanceSquared(current, target) < .0025f && velocity.LengthSquared() < .01f) {
                State = FollowState.Following;
                velocity = Vector2.Zero;
                return target;
            }
            return current;
        }
        return target;
    }
}