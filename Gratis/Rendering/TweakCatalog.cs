using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gratis.Data;

namespace Gratis.Rendering;

public sealed class TweakDefinition
{
    public string Id { get; }
    public string Group { get; }
    public char Icon { get; }
    public string Name { get; }
    public string Description { get; }
    public string ConfigPath { get; }
    public bool Default { get; }
    public string? ConfigureSection { get; }
    public string? ConfigureGroup { get; }
    public string? IntegrationNote { get; }
    private readonly FieldInfo[] fields;

    internal TweakDefinition(string id, string group, char icon, string name, string description,
        string path, string? section, string? configureGroup, string? integrationNote)
    {
        Id = id; Group = group; Icon = icon; Name = name; Description = description;
        ConfigPath = path; ConfigureSection = section; ConfigureGroup = configureGroup; IntegrationNote = integrationNote;
        var type = typeof(SystemConfig);
        fields = path.Split('.').Select(part => {
            var field = type.GetField(part) ?? throw new ArgumentException($"Unknown tweak binding: {path}");
            type = field.FieldType; return field;
        }).ToArray();
        if (type != typeof(bool)) throw new ArgumentException($"Tweaks must be independent toggles: {path}");
        Default = Get(new SystemConfig());
    }

    public bool Get(SystemConfig config)
    {
        object? owner = config;
        foreach (var field in fields) {
            if (owner is null) return Default;
            owner = field.GetValue(owner);
        }
        return (bool)owner!;
    }

    public void Set(SystemConfig config, bool value)
    {
        object owner = config;
        foreach (var field in fields.AsSpan(0, fields.Length - 1)) {
            var next = field.GetValue(owner);
            if (next is null) { next = Activator.CreateInstance(field.FieldType)!; field.SetValue(owner, next); }
            owner = next;
        }
        fields[^1].SetValue(owner, value);
        if (ConfigPath == nameof(MapDrawingConfig.TravelActions)) config.TravelActionsInitialized = true;
    }
    public bool IsModified(SystemConfig config) => Get(config) != Default;
    public void Reset(SystemConfig config) => Set(config, Default);

    internal int Rank(string search)
    {
        var words = search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var rank = 0;
        foreach (var word in words) {
            if (Name.Equals(word, StringComparison.OrdinalIgnoreCase)) rank += 100;
            else if (Name.StartsWith(word, StringComparison.OrdinalIgnoreCase)) rank += 60;
            else if (Name.Contains(word, StringComparison.OrdinalIgnoreCase)) rank += 40;
            else if (Id.Contains(word, StringComparison.OrdinalIgnoreCase) || ConfigPath.Contains(word, StringComparison.OrdinalIgnoreCase)) rank += 20;
            else if ((Group + " " + Description + " " + IntegrationNote).Contains(word, StringComparison.OrdinalIgnoreCase)) rank += 10;
            else return -1;
        }
        return rank;
    }
}

public static class TweakCatalog
{
    public static IReadOnlyList<string> Groups { get; } = Array.AsReadOnly(new[] { "Navigation", "Markers", "Travel", "Window", "Display", "Time and info" });
    public static IReadOnlyList<TweakDefinition> All { get; } = Array.AsReadOnly(Create());

    public static TweakDefinition[] Find(SystemConfig config, string search = "", bool modifiedOnly = false) =>
        All.Select(t => (Tweak: t, Rank: t.Rank(search.Trim())))
            .Where(t => t.Rank >= 0 && (!modifiedOnly || t.Tweak.IsModified(config)))
            .OrderByDescending(t => t.Rank).ThenBy(t => GroupOrder(t.Tweak.Group))
            .ThenBy(t => t.Tweak.Name, StringComparer.OrdinalIgnoreCase).Select(t => t.Tweak).ToArray();

    private static int GroupOrder(string group)
    {
        for (var i = 0; i < Groups.Count; i++) if (Groups[i] == group) return i;
        return Groups.Count;
    }

