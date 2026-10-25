namespace Gratis.Rendering;

public sealed class MotionSettings {
    public bool Pings = true;
    public float PingSeconds = .9f;
    public bool PlayerTrail = true;
    public float TrailSeconds = 8.3f;
    public float TrailOpacity = .33f;
    public bool EaseFollow = true;
    public bool EaseZoom = true;
    public bool PanInertia = true;
    public float PanInertiaStrength = .35f;
    public float SettleSeconds = .12f;
    public bool ZoneTransition = true;
    public float ZoneSeconds = .18f;
    public float ZoneSlidePixels = 8;
}