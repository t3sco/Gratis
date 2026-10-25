using System.Text.Json;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;

namespace Gratis.Preview;

internal static class TweakVerification {
    public static void Run() {
        static void Check(bool condition, string message) {
            if (!condition)
                throw new InvalidOperationException(message);
        }
        var defaults = new SystemConfig();
        var config = new SystemConfig();
        var all = TweakCatalog.All;
        Check(all.Count >= 24 && all.Select(t => t.Id).Distinct().Count() == all.Count
            && all.Select(t => t.ConfigPath).Distinct().Count() == all.Count,
            "Catalog IDs and config bindings must be unique");
        Check(TweakCatalog.Find(config, modifiedOnly: true).Length == 0, "New config must have no modified tweaks");
        foreach (var tweak in all) {
            Check(TweakCatalog.Groups.Contains(tweak.Group) && !string.IsNullOrWhiteSpace(tweak.Name)
                && !string.IsNullOrWhiteSpace(tweak.Description) && GratisIcons.GlyphRanges.Contains(tweak.Icon),
                "Incomplete tweak metadata or new atlas glyph");
            Check(tweak.Default == tweak.Get(defaults), "Tweak default differs from config: " + tweak.Id);
            tweak.Set(config, !tweak.Default);
            var modified = TweakCatalog.Find(config, modifiedOnly: true);
            if (tweak.Locked)
                Check(tweak.Get(config) && modified.Length == 0, "Mandatory policy cannot be disabled");
            else
                Check(modified.Length == 1 && modified[0] == tweak,
                    "Tweak must be independently editable: " + tweak.Id);
            tweak.Reset(config);
            Check(tweak.Get(config) == tweak.Default && !tweak.IsModified(config), "Reset missed default: " + tweak.Id);
            if (tweak.ConfigureSection is not null) {
                var layout = new SettingsLayout();
                layout.ConfigureTweak(tweak);
                Check(SettingsLayout.Sections[layout.SelectedSection] == tweak.ConfigureSection
                    && SettingsLayout.Find(config, tweak.ConfigureSection,
                        layout.Search).Any(e => e.Path == tweak.ConfigPath),
                    "Configure must reveal existing controls and sub-options: " + tweak.Id);
            }
        }
        Check(!all.Any(t => t.ConfigPath is "FlagAutoClear" or "ShowPlayers" or "ShowPlayerIcon"
            || t.ConfigPath.StartsWith("Filters.") || t.ConfigPath.StartsWith("Markers.")),
            "Tweak admission must not hide priority markers");
        Check(config.TravelActionsInitialized, "Explicit travel reset must survive optional integration detection");
        Check(TweakCatalog.Find(config, "ghost")[0].Id == "ghost", "Search must rank names above description matches");
        Check(TweakCatalog.Find(config, "FLAG")[0].Name.StartsWith("Flag"),
            "Search must ignore case and prefer name prefixes");
        Check(TweakCatalog.Find(config, "eorzea time").Any(t => t.Id == "eorzea-time"), "Search must match all words");
        Check(TweakCatalog.Find(config, "lifestream").Single().Id == "travel-actions",
            "Search must include integration notes");
        Check(TweakCatalog.Find(config, "unlikely-no-match").Length == 0, "Search must reject unrelated results");
        foreach (var tweak in all.Where(t => !t.Locked).Take(3))
            tweak.Set(config, !tweak.Default);
        Check(TweakCatalog.Find(config, "   ", true).Length == 3
            && TweakCatalog.Find(config, "flag", true).Single().Id == "flag-arrival",
            "Modified filter must intersect search results");
        config.LastSeenTweakVersion = "3.1.0";
        config.WindowPosition = new(123, 456);
        SettingsLayout.Reset(config, "Tweaks");
        Check(TweakCatalog.Find(config, modifiedOnly: true).Length == 0 && config.LastSeenTweakVersion == "3.1.0"
            && config.WindowPosition == new global::System.Numerics.Vector2(123, 456),
            "Reset must preserve news metadata and window state");

        var json = new JsonSerializerOptions { IncludeFields = true };
        var old = JsonSerializer.Deserialize<SystemConfig>(
            "{\"VisualStyle\":1,\"GhostMode\":true,\"FlagArrival\":false,\"Motion\":null,\"UnknownOldField\":true}",
            json)!;
        Check(!old.IsGratis && old.GhostMode && !old.FlagArrival && old.ShowWhatsNew && old.LastSeenTweakVersion == "",
            "Old saved config must keep explicit settings and inherit new defaults");
        var inertia = all.Single(t => t.Id == "drag-inertia");
        Check(inertia.Get(old) == inertia.Default, "Null old nested config must expose safe defaults");
        inertia.Set(old, false);
        inertia.Reset(old);
        Check(old.Motion is not null && old.Motion.PanInertia, "Null nested config must be repairable by toggle/reset");
        Check(TweakNews.HasUnseen(old), "Old config should have a quiet unseen badge");
        old.ShowWhatsNew = false;
        Check(!TweakNews.HasUnseen(old) && TweakNews.Unseen(old).Length > 0,
            "Disabling news must hide badge without consuming history");
        old.ShowWhatsNew = true;
        TweakNews.Dismiss(old);
        Check(!TweakNews.HasUnseen(old) && TweakNews.Unseen(old).Length == 0, "Dismiss must consume current releases");
        var loaded = JsonSerializer.Deserialize<SystemConfig>(JsonSerializer.Serialize(old, json), json)!;
        Check(!TweakNews.HasUnseen(loaded) && loaded.LastSeenTweakVersion == TweakNews.LatestVersion && loaded.GhostMode
            && !loaded.FlagArrival,
            "News dismissal and old settings must survive save/load");
        foreach (var version in new[] { "3.1.0", "invalid", "", null }) {
            config.LastSeenTweakVersion = version!;
            Check(TweakNews.HasUnseen(config), "Missing/old/malformed seen version should show available releases");
        }
        config.LastSeenTweakVersion = "99.0.0";
        Check(!TweakNews.HasUnseen(config), "Downgrade must not advertise old releases as new");
        foreach (var release in TweakNews.Releases) {
            Check(Version.TryParse(release.Version, out _) && DateOnly.TryParse(release.Date, out _)
                && !string.IsNullOrWhiteSpace(release.Notes) && release.TweakIds.All(id => all.Any(t => t.Id == id)),
                "Release metadata references unknown tweaks");
        }
        Check(all.All(t => TweakNews.Releases.Any(r => r.TweakIds.Contains(t.Id)))
            && TweakNews.Releases.Select(r => r.Version).Distinct().Count() == TweakNews.Releases.Count,
            "Every new tweak must be assigned to one versioned release entry");
        var report = DiagnosticReport.Build(new ReportFixture());
        Check(report.Contains($"Registered: {all.Count}; modified: 1.") && report.Contains("ghost (Click-through while moving)=true"),
            "Report must include catalog count and modified tweak IDs/names/values");
        Program.VerifyTweakPages();
        Console.WriteLine($"Tweaks: {all.Count} unique bindings/defaults/resets, configure routing, search "
            + "ranking, modified filter, news/downgrade/migration/report and responsive pages passed.");
    }

    private sealed class ReportFixture : IReportContext {
        public string Version => "3.2.0";
        public string Commit => "unknown";
        public SystemConfig Settings { get; } = new() { GhostMode = true };
        public IEnumerable<(string Name, string State)> States => [];
        public IReadOnlyList<ReportLogEntry> RecentLogs => [];
    }
}