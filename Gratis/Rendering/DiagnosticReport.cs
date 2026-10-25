using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Gratis.Data;

namespace Gratis.Rendering;

public readonly record struct ReportLogEntry(DateTimeOffset Time, string Level, string Template, string ExceptionType);

public sealed class ReportLogBuffer
{
    public const int Capacity = 200;
    private readonly ReportLogEntry[] entries = new ReportLogEntry[Capacity];
    private readonly object gate = new();
    private int next, count;
    public void Add(string level, string template, string exceptionType = "none")
    {
        if (level is not ("Warning" or "Error" or "Fatal")) return;
        lock (gate) {
            entries[next] = new(DateTimeOffset.UtcNow, level, ReportPrivacy.Clean(template, 320), ReportPrivacy.Clean(exceptionType, 80));
            next = (next + 1) % Capacity; count = Math.Min(Capacity, count + 1);
        }
    }
    public ReportLogEntry[] Snapshot()
    {
        lock (gate) {
            var result = new ReportLogEntry[count];
            for (var i = 0; i < count; i++) result[i] = entries[(next - 1 - i + Capacity) % Capacity];
            return result;
        }
    }
}

public static class ReportPrivacy
{
    private static readonly Regex Paths = new(@"[A-Za-z]:[\\/][^\r\n;]*|/(?:Users|home|tmp)/[^\r\n;]*", RegexOptions.Compiled);
    private static readonly Regex Identifiers = new(@"\b(?:0x[0-9a-fA-F]{8,}|[0-9a-fA-F]{16,}|\d{9,})\b", RegexOptions.Compiled);
    public static string Clean(string text, int limit = 4096)
    {
        text = text.Length > limit ? text[..limit] + " [truncated]" : text;
        return Identifiers.Replace(Paths.Replace(text, "[path omitted]"), "[identifier omitted]")
            .Replace('`', '\'').Replace('\r', ' ').Replace('\n', ' ').Replace('\0', ' ');
    }
}

public interface IReportContext
{
    string Version { get; }
    string Commit { get; }
    SystemConfig Settings { get; }
    IEnumerable<(string Name, string State)> States { get; }
    IReadOnlyList<ReportLogEntry> RecentLogs { get; }
}

public static class DiagnosticReport
{
    public const int MaximumCharacters = 64 * 1024;
    private const int BodyLimit = MaximumCharacters - 1024;
    public static string Build(IReportContext context)
    {
        var text = new StringBuilder();
        void Line(string line = "") {
            if (text.Length >= BodyLimit) return;
            var remaining = BodyLimit - text.Length;
            text.Append(line.AsSpan(0, Math.Min(line.Length, Math.Max(0, remaining - 1)))).Append('\n');
        }
        Line("# Gratis diagnostic report");
        Line($"UTC: {DateTimeOffset.UtcNow:O}");
        var commit = context.Commit.Length is >= 7 and <= 64 && context.Commit.All(Uri.IsHexDigit) ? context.Commit[..Math.Min(12, context.Commit.Length)] : "unknown";
        Line($"Version: {ReportPrivacy.Clean(context.Version, 80)}; commit: {commit}");
        Line("Privacy: free-form settings, list contents, log values, exception messages and stack traces omitted.");
        Line(); Line("## Runtime state");
        foreach (var (name, state) in context.States.Take(24)) {
            Line(); Line("### " + ReportPrivacy.Clean(name, 64)); Line("```text");
            Line(ReportPrivacy.Clean(state, 6000)); Line("```");
        }
        Line(); Line("## Settings");
        Line("**Non-default** values are bold. Collections show counts; text values are omitted.");
        Line(); Line("| Setting | Value | Default |\n| --- | --- | --- |");
        var defaults = new SystemConfig();
        void Settings(object? value, object? baseline, Type type, string prefix, int depth)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.Name, StringComparer.Ordinal)) {
                if (field.DeclaringType?.Assembly != typeof(MapDrawingConfig).Assembly) continue;
                var path = prefix + field.Name;
                var current = value is null ? null : field.GetValue(value);
                var original = baseline is null ? null : field.GetValue(baseline);
                if (depth < 3 && field.FieldType.IsClass && field.FieldType != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(field.FieldType)) {
                    Settings(current, original, field.FieldType, path + ".", depth + 1); continue;
                }
                var display = Format(current); var normal = Format(original);
                var changed = !Equivalent(current, original);
                Line($"| {path} | {(changed ? "**" + display + "**" : display)} | {normal} |");
            }
        }
        Settings(context.Settings, defaults, typeof(SystemConfig), "", 0);
        Line(); Line("## Recent Gratis warnings/errors (newest first; maximum 200)");
        var logs = context.RecentLogs.Take(ReportLogBuffer.Capacity).ToArray();
        if (logs.Length == 0) Line("None recorded in this plugin session.");
        foreach (var entry in logs)
            Line($"- {entry.Time:HH:mm:ss.fff} {entry.Level}: {ReportPrivacy.Clean(entry.Template, 320)} (exception: {ReportPrivacy.Clean(entry.ExceptionType, 80)})");
        if (text.Length >= BodyLimit) text.Append("\n[Report truncated at size limit.]\n");
        return text.ToString();
    }
    private static bool Equivalent(object? a, object? b, int depth = 0)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null || a.GetType() != b.GetType()) return false;
        if (a is string || a.GetType().IsValueType) return a.Equals(b);
        if (depth >= 4) return false;
        if (a is ICollection ac && b is ICollection bc) {
            if (ac.Count != bc.Count || ac.Count > 200) return false;
            var left = ac.GetEnumerator(); var right = bc.GetEnumerator();
            try { while (left.MoveNext() && right.MoveNext()) if (!Equivalent(left.Current, right.Current, depth + 1)) return false; }
            finally { (left as IDisposable)?.Dispose(); (right as IDisposable)?.Dispose(); }
            return true;
        }
        return a.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
            .All(field => Equivalent(field.GetValue(a), field.GetValue(b), depth + 1));
    }
    private static string Format(object? value) => value switch {
        null => "null",
        string text => $"[text omitted; {text.Length} chars]",
        bool flag => flag ? "true" : "false",
        Enum option => Convert.ToInt64(option, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) + " (" + option + ")",
        float n => n.ToString("G6", CultureInfo.InvariantCulture), double n => n.ToString("G8", CultureInfo.InvariantCulture),
        Vector2 v => FormattableString.Invariant($"({v.X:G6}, {v.Y:G6})"),
        Vector3 v => FormattableString.Invariant($"({v.X:G6}, {v.Y:G6}, {v.Z:G6})"),
        Vector4 v => FormattableString.Invariant($"({v.X:G6}, {v.Y:G6}, {v.Z:G6}, {v.W:G6})"),
        global::System.Drawing.Color color => $"#{color.ToArgb():X8}",
        ICollection items => $"{items.Count} entries (contents omitted)",
        byte or sbyte or short or ushort or int or uint => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        long or ulong => "[identifier omitted]",
        _ => "[object omitted]",
    };
    public static string WithScreenshot(string body, string status) =>
        body + "\n## Window screenshot\n" + ReportPrivacy.Clean(status, 512) + "\n";
}
