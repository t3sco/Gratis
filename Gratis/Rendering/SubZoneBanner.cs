using System;

namespace Gratis.Rendering;

public sealed record SubZoneLocation(uint MapId, uint AreaPlaceNameId, uint SubAreaPlaceNameId,
    string MapName, string AreaName, string SubAreaName);

public sealed class SubZoneBanner
{
    public SubZoneLocation? Location { get; private set; }
    public string Text { get; private set; } = "";
    public float Opacity { get; private set; }
    private double began = double.NegativeInfinity, lastTime = double.NaN;
    public void Update(MapDrawingConfig config, SubZoneLocation? location, double now)
    {
        if (!double.IsFinite(now)) return;
        var old = Location; Location = location;
        if (!config.IsGratis || !config.SubZoneBanner || location is null || location.MapId == 0 || now < lastTime) {
            began = double.NegativeInfinity; Opacity = 0; Text = ""; lastTime = now; return;
        }
        lastTime = now;
        if (old?.MapId != location.MapId) { began = double.NegativeInfinity; Text = ""; }
        else if (old.AreaPlaceNameId != location.AreaPlaceNameId || old.SubAreaPlaceNameId != location.SubAreaPlaceNameId) {
            var local = !string.IsNullOrWhiteSpace(location.SubAreaName) ? location.SubAreaName : location.AreaName;
            Text = string.IsNullOrWhiteSpace(local) ? "" : string.IsNullOrWhiteSpace(location.MapName) ? local : location.MapName + " · " + local;
            began = now;
        }
        var age = now - began;
        var speed = Math.Clamp(float.IsFinite(config.AnimationSpeed) ? config.AnimationSpeed : 1, .5f, 1.5f);
        var duration = InterfaceMotionTokens.Standard / speed;
        var instant = config.ReduceMotion || config.EffectiveInterfaceMotion != InterfaceAnimationMode.Full;
        Opacity = Text.Length == 0 || age >= 1.8 ? 0 : instant ? 1 :
            InterfaceMotionTokens.Ease((float)Math.Clamp(age / duration, 0, 1), InterfaceCurve.Enter) *
            (1 - InterfaceMotionTokens.Ease((float)Math.Clamp((age - (1.8 - duration)) / duration, 0, 1), InterfaceCurve.Exit));

    }
    public string Diagnostics() => $"map={Location?.MapId} AreaPlaceNameId={Location?.AreaPlaceNameId} " +
        $"SubAreaPlaceNameId={Location?.SubAreaPlaceNameId} alpha={Opacity:0.00} text={Text}";
}
