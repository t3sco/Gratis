using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using Gratis.Data;
using Gratis.Rendering;
using KamiLib.CommandManager;

namespace Gratis.Classes;

internal sealed class ReportCommand : IDisposable
{
    [PluginService] private IChatGui ChatGui { get; set; } = null!;
    private readonly ScreenshotCapture capture;
    private readonly ReportLogBuffer logs;
    private readonly CommandHandler command;
    private bool pending;
    private bool disposed;
    public ReportCommand(ScreenshotCapture capture, ReportLogBuffer logs)
    {
        this.capture = capture; this.logs = logs;
        Service.PluginInterface.Inject(this);
        command = new CommandHandler { ActivationPath = "/report", Delegate = _ => pending = true };
        System.CommandManager.RegisterCommand(command);
        Service.PluginInterface.UiBuilder.Draw += OnDraw;
    }
    private void OnDraw()
    {
        if (!pending || disposed) return;
        pending = false;
        try {
            var directory = Path.Combine(Service.PluginInterface.GetPluginConfigDirectory(), "reports");
            Directory.CreateDirectory(directory);
            var stem = $"gratis-report-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}";
            var path = Path.Combine(directory, stem + ".md");
            var png = Path.Combine(directory, stem + ".png");
            TextDrawing.SampleDiagnostics();
            var body = DiagnosticReport.Build(new Context(logs));
            void Write(string status) => File.WriteAllText(path, DiagnosticReport.WithScreenshot(body, status), new UTF8Encoding(false));
            Write("Capture requested; pending next rendered map frame.");
            if (!capture.RequestReport(png, success => Write(success ? $"Saved alongside this report: {stem}.png" : "Unavailable: map capture failed (see recent warnings/errors).")))
                Write("Unavailable: capture busy, map closed, or a text-entry overlay is open.");
            ChatGui.Print($"[Gratis] Report saved: {path}");
        }
        catch (Exception error) {
            Service.Log.Error(error, "[Gratis] Diagnostic report could not be written.");
            ChatGui.PrintError("[Gratis] Could not write the diagnostic report; see the plugin log.");
        }
    }
    private sealed class Context(ReportLogBuffer logs) : IReportContext
    {
        private static string Information => typeof(GratisPlugin).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        public string Version => Information;
        public string Commit => Information.Contains('+') ? Information[(Information.IndexOf('+') + 1)..] : "unknown";
        public SystemConfig Settings => System.SystemConfig;
        public IReadOnlyList<ReportLogEntry> RecentLogs => logs.Snapshot();
        private static string SafeLensName(string name)
        {
            foreach (var lens in Lenses.BuiltIn) if (name == lens.Name) return lens.Name;
            return "custom (name omitted)";
        }
        public IEnumerable<(string Name, string State)> States {
            get {
                var ctx = MapDrawContext.Current;
                yield return ("Fonts", ctx.TextDiagnostics());
                yield return ("Marker paths", ctx.MarkerRendering.Diagnostics());
                yield return ("Glass", ctx.Blur.Status(Settings, false));
                yield return ("Chrome", ctx.Chrome.TakeDiagnostics(Settings, ImGui.GetTime()));
                yield return ("Motion", System.MapRenderer.TakeMotionDiagnostics());
                yield return ("Camera", System.MapRenderer.CameraDiagnostics());
                yield return ("Arrow smoothing", System.MapRenderer.HeadingDiagnostics());
                yield return ("Zone", System.ZoneSwitchController?.Diagnostics() ?? "unavailable");
                yield return ("Spotlight", System.MapRenderer.Frame.Spotlight.Diagnostics());
                var visibility = ctx.Visibility;
                var states = $"active={SafeLensName(Settings.ActiveLens)} effective={SafeLensName(visibility.EffectiveLens)} auto={Settings.LensAuto} tint={Settings.LensTintsMap} peek={visibility.Peek} cycleKey={Settings.LensCycleKey} custom={Settings.CustomLenses?.Count ?? 0}; ";
                for (var i = 0; i < MarkerVisibility.GroupCount; i++)
                    states += $"{(MarkerGroup)i}={visibility.State((MarkerGroup)i)} count={visibility.Counts[i]} filter={visibility.Overrides[i]?.ToString() ?? "lens"}; ";
                yield return ("Lens", states);
                yield return ("Viewport", System.MapRenderer.Frame.ViewportDiagnostics() + $" globalScale={ctx.GlobalScale:0.###} chromeScale={Settings.ChromeScale:0.###}");
                yield return ("View memory", System.MapRenderer.Frame.ViewMemoryDiagnostics());
            }
        }
    }
    public void Dispose()
    {
        disposed = true;
        Service.PluginInterface.UiBuilder.Draw -= OnDraw;
        System.CommandManager.RemoveCommand(command);
    }
}
