using System;

namespace Gratis.Rendering;

public sealed class GhostMode
{
    public bool ClickThrough { get; private set; }
    public float Opacity { get; private set; } = 1;
    public string Reason { get; private set; } = "disabled";
    private double lastTime = double.NaN, stationarySince = double.NaN, transitionAt, awakeUntil;
    private float transitionFrom = 1, target = 1;
    public void Wake(double now) { awakeUntil = now + .6; ClickThrough = false; }

    public void Update(MapDrawingConfig config, double now, bool moving, bool hovered, bool focused,
        bool blocked, bool interacting = false, bool mapKey = false)
    {
        if (!double.IsFinite(now)) return;
        if (!double.IsNaN(lastTime) && now < lastTime) {
            stationarySince = double.NaN; awakeUntil = 0; ClickThrough = false;
            target = transitionFrom = Opacity = 1; transitionAt = now;
        }
        lastTime = now;
        if (mapKey) Wake(now);
        if (moving) stationarySince = double.NaN;
        else if (double.IsNaN(stationarySince)) stationarySince = now;
        var enabled = config.IsGratis && config.GhostMode;
        var delay = float.IsFinite(config.GhostRestoreDelay) ? Math.Clamp(config.GhostRestoreDelay, 0, 10) : .6f;
        var recentlyMoving = moving || ClickThrough && now - stationarySince < delay;
        var ghost = enabled && recentlyMoving && !hovered && !focused && !blocked && !interacting && now >= awakeUntil;
        Reason = !enabled ? "disabled" : hovered ? "hover" : focused ? "focus" : blocked ? "overlay/popup/drag" :
            interacting ? "interaction" : now < awakeUntil ? "map key" : ghost ? "moving/restore delay" : "stationary";
        ClickThrough = ghost;
        var next = ghost ? (float.IsFinite(config.GhostOpacity) ? Math.Clamp(config.GhostOpacity, .05f, 1) : .35f) : 1;
        if (!enabled || config.ReduceMotion) { target = next; Opacity = next; transitionAt = now; transitionFrom = next; return; }
        if (next != target) { transitionFrom = Opacity; target = next; transitionAt = now; }
        var t = (float)Math.Clamp((now - transitionAt) / .12, 0, 1);
        Opacity = transitionFrom + (target - transitionFrom) * t * t * (3 - 2 * t);
    }

    public string Diagnostics() => $"clickThrough={ClickThrough} opacity={Opacity:0.000} reason={Reason} flags=" +
        (ClickThrough ? "NoInputs (NoMouseInputs|NoNavInputs|NoNavFocus)" : "None");
}
