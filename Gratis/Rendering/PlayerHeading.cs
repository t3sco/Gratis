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
    private bool wasReduced;
    public float RawAngle { get; private set; }
    public float OutputAngle { get; private set; }
    public PlayerArrowSmoothing Level { get; private set; }
    public double Tau => TimeConstant(Level);
    public bool Reduced { get; private set; }
    public int ResetCount { get; private set; }
    public string LastResetReason { get; private set; } = "none";
    public long FrameId { get; private set; } = -1;
    public int AdvanceCallsPerFrame { get; private set; }
    public int IntegratedCallsPerFrame { get; private set; }
    public int DuplicateFrameCalls { get; private set; }
    public double Elapsed { get; private set; }
    public int InvalidSamples { get; private set; }

    public static double Wrap(double angle) => Math.IEEERemainder(angle, 2 * Math.PI);
    public static double TimeConstant(PlayerArrowSmoothing level) => level switch {
        PlayerArrowSmoothing.Off => 0, PlayerArrowSmoothing.Low => .12,
        PlayerArrowSmoothing.High => .5, _ => .25,
    };

    public float Advance(float raw, double now, PlayerArrowSmoothing level, ulong mapId,
        Vector3 playerPosition, bool reduceMotion = false, long frameId = -1)
    {
        if (frameId >= 0 && FrameId == frameId) {
            AdvanceCallsPerFrame++; DuplicateFrameCalls++;
            return OutputAngle;
        }
        FrameId = frameId; AdvanceCallsPerFrame = 1; IntegratedCallsPerFrame = 0;
        if (!float.IsFinite(raw) || !double.IsFinite(now)) { InvalidSamples++; return OutputAngle; }
        IntegratedCallsPerFrame = 1;
        RawAngle = raw; Level = level; Reduced = reduceMotion;
        var elapsed = now - time;
        Elapsed = initialized ? elapsed : 0;
        var reason = !initialized ? "initial-sample" : map != mapId ? "map-changed" : elapsed < 0 ? "clock-rewind" :
            Vector3.DistanceSquared(position, playerPosition) > TeleportDistance * TeleportDistance ? "teleport" :
            reduceMotion && !wasReduced ? "reduce-motion-enabled" : null;
        if (reason is not null) { ResetCount++; LastResetReason = reason; }
        wasReduced = reduceMotion;
        time = now; map = mapId; position = playerPosition; initialized = true;
        var tau = TimeConstant(level);
        if (reason is not null || reduceMotion || tau == 0) {
            heading = target = Wrap(raw);
            return OutputAngle = raw;
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
        return OutputAngle = (float)heading;
    }

    public string Diagnostics() => string.Create(global::System.Globalization.CultureInfo.InvariantCulture,
        $"arrow raw={RawAngle * 180 / MathF.PI:0.###} smoothed={OutputAngle * 180 / MathF.PI:0.###}deg tau={Tau:0.###}s level={Level} " +
        $"effectiveReduceMotion={Reduced} resetCount={ResetCount} lastReset={LastResetReason} AdvanceCallsPerFrame={AdvanceCallsPerFrame} " +
        $"integrationsPerFrame={IntegratedCallsPerFrame} duplicateCalls={DuplicateFrameCalls} invalidSamples={InvalidSamples} elapsed={Elapsed:0.####}s frame={FrameId}");
}
