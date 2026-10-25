using System;

namespace Gratis.Rendering;

public sealed class GhostMode {
    public bool ClickThrough { get; private set; }
    public float Opacity { get; private set; } = 1;
    public string Reason { get; private set; } = "disabled";
    private double lastTime = double.NaN, stationarySince = double.NaN, awakeUntil;
    private readonly InterfaceMotionStore motion = new();
    private int frame;
    public int AnimationActive => motion.Active;
    public int AnimationKeys => motion.Count;
    public void Wake(double now) {
        awakeUntil = now + .6;
        ClickThrough = false;
    }

    public void Update(MapDrawingConfig config, double now, bool moving, bool hovered, bool focused, bool blocked,
        bool interacting = false, bool mapKey = false) {
        if (!double.IsFinite(now))
            return;
        if (!double.IsNaN(lastTime) && now < lastTime) {
            stationarySince = double.NaN;
            awakeUntil = 0;
            ClickThrough = false;
            Opacity = 1;
            motion.Reset();
        }
        lastTime = now;
        if (mapKey)
            Wake(now);
        if (moving)
            stationarySince = double.NaN;
        else if (double.IsNaN(stationarySince))
            stationarySince = now;
        var enabled = config.IsGratis && config.GhostMode;
        var delay = float.IsFinite(config.GhostRestoreDelay) ? Math.Clamp(config.GhostRestoreDelay, 0, 10) : .6f;
        var recentlyMoving = moving || ClickThrough && now - stationarySince < delay;
        var ghost = enabled && recentlyMoving && !hovered && !focused && !blocked && !interacting && now >= awakeUntil;
        Reason = !enabled ? "disabled" : hovered ? "hover" : focused ? "focus" : blocked ? "overlay/popup/drag"
            : interacting ? "interaction" : now < awakeUntil ? "map key" : ghost ? "moving/restore delay"
            : "stationary";
        ClickThrough = ghost;
        var fade = enabled && moving && !hovered && !focused && !blocked && !interacting && now >= awakeUntil;
        var next = fade ? (float.IsFinite(config.GhostOpacity) ? Math.Clamp(config.GhostOpacity, .05f, 1) : .35f) : 1;
        if (!enabled) {
            Opacity = 1;
            if (motion.Count > 0)
                motion.Reset();
            return;
        }
        motion.Begin(now, ++frame);
        var speed = Math.Clamp(float.IsFinite(config.AnimationSpeed) ? config.AnimationSpeed : 1, .5f, 1.5f);
        var duration = next > Opacity ? InterfaceMotionTokens.HoverEnter : InterfaceMotionTokens.HoverExit;
        Opacity = motion.Value(new(1), next, duration / speed,
            instant: !enabled || config.ReduceMotion || config.EffectiveInterfaceMotion != InterfaceAnimationMode.Full,
            initial: 1);

    }

    public string Diagnostics() => $"clickThrough={ClickThrough} opacity={Opacity:0.000} reason={Reason} flags="
        + (ClickThrough ? "NoInputs (NoMouseInputs|NoNavInputs|NoNavFocus)" : "None");
}