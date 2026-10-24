namespace Gratis.Rendering;

public sealed class MotionSettings
{
    public bool Pings = true;
    public float PingSeconds = .9f;
    public bool PlayerTrail = true;
    public float TrailSeconds = 4;
    public float TrailOpacity = .22f;
    public bool EaseFollow = true;
    public bool EaseZoom = true;
    public float SettleSeconds = .12f;
    public bool ZoneTransition = true;
    public float ZoneSeconds = .18f;
    public float ZoneSlidePixels = 8;
}
