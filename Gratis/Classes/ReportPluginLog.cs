using System;
using Dalamud.Plugin.Services;
using Gratis.Rendering;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Gratis.Classes;

// Wrap only this plugin's logger. Never subscribe to Dalamud/global log output.
internal sealed class ReportPluginLog : IPluginLog, IDisposable {
    private sealed class Sink(ReportLogBuffer buffer) : ILogEventSink {
        public void Emit(LogEvent entry) =>
            buffer.Add(entry.Level.ToString(), entry.MessageTemplate.Text, entry.Exception?.GetType().Name ?? "none");
    }
    private readonly IPluginLog original;
    private readonly Logger logger;
    public ReportPluginLog(IPluginLog original, ReportLogBuffer buffer) {
        this.original = original;
        logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Logger(original.Logger).WriteTo.Sink(
            new Sink(buffer),
            LogEventLevel.Warning).CreateLogger();
    }

    public ILogger Logger => logger;
    public LogEventLevel MinimumLogLevel {
        get => original.MinimumLogLevel; set => original.MinimumLogLevel = value;
    }

    public void Write(LogEventLevel level, Exception? error, string template, params object[] values) =>
        logger.Write(level, error, template, values);
    public void Fatal(string template, params object[] values) => Write(LogEventLevel.Fatal, null, template, values);
    public void Fatal(Exception? error, string template, params object[] values) =>
        Write(LogEventLevel.Fatal, error, template, values);
    public void Error(string template, params object[] values) => Write(LogEventLevel.Error, null, template, values);
    public void Error(Exception? error, string template, params object[] values) =>
        Write(LogEventLevel.Error, error, template, values);
    public void Warning(string template, params object[] values) =>
        Write(LogEventLevel.Warning, null, template, values);
    public void Warning(Exception? error, string template, params object[] values) =>
        Write(LogEventLevel.Warning, error, template, values);
    public void Information(string template, params object[] values) =>
        Write(LogEventLevel.Information, null, template, values);
    public void Information(Exception? error, string template, params object[] values) =>
        Write(LogEventLevel.Information, error, template, values);
    public void Info(string template, params object[] values) => Information(template, values);
    public void Info(Exception? error, string template, params object[] values) => Information(error, template, values);
    public void Debug(string template, params object[] values) => Write(LogEventLevel.Debug, null, template, values);
    public void Debug(Exception? error, string template, params object[] values) =>
        Write(LogEventLevel.Debug, error, template, values);
    public void Verbose(string template, params object[] values) =>
        Write(LogEventLevel.Verbose, null, template, values);
    public void Verbose(Exception? error, string template, params object[] values) =>
        Write(LogEventLevel.Verbose, error, template, values);
    public void Dispose() {
        Service.Log = original;
        logger.Dispose();
    }
}