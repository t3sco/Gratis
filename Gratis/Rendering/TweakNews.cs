using System;
using System.Collections.Generic;
using System.Linq;

namespace Gratis.Rendering;

public sealed record TweakRelease(string Version, string Date, IReadOnlyList<string> TweakIds, string Notes);

public static class TweakNews {
    public static IReadOnlyList<TweakRelease> Releases { get; } = Array.AsReadOnly(new[] {
        new TweakRelease("3.2.3", "2026-10-25", Array.AsReadOnly(new[] { "pvp-map-only" }),
            "Mandatory PvP marker-source policy; native map fallback and /gratis pvp diagnostics."),
        new TweakRelease("3.2.2", "2026-10-25", Array.AsReadOnly(new[] { "interface-motion" }),
            "One consistent motion language for controls, popovers, scrolling and hover opacity."),
        new TweakRelease("3.2.1", "2026-10-25", Array.AsReadOnly(new[] { "location-card", "coordinates-card" }),
            "Centre your location and coordinates together, with selectable area detail and player/cursor rows."),
        new TweakRelease("3.2.0", "2026-10-25", Array.AsReadOnly(new[] {
            "flag-arrival",
                "map-memory",
                "zone-switch",
                "measure",
                "spotlight",
                "follow-return",
                "frame-scale",
                "clamp-map",
            "small-rooms",
                "follow-open",
                "place-search",
                "edge-pointers",
                "attunement",
                "pins",
                "marker-tooltips",
                "player-trail",
            "aetheryte-chips",
                "travel-actions",
                "fate-flag",
                "ghost",
                "move-handle",
                "alt-drag",
                "move-snap",
                "keep-open",
            "avoid-focus", "auto-hide", "hover-unfade", "context-menu", "drag-inertia", "reduce-motion",
            "eorzea-time", "sub-zone", "mute-map-sound", "whats-new"
        }),
            "Your shipped quality-of-life options now live together in searchable Tweaks, with individual resets.")
    });
    public static string LatestVersion => Releases[0].Version;
    public static TweakRelease[] Unseen(MapDrawingConfig config) =>
        Releases.Where(release => !Version.TryParse(config.LastSeenTweakVersion, out var seen)
            || Version.Parse(release.Version) > seen).ToArray();
    public static bool HasUnseen(MapDrawingConfig config) {
        if (!config.ShowWhatsNew)
            return false;
        var valid = Version.TryParse(config.LastSeenTweakVersion, out var seen);
        foreach (var release in Releases)
            if (release.TweakIds.Count > 0 && (!valid || Version.Parse(release.Version) > seen))
                return true;
        return false;
    }

    public static void Dismiss(MapDrawingConfig config) => config.LastSeenTweakVersion = LatestVersion;
}