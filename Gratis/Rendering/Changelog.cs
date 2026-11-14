using System;
using System.Collections.Generic;

namespace Gratis.Rendering;

public enum ChangeKind { New, Improved, Fixed }

public sealed record ChangelogGroup(ChangeKind Kind, IReadOnlyList<string> Items);

public sealed record ChangelogRelease(string Version, string Date, string Headline, IReadOnlyList<ChangelogGroup> Groups);

// Player-facing release notes, newest first. Keep each line short and about what it feels like, not how it works.
public static class Changelog {
    public static string Label(ChangeKind kind) => kind switch {
        ChangeKind.New => "New",
        ChangeKind.Improved => "Improved",
        _ => "Fixed",
    };

    public static IReadOnlyList<ChangelogRelease> Releases { get; } = Array.AsReadOnly(new[] {
        new ChangelogRelease("0.1.2.0", "14 Nov 2026", "FATEs on the map, and a new home", [
            new(ChangeKind.New, [
                "Gratis is now in the official Dalamud plugin repository (testing channel). If you installed from the old custom repository, switch: in /xlsettings > Experimental, tick Get plugin testing builds, install Gratis from /xlplugins, then remove the old custom repository URL so you don't get two copies.",
                "A FATE button beside the Quest Log lists the FATEs on your map with level, progress, time left and distance. Click one to find it on the map.",
                "FATEs with the EXP bonus now show a gold + on the map without hovering.",
                "Your pet and party pets, and striking dummies, now appear on the map in the Gratis style."]),
            new(ChangeKind.Improved, [
                "The old FATE window now opens the map and the FATE list instead.",
                "Teleport cards now open in small map windows."]),
        ]),
        new ChangelogRelease("0.1.0.8", "1 Nov 2026", "Lighter on your frame time (and sorry for the spam)", [
            new(ChangeKind.New, [
                "/gratis perf: times Gratis's own drawing while the map is open and tells you the average, the slowest frames, what costs the most and whether a garbage collection hit. The details go to dalamud.log if you want to share them."]),
            new(ChangeKind.Improved, [
                "Sorry for the flurry of updates lately. This is the end of a performance pass, so it should be quiet for a while.",
                "In a busy hub with the map open, the typical frame went from about 0.55 ms to about 0.28 ms and the average from 0.85 ms to 0.36 ms (measured with /gratis perf in Solution Nine). A boss fight sits around 0.19 ms.",
                "Frame outlines and glass edges now cost roughly a tenth of what they did, and use about a third of the vertices.",
                "Hovering a marker used to add about 0.9 ms to a frame. It now adds almost nothing, and frames over 1 ms dropped from 5% to about 1.4%.",
                "Much less garbage is created each frame (marker text, group members and outlines were the worst), so there are fewer small stutters.",
                "Changing maps no longer stalls for a moment while a big map is put together.",
                "The player's view cone is drawn from a cached grid instead of being rebuilt every frame."]),
            new(ChangeKind.Fixed, [
                "Marker hover cards no longer rebuild their text every frame."]),
        ]),
        new ChangelogRelease("0.1.0.7", "1 Nov 2026", "Quests you can follow, a map that fits", [
            new(ChangeKind.New, [
                "Click a quest marker on the map to open that quest in the Quest Log, accepted or not yet picked up.",
                "Quests inside instances now point at the way in: the NPC or the door you click, with a \"This quest is through a door\" note when the door can't be marked.",
                "A different map size and position inside duties: pick one in Settings, and the map switches as you enter and leave.",
                "A Locate player button on the toolbar.",
                "Enemy dots: nearby hostiles on the map, with a gold glow for quest, FATE and hunting log targets (off by default)."]),
            new(ChangeKind.Improved, [
                "The toolbar only condenses when it runs out of width, not when the window gets short.",
                "The up-one-level arrow on the location card appears when you point at the card.",
                "Settings rows are evenly spaced, captions sit under their labels and chips have room to breathe.",
                "Map recolours no longer flash the original map first, and the Quest Log loads in the background.",
                "FATE markers are calmer when you zoom out, and tooltips no longer have a gap on the right."]),
            new(ChangeKind.Fixed, [
                "Pointing at the Quest Log no longer stops WASD from reaching the game.",
                "Fog of war has been removed: it made the game use more and more memory, especially after reloading the plugin.",
                "Settings are kept as a backup if the file ever fails to load, and your pins survive a reload.",
                "Feedback button reads Send feedback / bug reports."]),
        ]),
        new ChangelogRelease("0.1.0.6", "31 Oct 2026", "Everything where you'd look for it", [
            new(ChangeKind.New, [
                "Essentials: a new first tab in Settings with the handful of choices most people actually want, from your look to how the map behaves.",
                "Settings are now sorted into Appearance, Map and Controls, each with its own sections, so nothing is buried.",
                "Show advanced: fine-tuning stays out of the way until you want it. Search still finds everything.",
                "One-click presets for Motion, Controls, Map fade, Glass, Frame effects, Map labels and Spotlight.",
                "A What's new window appears once after an update. Reopen it any time with /gratis whatsnew.",
                "An up-one-level button on the location card takes you to the parent zone in one click.",
                "Try On for gear in quest rewards, right from the right-click menu."]),
            new(ChangeKind.Improved, [
                "Quick setup lets you pick a look from plain text choices, and each look brings its own map recolour.",
                "The Quest Log is tidier: tabs sit in the header, summaries lose the clutter, place names are in proper title case and dashes read correctly.",
                "Show on map now takes you somewhere you can actually reach, such as a duty's entrance, instead of a closed-off instance.",
                "Search shows each zone once, with bigger expansion cards and your nearest aetheryte, market board and inn in game art.",
                "Scrollbars only appear while you scroll, and tooltips no longer clip.",
                "A roomier feedback form, and the window corners are easier to grab."]),
            new(ChangeKind.Fixed, [
                "Hovering the Quest Log no longer stops your character from moving.",
                "Show on map no longer closes the map you opened it on.",
                "Clearing an empty search now closes it.",
                "Aethernet shards that sit in another zone now show where they really are.",
                "Nearby cards with nothing nearby say so instead of running an empty search."]),
        ]),
        new ChangelogRelease("0.1.0.5", "31 Oct 2026", "Make it yours in a minute", [
            new(ChangeKind.New, [
                "Quick setup: a friendly first-run tour that helps you pick your look, your marker icons and the little extras. Replay it any time from Appearance.",
                "Profiles: save up to five looks, name them, and share one as a single line of text with friends. Revert to defaults is one click away, with a warning before you lose anything."]),
            new(ChangeKind.Improved, [
                "Vanilla+ is the new default: the familiar map with softly rounded edges, game markers and the standard recolour.",
                "Quest spotlight now hushes every other icon for a couple of seconds, so the quest you picked can't be missed.",
                "Completed quests glow green with a crisp white check.",
                "Tooltips are calmer: no more jumping, double hovers or scrollbars."]),
            new(ChangeKind.Fixed, [
                "Aetherytes and aethernet shards open the travel card when you click them, every time.",
                "The location card never cuts your zone name short when there's room.",
                "Quick zone finder lists each place once.",
                "The settings sidebar highlight stays with its tab while you move the window."])]),
        new ChangelogRelease("0.1.0.4", "26 Oct 2026", "Your quests, right on the map", [
            new(ChangeKind.New, [
                "The in-map Quest Log: browse your journal without leaving the map, with progress, rewards and tabs.",
                "Optional Journal replacement, so opening your journal opens Gratis."]),
            new(ChangeKind.Improved, [
                "Expansion crests on region browsing, and one consistent corner style across every panel.",
                "Smarter spotlight and crisper item tooltips."]),
            new(ChangeKind.Fixed, [
                "Better icon fallbacks, so fewer markers show up blank."])]),
        new ChangelogRelease("0.1.0.3", "26 Oct 2026", "A better way to teleport", [
            new(ChangeKind.New, [
                "In-map feedback, and a gentle notice when a new version is out."]),
            new(ChangeKind.Improved, [
                "A reworked teleport card that tells you the cost and the route at a glance.",
                "Hover cards stack neatly instead of covering each other.",
                "More map icons recognised and drawn properly.",
                "Small maps now use wide windows far better."])]),
        new ChangelogRelease("0.1.0.2", "25 Oct 2026", "Friendlier everywhere", [
            new(ChangeKind.Improved, [
                "Settings rewritten in plain language.",
                "Nearby places cards, solid marker glyphs and a more polished dropdown."])]),
        new ChangelogRelease("0.1.0.1", "25 Oct 2026", "Smoother motion", [
            new(ChangeKind.Improved, [
                "One consistent motion style across every control."]),
            new(ChangeKind.Fixed, [
                "Palette search no longer scrolls under your cursor."])]),
        new ChangelogRelease("0.1.0.0", "25 Oct 2026", "Hello, Gratis", [
            new(ChangeKind.New, [
                "A glass-and-gold take on the FFXIV map: search any place, drop pins, measure distances, follow your character and teleport from the map.",
                "More than 30 small quality-of-life tweaks, each one searchable and individually switchable.",
                "Classic look is always there if you prefer the original."])]),
    });

    public static string Latest => Releases[0].Version;
}