    private static TweakDefinition[] Create()
    {
        var items = new List<TweakDefinition>();
        void Add(string id, string group, char icon, string name, string description, string path,
            string? section = null, string? configureGroup = null, string? integration = null) =>
            items.Add(new(id, group, icon, name, description, path, section, configureGroup, integration));
        Add("flag-arrival", "Navigation", GratisIcons.Flag, "Flag arrival", "Show a quiet notification when you reach your flag.", "FlagArrival", "Chrome & motion", "Motion");
        Add("map-memory", "Navigation", GratisIcons.BookOpen, "Remember map views", "Restore each map's zoom and detached pan.", "RememberMapView", "Map", "Viewport");
        Add("zone-switch", "Navigation", GratisIcons.Compass, "Automatic zone switch", "Switch to your new zone while respecting a deliberately opened map.", "AutoSwitchOnZoneChange", "Map", "Zone changes");
        Add("measure", "Navigation", GratisIcons.Crosshair, "Measure tool", "Ctrl-drag to measure a straight distance in yalms.", "MeasureTool");
        Add("spotlight", "Navigation", GratisIcons.LocateFixed, "Quest spotlight", "Frame a quest target briefly when opening a quest map link.", "QuestSpotlight", "Map", "Spotlight");
        Add("follow-return", "Navigation", GratisIcons.Repeat, "Follow return", "Resume following the player after detached panning.", "FollowReturnToPlayer", "Map", "Viewport");
        Add("frame-scale", "Navigation", GratisIcons.LayoutGrid, "Frame scaling", "Preserve relative zoom when resizing the map window.", "ScaleWithFrame");
        Add("clamp-map", "Navigation", GratisIcons.Anchor, "Clamp to map", "Keep the viewport within the map texture.", "ClampToMap");
        Add("small-rooms", "Navigation", GratisIcons.House, "Fit small rooms to content", "Fit small instanced rooms with comfortable padding.", "FitSmallRoomsToContent");
        Add("follow-open", "Navigation", GratisIcons.LocateFixed, "Follow on open", "Start following the player whenever the map opens.", "FollowOnOpen");
        Add("place-search", "Navigation", GratisIcons.Search, "Place search", "Find zones, aetherytes, services and pins in the palette.", "Palette", "Map", "Place search");
        Add("edge-pointers", "Markers", GratisIcons.Compass, "Edge pointers", "Point toward important targets beyond the visible map.", "EdgePointers", "Map", "Edge pointers");
        Add("attunement", "Markers", GratisIcons.Landmark, "Aetheryte attunement styling", "Distinguish unattuned crystals and shards without hiding them.", "AetheryteAttunementStyle");
        Add("pins", "Markers", GratisIcons.MapPin, "Pins and notes", "Add personal map pins with notes and share codes.", "Pins", "Markers", "Pins and notes");
        Add("marker-tooltips", "Markers", GratisIcons.Info, "Extra marker tooltips", "Show additional information when hovering map markers.", "ShowMiscTooltips");
        Add("player-trail", "Markers", GratisIcons.Footprints, "Player trail", "Leave a short fading trail behind the player.", "Motion.PlayerTrail", "Chrome & motion", "Motion");
        Add("aetheryte-chips", "Travel", GratisIcons.Landmark, "Aetheryte chips", "Offer the nearest attuned aetheryte for distant targets.", "AetheryteChips", "Map", "Travel");
        Add("travel-actions", "Travel", GratisIcons.Compass, "Travel actions", "Offer explicit teleport actions for map objectives.", "TravelActions", "Map", "Travel", "Native Teleport; Aethernet needs Lifestream.");
        Add("fate-flag", "Travel", GratisIcons.Flag, "Flag FATE clicks", "Place a map flag when selecting a FATE in the list.", "SetFlagOnFateClick");
        Add("ghost", "Window", GratisIcons.EyeOff, "Ghost mode", "Fade and pass clicks through while moving; hover restores input.", "GhostMode", "Chrome & motion", "Chrome visibility");
        Add("move-handle", "Window", GratisIcons.Anchor, "Move handle", "Move the window with its subtle top-edge handle.", "MoveHandle", "Advanced", "Window behaviour");
        Add("alt-drag", "Window", GratisIcons.Unlock, "Alt-drag move", "Hold Alt and drag to move the map window.", "AltDragMovesWindow");
        Add("move-snap", "Window", GratisIcons.LayoutGrid, "Window snapping", "Snap or restore with the handle; Shift aligns to a grid.", "MoveHandleSnap", "Advanced", "Window behaviour");
        Add("keep-open", "Window", GratisIcons.Lock, "Keep map open", "Keep the map open after closing the game's map.", "KeepOpen");
        Add("avoid-focus", "Window", GratisIcons.Eye, "Avoid focus on open", "Open the map without taking keyboard focus.", "NoFocusOnAppear");
        Add("auto-hide", "Display", GratisIcons.Eye, "Auto-hide chrome", "Fade map controls when idle and reveal them on interaction.", "AutoHideChrome", "Chrome & motion", "Chrome visibility");
        Add("hover-unfade", "Display", GratisIcons.Eye, "Hover unfade", "Restore ordinary faded-map opacity while hovered.", "RestoreOpacityOnHover");
        Add("context-menu", "Display", GratisIcons.LayoutGrid, "Context menu", "Open map actions with a right-click.", "GratisContextMenu");
        Add("drag-inertia", "Display", GratisIcons.Footprints, "Drag inertia", "Let a released map pan glide gently to a stop.", "Motion.PanInertia", "Chrome & motion", "Motion");
        Add("reduce-motion", "Display", GratisIcons.Shield, "Reduce motion", "Use immediate movement and suppress decorative animation.", "ReduceMotion");
        Add("location-card", "Display", GratisIcons.MapPin, "Location card", "Show your zone and area together; choose how much detail to include.", "ShowTitleOnMap", "Chrome & motion", "title");
        Add("coordinates-card", "Display", GratisIcons.Crosshair, "Player and cursor coordinates", "Keep player and cursor positions together in a compact card.", "ShowCoordinateBar", "Chrome & motion", "coordinates");
        Add("eorzea-time", "Time and info", GratisIcons.Compass, "Eorzea time chip", "Show Eorzea time beside map coordinates.", "ShowEorzeaTime", "Chrome & motion", "coordinates");
        Add("sub-zone", "Time and info", GratisIcons.MapPin, "Sub-zone banner", "Briefly announce the area you have just entered.", "SubZoneBanner");
        Add("mute-map-sound", "Time and info", GratisIcons.Bell, "Mute native map sound", "Suppress the game's native map-open sound.", "SuppressNativeMapOpenSound");
        Add("whats-new", "Time and info", GratisIcons.Sparkles, "Show what's new", "Show a quiet settings badge and release notes for new tweaks.", "ShowWhatsNew");
        if (items.Select(t => t.Id).Distinct(StringComparer.Ordinal).Count() != items.Count ||
            items.Select(t => t.ConfigPath).Distinct(StringComparer.Ordinal).Count() != items.Count ||
            items.Any(t => !Groups.Contains(t.Group) || string.IsNullOrWhiteSpace(t.Description)))
            throw new InvalidOperationException("Tweak catalog must have unique IDs/bindings and complete UI metadata.");
        return items.ToArray();
    }
}
