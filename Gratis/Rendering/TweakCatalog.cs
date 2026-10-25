using System;
using System.Collections.Generic;
using System.Linq;
using Gratis.Data;

namespace Gratis.Rendering;

public static class TweakCatalog {
    public static IReadOnlyList<string> Groups { get; } = Array.AsReadOnly(new[] {
        "Navigation",
        "Markers",
        "Travel",
        "Window",
        "Display",
        "Time and info" });
    public static IReadOnlyList<TweakDefinition> All { get; } = Array.AsReadOnly(Create());

    public static TweakDefinition[] Find(SystemConfig config, string search = "", bool modifiedOnly = false) =>
        All.Select(t => (Tweak: t, Rank: t.Rank(search.Trim()))).Where(
            t => t.Rank >= 0 && (!modifiedOnly || t.Tweak.IsModified(config))).OrderByDescending(t => t.Rank).ThenBy(
            t => GroupOrder(t.Tweak.Group)).ThenBy(t => t.Tweak.Name, StringComparer.OrdinalIgnoreCase).Select(
            t => t.Tweak).ToArray();

    private static int GroupOrder(string group) {
        for (var i = 0; i < Groups.Count; i++)
            if (Groups[i] == group)
                return i;
        return Groups.Count;
    }

    private static TweakDefinition[] Create() {
        var items = new List<TweakDefinition>();
        void Add(string id, string group, char icon, string name, string description, string path,
            string? section = null, string? configureGroup = null, string? integration = null) =>
            items.Add(new(id, group, icon, name, description, path, section, configureGroup, integration));
        Add("pvp-map-only", "Markers", GratisIcons.Flag, "PvP: show only what the game map shows",
            "Always on. Shows your party and alliance, not enemies or nearby players.", "PvpMapOnly",
            section: "Map", configureGroup: "PvP", integration: "Always on. The game's own map still works in PvP.");
        Add("flag-arrival", "Navigation", GratisIcons.Flag, "Flag arrival",
            "Show a quiet notification when you reach your flag.", "FlagArrival", section: "Chrome & motion",
            configureGroup: "Motion");
        Add("map-memory", "Navigation", GratisIcons.BookOpen, "Remember map views",
            "Reopen each map at the zoom and position you left it.", "RememberMapView", section: "Map", configureGroup: "Viewport");
        Add("zone-switch", "Navigation", GratisIcons.Compass, "Automatic zone switch",
            "Switch to your new zone's map, unless you opened a different map yourself.", "AutoSwitchOnZoneChange",
            section: "Map", configureGroup: "Zone changes");
        Add("measure", "Navigation", GratisIcons.Crosshair, "Measure tool",
            "Ctrl-drag to measure a straight distance in yalms.", "MeasureTool");
        Add("spotlight", "Navigation", GratisIcons.LocateFixed, "Quest spotlight",
            "Briefly highlight the quest target when you open a quest's map link.", "QuestSpotlight", section: "Map",
            configureGroup: "Spotlight");
        Add("follow-return", "Navigation", GratisIcons.Repeat, "Follow return",
            "Go back to following you after you drag the map.", "FollowReturnToPlayer", section: "Map",
            configureGroup: "Viewport");
        Add("frame-scale", "Navigation", GratisIcons.LayoutGrid, "Zoom with window size",
            "Keep what you see the same when you resize the map window.", "ScaleWithFrame");
        Add("clamp-map", "Navigation", GratisIcons.Anchor, "Keep view inside map", "Stops you panning past the edge of the map.",
            "ClampToMap");
        Add("small-rooms", "Navigation", GratisIcons.House, "Zoom small areas to fit",
            "Zoom in on small rooms and instances when you first visit.", "FitSmallRoomsToContent");
        Add("follow-open", "Navigation", GratisIcons.LocateFixed, "Follow on open",
            "Start following the player whenever the map opens.", "FollowOnOpen");
        Add("place-search", "Navigation", GratisIcons.Search, "Place search",
            "Search for zones, aetherytes, services and pins.", "Palette", section: "Map",
            configureGroup: "Place search");
        Add("edge-pointers", "Markers", GratisIcons.Compass, "Arrows to off-screen targets",
            "Point toward important targets outside the visible map.", "EdgePointers", section: "Map",
            configureGroup: "Edge pointers");
        Add("attunement", "Markers", GratisIcons.Landmark, "Unattuned aetheryte styling",
            "Dim aetherytes you have not attuned to, without hiding them.", "AetheryteAttunementStyle");
        Add("pins", "Markers", GratisIcons.MapPin, "Pins and notes",
            "Add personal map pins with notes and share codes.", "Pins", section: "Markers",
            configureGroup: "Pins and notes");
        Add("marker-tooltips", "Markers", GratisIcons.Info, "Extra marker tooltips",
            "Show additional information when hovering map markers.", "ShowMiscTooltips");
        Add("player-trail", "Markers", GratisIcons.Footprints, "Player trail",
            "Leave a short fading trail behind the player.", "Motion.PlayerTrail", section: "Chrome & motion",
            configureGroup: "Motion");
        Add("aetheryte-chips", "Travel", GratisIcons.Landmark, "Nearest aetheryte buttons",
            "Offer the nearest attuned aetheryte for distant targets.", "AetheryteChips", section: "Map",
            configureGroup: "Travel");
        Add("travel-actions", "Travel", GratisIcons.Compass, "Teleport actions",
            "Offer teleport buttons for quests and objectives.", "TravelActions", section: "Map",
            configureGroup: "Travel", integration: "Uses the game's Teleport. Aethernet needs Lifestream.");
        Add("fate-flag", "Travel", GratisIcons.Flag, "Flag FATE clicks",
            "Place a map flag when selecting a FATE in the list.", "SetFlagOnFateClick");
        Add("ghost", "Window", GratisIcons.EyeOff, "Click-through while moving",
            "Fade the map and let clicks reach the game while you move.", "GhostMode", section: "Chrome & motion",
            configureGroup: "Chrome visibility");
        Add("move-handle", "Window", GratisIcons.Anchor, "Move handle",
            "Drag the thin bar at the top edge to move the window.", "MoveHandle", section: "Advanced",
            configureGroup: "Window behaviour");
        Add("alt-drag", "Window", GratisIcons.Unlock, "Alt-drag move", "Hold Alt and drag to move the map window.",
            "AltDragMovesWindow");
        Add("move-snap", "Window", GratisIcons.LayoutGrid, "Window snapping",
            "Double-click the move handle to snap or restore. Hold Shift to align to screen edges.", "MoveHandleSnap", section: "Advanced",
            configureGroup: "Window behaviour");
        Add("keep-open", "Window", GratisIcons.Lock, "Keep map open", "Keep the map open after you close the game's map.",
            "KeepOpen");
        Add("avoid-focus", "Window", GratisIcons.Eye, "Avoid focus on open",
            "Open the map without taking keyboard focus.", "NoFocusOnAppear");
        Add("auto-hide", "Display", GratisIcons.Eye, "Auto-hide controls",
            "Fade the toolbar, location and coordinates when idle. Hover to bring them back.", "AutoHideChrome", section: "Chrome & motion",
            configureGroup: "Chrome visibility");
        Add("hover-unfade", "Display", GratisIcons.Eye, "Full opacity on hover",
            "Show the map at normal opacity while hovered.", "RestoreOpacityOnHover");
        Add("context-menu", "Display", GratisIcons.LayoutGrid, "Right-click menu", "Right-click the map for actions like setting a flag.",
            "GratisContextMenu");
        Add("drag-inertia", "Display", GratisIcons.Footprints, "Drag glide",
            "Let the map keep gliding a moment after you let go.", "Motion.PanInertia", section: "Chrome & motion",
            configureGroup: "Motion");
        Add("reduce-motion", "Display", GratisIcons.Shield, "Reduce motion",
            "Skip animations and move things instantly.", "ReduceMotion");
        Add("interface-motion", "Display", GratisIcons.Sparkles, "Animations",
            "Smooth the toolbar, menus and list scrolling. Choose how much and how fast.", "InterfaceAnimations",
            section: "Chrome & motion", configureGroup: "Motion");
        Add("location-card", "Display", GratisIcons.MapPin, "Location card",
            "Show your zone and area together. Choose how much detail to show.", "ShowTitleOnMap",
            section: "Chrome & motion", configureGroup: "title");
        Add("coordinates-card", "Display", GratisIcons.Crosshair, "Player and cursor coordinates",
            "Show your position and the cursor position together in a small card.", "ShowCoordinateBar",
            section: "Chrome & motion", configureGroup: "coordinates");
        Add("eorzea-time", "Time and info", GratisIcons.Compass, "Eorzea time",
            "Show Eorzea time beside the map coordinates.", "ShowEorzeaTime", section: "Chrome & motion",
            configureGroup: "coordinates");
        Add("sub-zone", "Time and info", GratisIcons.MapPin, "Sub-zone banner",
            "Briefly announce the area you have just entered.", "SubZoneBanner");
        Add("mute-map-sound", "Time and info", GratisIcons.Bell, "Mute the game's map sound",
            "Silence the sound the game plays when the map opens.", "SuppressNativeMapOpenSound");
        Add("whats-new", "Time and info", GratisIcons.Sparkles, "Show what's new",
            "Show a small badge and release notes when there are new tweaks.", "ShowWhatsNew");
        if (items.Select(t => t.Id).Distinct(StringComparer.Ordinal).Count() != items.Count
            || items.Select(t => t.ConfigPath).Distinct(StringComparer.Ordinal).Count() != items.Count
            || items.Any(t => !Groups.Contains(t.Group) || string.IsNullOrWhiteSpace(t.Description)))
            throw new InvalidOperationException(
                "Tweak catalog must have unique IDs/bindings and complete UI metadata.");
        return items.ToArray();
    }
}
