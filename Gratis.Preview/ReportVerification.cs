using System.Text;
using Gratis.Data;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class ReportVerification
{
    private sealed class Fake(ReportLogBuffer buffer, bool huge = false) : IReportContext
    {
        public string Version => "3.2.0";
        public string Commit => "eebb39d1234567890abcdef01234567890abcdef01";
        public SystemConfig Settings { get; } = new() { FitSmallRoomsToContent = false, TitleSeparator = "Secret Player", ActiveLens = "Secret Player" };
        public IReadOnlyList<ReportLogEntry> RecentLogs => buffer.Snapshot();
        public IEnumerable<(string Name, string State)> States => huge
            ? Enumerable.Range(0, 30).Select(i => ("State " + i, new string('x', 100000)))
            : new[] { ("Fonts", "handles=8/16 estimatedArea=80000/4194304 budgetWarning=False defaultFallbacks=2"),
                ("Marker paths", "glyph+plate=20 shape=0"), ("Glass", "blurUnavailable=False"), ("Chrome", "progress=1"),
                ("Motion", "min/max/fit=.25/4/.77 markerScale=.77"), ("Zone", "selected=1/999"),
                ("Spotlight", "source=Quest"), ("Lens", "auto=False"), ("Viewport", "zoom=.77 window=540x1030"),
                ("Camera", "cameraAngle=-30 characterAngle=90 source=Camera fallback=False"),
                ("Arrow smoothing", "raw=90 smoothed=30 tau=.25 level=Medium reduceMotion=False resetCount=1 lastReset=initial-sample AdvanceCallsPerFrame=1") };
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public static void Run()
    {
        var buffer = new ReportLogBuffer();
        for (var i = 0; i < 500; i++) buffer.Add("Warning", "failure " + i, "InvalidOperationException");
        buffer.Add("Information", "not a warning");
        var snapshot = buffer.Snapshot();
        Check(snapshot.Length == 200 && snapshot[0].Template == "failure 499" && snapshot[^1].Template == "failure 300", "Ring must retain exactly 200 latest warnings/errors");
        buffer.Add("Error", "Cannot load C:\\Users\\Secret Player\\settings.json; account=1234567890123456", "IOException");
        buffer.Add("Warning", "Failed operation for {CharacterName}", "InvalidOperationException");
        var report = DiagnosticReport.Build(new Fake(buffer));
        Check(report.Contains("| FitSmallRoomsToContent | **false** | true |") && report.Contains("commit: eebb39d12345"), "Report must highlight non-default settings and retain commit");
        Check(!report.Contains("Secret Player") && !report.Contains("1234567890123456") && !report.Contains("C:\\Users"), "Report must omit names, filesystem paths and identity numbers");
        foreach (var name in new[] { "Fonts", "Marker paths", "Glass", "Chrome", "Motion", "Zone", "Spotlight", "Lens", "Viewport", "Camera", "Arrow smoothing" })
            Check(report.Contains("### " + name), "Missing report state " + name);
        var huge = DiagnosticReport.WithScreenshot(DiagnosticReport.Build(new Fake(buffer, true)), "Unavailable: map closed.");
        Check(huge.Length <= DiagnosticReport.MaximumCharacters && Encoding.UTF8.GetByteCount(huge) <= DiagnosticReport.MaximumCharacters * 3 && huge.Contains("truncated"), "Report must remain bounded");
        Check(huge.Contains("Unavailable: map closed."), "Capture failure must leave a report with a useful status");
        Directory.CreateDirectory("_preview");
        File.WriteAllText("_preview/report-selftest.md", DiagnosticReport.WithScreenshot(report, "Unavailable in headless harness."));
        Console.WriteLine("Report: fake context, all state sections, non-default settings, privacy, latest-200 ring, size cap and unavailable-capture status passed.");
    }
}
