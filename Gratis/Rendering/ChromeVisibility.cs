using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

[Flags]
public enum ChromeActivity { None = 0, Hover = 1, Drag = 2, Settings = 4, FocusedActiveItem = 8, WindowMove = 16 }

public sealed class ChromeVisibility
{
    private double lastActivity = double.NaN;
    private double lastUpdate = double.NaN;
    private readonly Dictionary<string, long> reasons = new(StringComparer.Ordinal);
    private long settingsFrames;
    private long clockResets;
    public float ParentAlpha { get; private set; } = 1;
    public float Progress { get; private set; } = 1;

    public void Reveal(double now, [CallerFilePath] string callerFile = "", [CallerMemberName] string caller = "")
    {
        var source = Path.GetFileNameWithoutExtension(callerFile);
        Count($"external:{source}.{caller}");
        if (source is "MapToolbarDrawing" or "MapContextMenu") Count("popup");
        if (source == "HoverCardDrawing") Count("hovercard");
        SetVisible(now);
    }

    private void SetVisible(double now)
    {
        if (!double.IsFinite(now)) return;
        lastActivity = now;
        Progress = 1;
    }

    public void Update(MapDrawingConfig config, double now, bool interacting, float? fixedProgress = null) =>
        Update(config, now, interacting ? ChromeActivity.Hover : ChromeActivity.None, fixedProgress);

    public void Update(MapDrawingConfig config, double now, ChromeActivity activity, float? fixedProgress = null, float parentAlpha = 1)
    {
        ParentAlpha = Finite(parentAlpha, 1, 0, 1);
        if (!double.IsFinite(now)) return;
        if (double.IsFinite(lastUpdate) && now < lastUpdate) clockResets++;
        if (activity.HasFlag(ChromeActivity.Settings)) settingsFrames++;
        if (activity.HasFlag(ChromeActivity.WindowMove)) Count("window-move");
        var interacting = false;
        foreach (var (flag, name) in ActivityReasons) {
            if ((activity & flag) == 0) continue;
            Count(name);
            interacting = true;
        }
        if (!double.IsFinite(lastActivity) || now < lastUpdate || interacting ||
            !config.IsGratis || !config.AutoHideChrome || config.PinChrome) SetVisible(now);
        lastUpdate = now;
        var delay = Finite(config.ChromeIdleDelay, 1.2f, 0, 10);
        var duration = Finite(config.ChromeFadeDurationMs, 180, 0, 2000) / 1000;
        if (config.IsGratis) {
            duration /= Finite(config.AnimationSpeed, 1, .5f, 1.5f);
            if (config.ReduceMotion || config.EffectiveInterfaceMotion == InterfaceAnimationMode.Off) duration = 0;
            else if (config.EffectiveInterfaceMotion == InterfaceAnimationMode.Reduced) duration = MathF.Min(duration, .06f);
        }
        var elapsed = now - lastActivity - delay;
        var t = elapsed <= 0 ? 0 : duration <= 0 ? 1 : (float)Math.Clamp(elapsed / duration, 0, 1);
        Progress = fixedProgress is { } value && float.IsFinite(value) ? Math.Clamp(value, 0, 1) : 1 - t * t * (3 - 2 * t);
    }

    private static readonly (ChromeActivity Flag, string Name)[] ActivityReasons = [
        (ChromeActivity.Hover, "hover"), (ChromeActivity.Drag, "drag"), (ChromeActivity.FocusedActiveItem, "focused-active-item"),
    ];
    private void Count(string reason) => reasons[reason] = reasons.GetValueOrDefault(reason) + 1;

    public static bool IsMapFrameHovered()
    {
        var (min, max) = Classes.MapShape.Bounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        return ImGui.IsMouseHoveringRect(min, max, false) && Classes.MapShape.Contains(ImGui.GetMousePos()) &&
            ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
    }

    public double SecondsSinceActivity(double now) => double.IsFinite(now) && double.IsFinite(lastActivity) ? Math.Max(0, now - lastActivity) : 0;

    public string TakeDiagnostics(MapDrawingConfig config, double now)
    {
        foreach (var reason in new[] { "hover", "drag", "settings", "popup", "hovercard", "focused-active-item", "window-move" })
            reasons.TryAdd(reason, 0);
        var counts = string.Join(",", global::System.Linq.Enumerable.Select(reasons, pair => $"{pair.Key}={pair.Value}"));
        var line = global::System.FormattableString.Invariant($"Progress={Progress:0.000} idle={SecondsSinceActivity(now):0.00}s alpha(title/controls)={ParentAlpha * TitleAlpha(config):0.000}/{ParentAlpha * ControlsAlpha(config):0.000} AutoHide={config.AutoHideChrome} Pin={config.PinChrome} delay={config.ChromeIdleDelay:0.##}s fade={config.ChromeFadeDurationMs:0.##}ms [{counts}] settings-open={settingsFrames} clock-reset={clockResets}");
        reasons.Clear();
        settingsFrames = clockResets = 0;
        return line;
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
        public bool Visible { get; }
        public bool Interactive { get; }
        public AlphaScope(bool enabled, float alpha)
        {
            pushed = enabled;
            var effective = ImGui.GetStyle().Alpha * Finite(alpha, 0, 0, 1);
            Visible = !enabled || effective > 0;
            Interactive = !enabled || effective >= .05f;
            if (pushed) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, effective);
        }
        public void Dispose() { if (pushed) ImGui.PopStyleVar(); }
    }
}
