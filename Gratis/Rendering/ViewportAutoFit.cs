using System.Numerics;

namespace Gratis.Rendering;

public enum ViewportViewMode {
    AutoFit, Manual
}

public sealed class ViewportAutoFit {
    private ulong? map;
    private long session;
    private (Vector2 Frame, Vector2 Texture, float Dpi, float UiScale, MapContentBounds? Content, bool Circle,
        bool ContentFit, bool Clamp, bool Resize)
        ? previous;
    public ViewportViewMode Mode { get; private set; } = ViewportViewMode.AutoFit;
    public string Readiness { get; private set; } = "waiting-window";
    public string LastFitReason { get; private set; } = "not fitted";
    public string ManualReason { get; private set; } = "none";
    public double EnteredAt { get; private set; } = double.NaN;
    public double LastFitAt { get; private set; } = double.NaN;
    public double EvaluatedAt { get; private set; } = double.NaN;
    public double ManualAt { get; private set; } = double.NaN;
    public bool Observe(ulong key, long generation, double now) {
        if (map == key && session == generation)
            return false;
        map = key;
        session = generation;
        Resume(now, "map/session changed");
        return true;
    }

    public void Invalidate() => map = null;
    public void Resume(double now, string reason) {
        Mode = ViewportViewMode.AutoFit;
        EnteredAt = now;
        LastFitAt = double.NaN;
        LastFitReason = reason;
        ManualReason = "none";
        previous = null;
    }

    public void Manual(double now, string reason) {
        if (Mode == ViewportViewMode.Manual)
            return;
        Mode = ViewportViewMode.Manual;
        ManualAt = now;
        ManualReason = reason;
    }

    public void Wait(double now, bool validWindow) {
        EvaluatedAt = now;
        Readiness = validWindow ? "waiting-texture" : "waiting-window";
    }

    public void Evaluate(MapDrawingConfig config, Vector2 frame, Vector2 texture, float dpi, MapContentBounds? bounds,
        double now) {
        Readiness = "ready";
        EvaluatedAt = now;
        if (Mode != ViewportViewMode.AutoFit)
            return;
        var next = (Frame: frame, Texture: texture, Dpi: dpi, UiScale: config.ChromeScale, Content: bounds,
            Circle: config.MapCircle, ContentFit: config.FitSmallRoomsToContent, Clamp: config.ClampToMap,
            Resize: config.ScaleWithFrame);
        var old = previous.GetValueOrDefault();
        if (previous is null || old != next) {
            LastFitReason = previous is null ? "first ready geometry" : old.Frame != frame ? "window/preset resized"
                : old.Texture != texture ? "texture dimensions changed"
                : old.Dpi != dpi || old.UiScale != config.ChromeScale ? "DPI/UI scale changed" : old.Content != bounds
                ? "content bounds changed" : "viewport settings changed";
            LastFitAt = now;
            previous = next;
        }
    }

    public string Diagnostics() => $"AutoFit={Mode} ready={Readiness} lastFitReason={LastFitReason} "
        + $"enteredAt={Time(EnteredAt)} lastFitAt={Time(LastFitAt)} "
        + $"evaluatedAt={Time(EvaluatedAt)} manualAt={Time(ManualAt)} manualReason={ManualReason} session={session}";
    private static string Time(double value) => double.IsFinite(value)
        ? value.ToString("0.###", global::System.Globalization.CultureInfo.InvariantCulture) : "none";
}