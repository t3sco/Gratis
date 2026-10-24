using System;
using System.Numerics;

namespace Gratis.Controllers;

public readonly record struct ArrivalFlag(uint Territory, uint Map, Vector2 World, long Revision = 0);

public sealed class FlagArrivalState
{
    public const double DwellSeconds = 1.5;
    public ArrivalFlag? Flag { get; private set; }
    public bool Arrived { get; private set; }
    public string Decision { get; private set; } = "idle";
    public float? Distance { get; private set; }
    public double Dwell { get; private set; }
    private uint territory, map;
    private long zoneRevision;
    private bool initialized, needsExit;
    private double lastTime = double.NegativeInfinity;
    private double? insideSince;

    public bool Update(double now, uint currentTerritory, uint currentMap, Vector2? player,
        ArrivalFlag? flag, bool enabled = true, float radius = 4, bool transitioning = false, long currentZoneRevision = 0)
    {
        var zoneChanged = initialized && (territory != currentTerritory || map != currentMap || zoneRevision != currentZoneRevision);
        territory = currentTerritory; map = currentMap; zoneRevision = currentZoneRevision; initialized = true;
        var changed = Flag != flag;
        if (changed) { Flag = flag; Arrived = false; needsExit = false; insideSince = null; }
        Distance = null; Dwell = 0;
        var discontinuous = !double.IsFinite(now) || now < lastTime ||
            double.IsFinite(lastTime) && now - lastTime > 1;
        lastTime = double.IsFinite(now) ? now : double.NegativeInfinity;
        if (zoneChanged || transitioning) { needsExit = flag != null; insideSince = null; }
        bool Stop(string reason) { Decision = reason; insideSince = null; return false; }
        if (!enabled) { Arrived = false; return Stop("disabled"); }
        if (flag == null) { needsExit = false; return Stop("no flag"); }
        if (currentMap == 0 || currentTerritory == 0 || transitioning) return Stop("zone transition");
        if (flag.Value.Map != currentMap || flag.Value.Territory != currentTerritory) return Stop("flag on another map");
        if (player is not { } position || !Finite(position) || !Finite(flag.Value.World)) return Stop("position unavailable");
        if (!float.IsFinite(radius) || radius <= 0) radius = 4;
        Distance = Vector2.Distance(position, flag.Value.World);
        if (Distance > radius) { needsExit = false; return Stop(Arrived ? "arrived (outside)" : "outside radius"); }
        if (zoneChanged || needsExit) return Stop("zone entry: leave radius to arm");
        if (Arrived) return Stop("already arrived");
        if (discontinuous) return Stop("clock/sample discontinuity");
        insideSince ??= now;
        Dwell = Math.Max(0, now - insideSince.Value);
        Decision = "dwelling";
        if (Dwell < DwellSeconds) return false;
        Arrived = true; Decision = "arrived"; return true;
    }
    private static bool Finite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);
}
