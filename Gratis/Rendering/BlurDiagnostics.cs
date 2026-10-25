using System;
using System.Globalization;

namespace Gratis.Rendering;

public enum BlurRefusal {
    Clip, Circular, Alpha, Config, Host
}
public enum GlassSurface {
    Other, Toolbar, Title, Coordinates, Window
}

public sealed class BlurDiagnostics {
    private readonly int[] refusals = new int[5];
    private readonly string[] surfaces = ["not-drawn", "not-drawn", "not-drawn", "not-drawn", "not-drawn"];
    private int retryFrame;
    public int Frame { get; private set; } = -1;
    public int Blurred { get; private set; }
    public int Refused { get; private set; }
    public bool BlurUnavailable { get; private set; }
    public string LastException { get; private set; } = "none";
    public float LastStrength { get; private set; }
    public int Count(BlurRefusal reason) => refusals[(int)reason];

    public void BeginFrame(int frame) {
        if (Frame == frame)
            return;
        Frame = frame;
        Blurred = Refused = 0;
        Array.Clear(refusals);
        Array.Fill(surfaces, "not-drawn");
    }

    public bool Refuse(BlurRefusal reason, GlassSurface surface = GlassSurface.Other) {
        refusals[(int)reason]++;
        Refused++;
        surfaces[(int)surface] = reason switch {
            BlurRefusal.Clip => "clip",
            BlurRefusal.Circular => "circular",
            BlurRefusal.Alpha => "alpha",
            BlurRefusal.Config => "config",
            _ => "host",
        };
        return false;
    }

    public void Recorded(GlassSurface surface = GlassSurface.Other) {
        Blurred++;
        surfaces[(int)surface] = "blurred";
    }

    public string SurfaceStatus(GlassSurface surface) => surfaces[(int)surface];
    public static float Strength(float radius, float opacity) => Math.Clamp(radius * opacity / 4, .05f, 8);
    public void Attempt(float strength) => LastStrength = strength;
    public bool CanAttempt(int frame) => !BlurUnavailable || frame >= retryFrame;
    public void Succeeded() => BlurUnavailable = false;
    public void Failed(Exception error, int frame) {
        BlurUnavailable = true;
        retryFrame = frame + 120;
        LastException = $"{error.GetType().Name}: {error.Message.Replace('\r', ' ').Replace('\n', ' ')}";
        if (LastException.Length > 160)
            LastException = LastException[..157] + "...";
    }

    public string Status(MapDrawingConfig config, bool includeExceptionMessage = true) =>
        string.Create(CultureInfo.InvariantCulture,
            $"GlassEnabled={config.GlassEnabled} GlassBlur={config.GlassBlur} blurUnavailable={BlurUnavailable} "
                + $"lastException={(includeExceptionMessage ? LastException : LastException.Split(':')[0])}; "
                + $"strength={LastStrength:0.##} frame={Frame} blurred={Blurred} refused={Refused} "
                + $"(clip={Count(BlurRefusal.Clip)},circular={Count(BlurRefusal.Circular)},"
                + $"alpha={Count(BlurRefusal.Alpha)},config={Count(BlurRefusal.Config)},host={Count(BlurRefusal.Host)}) "
                + $"toolbar={SurfaceStatus(GlassSurface.Toolbar)} title={SurfaceStatus(GlassSurface.Title)} "
                + $"coordinates={SurfaceStatus(GlassSurface.Coordinates)} window={SurfaceStatus(GlassSurface.Window)}");
}