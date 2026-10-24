using System;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed class ChromeVisibility
{
    private double lastActivity = double.NaN;
    public float Progress { get; private set; } = 1;

    public void Reveal(double now)
    {
        lastActivity = now;
        Progress = 1;
    }

    public void Update(MapDrawingConfig config, double now, bool interacting, float? fixedProgress = null)
    {
        if (double.IsNaN(lastActivity) || now < lastActivity || interacting ||
            !config.IsGratis || !config.AutoHideChrome || config.PinChrome) Reveal(now);
        var delay = Finite(config.ChromeIdleDelay, 1.2f, 0, 10);
        var duration = Finite(config.ChromeFadeDurationMs, 180, 0, 2000) / 1000;
        var elapsed = now - lastActivity - delay;
        var t = elapsed <= 0 ? 0 : duration <= 0 ? 1 : (float)Math.Clamp(elapsed / duration, 0, 1);
        Progress = fixedProgress is { } value ? Math.Clamp(value, 0, 1) : 1 - t * t * (3 - 2 * t);
    }

    public float TitleAlpha(MapDrawingConfig config) => Alpha(config, config.ChromeTitleMinAlpha, .35f);
    public float ControlsAlpha(MapDrawingConfig config) => Alpha(config, config.ChromeControlsMinAlpha, 0);
    private float Alpha(MapDrawingConfig config, float minimum, float fallback) =>
        !config.IsGratis || !config.AutoHideChrome || config.PinChrome ? 1 :
        Finite(minimum, fallback, 0, 1) + (1 - Finite(minimum, fallback, 0, 1)) * Progress;
    private static float Finite(float value, float fallback, float min, float max) =>
        Math.Clamp(float.IsFinite(value) ? value : fallback, min, max);

    public readonly struct AlphaScope : IDisposable
    {
        private readonly bool pushed;
        public AlphaScope(bool enabled, float alpha)
        {
            pushed = enabled;
            if (pushed) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * MathF.Max(alpha, .0001f));
        }
        public void Dispose() { if (pushed) ImGui.PopStyleVar(); }
    }
}
