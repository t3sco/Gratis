using System;
using System.Numerics;

namespace Gratis.Rendering;

public enum PlayerArrowSmoothing { Off, Low, Medium, High }

public sealed class PlayerHeading
{
    public const double Deadzone = 2 * Math.PI / 180;
    public const double MaximumSpeed = 540 * Math.PI / 180;
    public const float TeleportDistance = 30;
    private double heading, target, time;
    private ulong map;
    private Vector3 position;
    private bool initialized;

    public static double Wrap(double angle) => Math.IEEERemainder(angle, 2 * Math.PI);
    public static double TimeConstant(PlayerArrowSmoothing level) => level switch {
        PlayerArrowSmoothing.Off => 0, PlayerArrowSmoothing.Low => .08,
        PlayerArrowSmoothing.High => .3, _ => .16,
    };

    public float Advance(float raw, double now, PlayerArrowSmoothing level, ulong mapId,
        Vector3 playerPosition, bool reduceMotion = false)
    {
        if (!float.IsFinite(raw) || !double.IsFinite(now)) return (float)heading;
        var elapsed = now - time;
        var reset = !initialized || map != mapId || elapsed < 0 ||
            Vector3.DistanceSquared(position, playerPosition) > TeleportDistance * TeleportDistance;
        time = now; map = mapId; position = playerPosition; initialized = true;
        var tau = TimeConstant(level);
        if (reset || reduceMotion || tau == 0) {
            heading = target = Wrap(raw);
            return raw;
        }
        if (Math.Abs(Wrap(raw - target)) > Deadzone) target = Wrap(raw);
        var error = Wrap(target - heading);
        var distance = Math.Abs(error);
        var boundary = MaximumSpeed * tau;
        var linearTime = Math.Max(0, (distance - boundary) / MaximumSpeed);
        var linearStep = Math.Min(elapsed, linearTime);
        var remaining = (distance - MaximumSpeed * linearStep) *
            Math.Exp(-Math.Max(0, elapsed - linearTime) / tau);
        heading = Wrap(heading + Math.Sign(error) * (distance - remaining));
        return (float)heading;
    }
}
