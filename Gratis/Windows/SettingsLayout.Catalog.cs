using System;
using System.Collections.Generic;
using System.Linq;
using Gratis.Data;
using Gratis.Rendering;

namespace Gratis.Windows;

public sealed partial class SettingsLayout {
    private static SettingsEntry[] CreateEntries() {
        var entries = new List<SettingsEntry>();
        var section = "";
        var group = "";
        Func<SystemConfig, bool> visible = _ => true;
        void Group(string area, string name, Func<SystemConfig, bool>? show = null) {
            section = area;
            group = name;
            visible = show ?? (_ => true);
        }
        void Add(string path, string label, float min = 0, float max = 1, string format = "%.2f",
            string? choices = null, Func<SystemConfig, bool>? when = null, string? tip = null) =>
            entries.Add(new SettingsEntry(section, group, path, label, min, max, format, choices, visible, when, tip));
        bool Gratis(SystemConfig c) => c.IsGratis;
        bool Classic(SystemConfig c) => !c.IsGratis;
        bool ConicRing(SystemConfig c) => c.IsGratis && !c.NeutralFrame && c.FramePreset == FramePreset.Hairline;
        bool FlatMarkers(SystemConfig c) => c.IsGratis && c.MarkerStyle == 1
            && c.Markers.IconStyle != MarkerIconStyle.Game;

        Group("Map", "PvP");
        Add("PvpMapOnly", "PvP: show only what the game map shows", when: _ => false,
            tip: "Always on. In PvP the map shows only what the game map shows: your party and alliance, not enemies or nearby players. The game's own map still works.");

        Group("Appearance", "Style");
        Add("VisualStyle", "Visual style", choices: "Gratis|Classic");
        Add("MapCircle", "Circular map");
        Group("Appearance", "Gratis style", Gratis);
        Add("AccentColor", "Accent colour");
        Add("FramePreset", "Frame preset", choices: "Hairline|Double ring|Inset glow|Notched corners|Soft shadow|None",
            tip: "The frame stays visible when the controls fade.");
        Add("NeutralFrame", "Use the Gratis frame",
            tip: "Turn off, with Hairline selected, to use the gradient border or your older "
                + "edge settings. Older edge settings are editable in Classic.");
        Add("FlatCornerRadius", "Corner radius", 0, 80, "%.0f px", when: c => c.NeutralFrame && !c.MapCircle);
        Group("Appearance", "Map recolour");
        Add("MapColour.Preset", "Recolour preset");
        Add("MapColour.Brightness", "Brightness", -1, 1, when: c => c.MapColour.Preset == MapColourPreset.Custom);
        Add("MapColour.Contrast", "Contrast", 0, 3, "%.2fx", when: c => c.MapColour.Preset == MapColourPreset.Custom);
        Add("MapColour.Saturation", "Saturation", 0, 2, "%.2fx",
            when: c => c.MapColour.Preset == MapColourPreset.Custom);
        Add("MapColour.HueShift", "Hue shift", -180, 180, "%.0f deg",
            when: c => c.MapColour.Preset == MapColourPreset.Custom);
        Add("MapColour.Tint", "Tint", when: c => c.MapColour.Preset == MapColourPreset.Custom);
        Add("MapColour.TintStrength", "Tint strength", when: c => c.MapColour.Preset == MapColourPreset.Custom);
        Add("MapColour.GradientEnabled", "Gradient map", when: c => c.MapColour.Preset == MapColourPreset.Custom);
        Add("MapColour.Shadow", "Shadow colour",
            when: c => c.MapColour.Preset == MapColourPreset.Custom && c.MapColour.GradientEnabled);
        Add("MapColour.Mid", "Midtone colour",
            when: c => c.MapColour.Preset == MapColourPreset.Custom && c.MapColour.GradientEnabled);
        Add("MapColour.Highlight", "Highlight colour",
            when: c => c.MapColour.Preset == MapColourPreset.Custom && c.MapColour.GradientEnabled);
        Group("Appearance", "Edge feather", Gratis);
        Add("FeatherEnabled", "Soften map edge");
        Add("FeatherWidth", "Feather width", 0, 48, "%.0f px", when: c => c.FeatherEnabled,
            tip: "Fades the map edge. Markers stay inside the solid area.");
        Group("Appearance", "Conic ring", ConicRing);
        Add("RingEnabled", "Gradient border");
        Add("RingThickness", "Border thickness", .5f, 8, "%.1f px", when: c => c.RingEnabled);
        Add("RingColorA", "Border colour 1", when: c => c.RingEnabled);
        Add("RingColorB", "Border colour 2", when: c => c.RingEnabled);
        Add("RingAccentColor", "Accent colour stop", when: c => c.RingEnabled);
        Add("RingAngle", "Angle offset", -180, 180, "%.0f deg", when: c => c.RingEnabled);
        Add("RingSweepWidth", "Highlight width", 5, 180, "%.0f deg", when: c => c.RingEnabled);
        Add("RingIntensity", "Highlight intensity", when: c => c.RingEnabled);
        Add("RingRotate", "Rotate border", when: c => c.RingEnabled);
        Add("RingRotationSpeed", "Rotation speed", -20, 20, "%.1f deg/s", when: c => c.RingEnabled && c.RingRotate);
        Group("Appearance", "Glass surfaces", Gratis);
        Add("GlassEnabled", "Frosted panels");
        Add("GlassBlur", "Blur behind panels", when: c => c.GlassEnabled);
        Add("GlassBlurRadius", "Blur radius", 0, 48, "%.0f px", when: c => c.GlassEnabled && c.GlassBlur,
            tip: "Higher is blurrier. Circular maps use a tint instead of blur.");
        Add("GlassTintOpacity", "Brightness tint", 0, .08f, "%.3f", when: c => c.GlassEnabled);
        Add("GlassHighlight", "Panel edge shine", 0, .6f, when: c => c.GlassEnabled);
        Add("GlassGrain", "Grain", when: c => c.GlassEnabled);
        Add("GlassGrainIntensity", "Grain strength", 0, .04f, "%.3f", when: c => c.GlassEnabled && c.GlassGrain);
        Group("Appearance", "Classic frame", Classic);
        Add("MapRounding", "Corner radius", 0, 80, "%.0f px", when: c => !c.MapCircle);
        Add("MapBorder", "Border");
        Add("MapBorderColor", "Border colour", when: c => c.MapBorder);
        Add("MapBorderThickness", "Border thickness", .5f, 8, "%.1f px", when: c => c.MapBorder);
        Add("MapGlow", "Inner glow");
        Add("MapGlowColor", "Glow colour", when: c => c.MapGlow);
        Add("MapGlowSize", "Glow size", 2, 60, "%.0f px", when: c => c.MapGlow);
        Add("MapGlowPulse", "Glow pulse", when: c => c.MapGlow);
        Add("MapGlowSpeed", "Pulse speed", .2f, 6, "%.1f rad/s", when: c => c.MapGlow && c.MapGlowPulse > 0,
            tip: "Angular pulse speed; the renderer applies a factor of two.");
        Add("MapVignette", "Edge vignette");

        Group("Map", "Travel", Gratis);
        Add("TravelActions", "Travel actions",
            tip: "Offer teleports to the nearest aetheryte for quests and objectives, using the game's "
                + "Teleport and, if installed, Lifestream for aethernet. Turns on when Lifestream is first found.");
        Add("TravelConfirmGil", "Confirm travel cost", 0, 10000, "%.0f gil");
        Add("AetheryteChips", "Nearest aetheryte buttons");
        Group("Map", "Viewport");
        Add("FollowPlayer", "Follow player");
        Add("RememberMapView", "Remember map views", when: Gratis,
            tip: "Reopen each map at the zoom and position you left it. Remembers up to 100 maps.");
        Add("RememberMapLens", "Remember map filters", when: Gratis);
        Add("ScaleWithFrame", "Zoom with window size", tip: "Resizing the window keeps what you see the same.");
        Add("ClampToMap", "Keep view inside map", tip: "Stops you panning past the edge of the map.");
        Add("FitSmallRoomsToContent", "Zoom small areas to fit", when: Gratis,
            tip: "Zoom in on small rooms the first time you visit. You can still zoom out to see "
                + "the whole map.");
        Add("FollowReturnToPlayer", "Return to player after dragging", when: c => c.IsGratis,
            tip: "Keep Follow player on while exploring. Classic stops following when you drag.");
        Add("FollowReturnTrigger", "Return when", choices: "When player moves|When unfocused|After idle",
            when: c => c.IsGratis && c.FollowReturnToPlayer);
        Add("FollowReturnIdleSeconds", "Return after idle for", .5f, 20, "%.1f s",
            when: c => c.IsGratis && c.FollowReturnToPlayer && c.FollowReturnTrigger == FollowReturnTrigger.AfterIdle);
        Add("FollowReturnEaseMs", "Return glide time", 0, 1500, "%.0f ms",
            when: c => c.IsGratis && c.FollowReturnToPlayer && !c.ReduceMotion);
        Add("LockCenterOnMap", "Lock map centre");
        Add("ZoomLocked", "Lock zoom");
        Group("Map", "Zoom");
        Add("UseLinearZoom", "Linear zoom");
        Add("ZoomSpeed", "Wheel zoom step", .001f, .5f, "%.3f / tick");
        Add("AutoZoom", "Auto zoom");
        Add("AutoZoomScaleFactor", "Auto zoom factor", .2f, 1, "%.2fx", when: c => c.AutoZoom);
        Group("Map", "Place search", Gratis);
        Add("Palette", "Place search");
        Add("PaletteTeleportButton", "Allow teleports from search",
            tip: "Enter shows a place. Alt+Enter teleports, only to aetherytes you have attuned to.");
        Add("PaletteShowUnattuned", "Show unattuned aetherytes");
        Add("PaletteImagery", "Location artwork",
            tip: "Show artwork from the game on search results. A simple placeholder appears "
                + "while it loads.");
        Add("AetheryteAttunementStyle", "Style unattuned map markers",
            tip: "Dim aetherytes you have not attuned to and show their status on hover.");
        Add("PaletteMaxResults", "Search results shown", 1, 12, "%.0f");
        Group("Map", "Quest Log", Gratis);
        Add("QuestLogShowLocked", "Show quests you have not unlocked",
            tip: "Off by default. Available shows client-known map offers without checking every requirement. Turn on to browse other quests, including locked quests.");
        Add("QuestLogOpenWithMap", "Open quest log with the map",
            tip: "Initial visibility when the map opens. You can close it unless Keep quest log pinned is enabled.");
        Add("QuestLogPinned", "Keep quest log pinned",
            tip: "Keep the log visible across Escape, map actions and zone changes. Overrides the initial visibility setting. Unpin to close it.");
        Add("QuestLogWidth", "Quest log panel width", 240, 440, "%.0f px",
            tip: "320 by default. Narrow maps use a smaller overlay without resizing the map.");
        Group("Map", "Journal replacement", Gratis);
        Add("QuestLogReplaceJournal", "Use the Gratis Quest Log instead of the game's Journal",
            tip: "Your Journal key, Duty menu, /journal and quest links open the Gratis Quest Log. /gratis journal also works with this setting off.");
        Group("Map", "Window");
        Add("WindowPresets", "Window presets",
            tip: "Save up to five named window sizes and positions. Pick one to apply it, "
                + "rename it or delete it. Open at reapplies the chosen one each time the map opens.");
        Group("Map", "On open");
        Add("FollowOnOpen", "Follow player on open");
        Add("CenterOnOpen", "Centre on open", choices: "Disabled|Player|Map");
        Group("Map", "Zone changes");
        Add("AutoSwitchOnZoneChange", "Switch map when changing zone");
        Add("OnlyIfViewingCurrentZone", "Only while viewing the current zone", when: c => c.AutoSwitchOnZoneChange,
            tip: "Stay on a map you opened yourself.");
        Add("SwitchInDuties", "Switch maps in duties", when: c => c.AutoSwitchOnZoneChange);
        Group("Map", "Map links");
        Add("CenterOnFlag", "Centre on flags");
        Add("CenterOnGathering", "Centre on gathering areas");
        Add("CenterOnQuest", "Centre on quests");
        Group("Map", "Spotlight", Gratis);
        Add("QuestSpotlight", "Highlight quest targets");
        Add("SpotlightDim", "Dim the rest of the map", 0, .85f, "%.2f");
        Add("SpotlightPulse", "Pulse the target icon");
        Add("SpotlightSeconds", "Highlight duration", 1, 30, "%.0f s");
        Add("SpotlightIncludePlayer", "Fit you and the target");
        Add("SpotlightDistanceChip", "Show distance");
        Add("SpotlightGuideLine", "Dotted line to target", tip: "A straight line, not a route.");
        Add("SpotlightHideOthers", "Hide other icons at first",
            tip: "Other map icons almost vanish for the first two seconds so the target stands out.");
        Group("Map", "Measure", Gratis);
        Add("MeasureTool", "Ctrl-drag ruler",
            tip: "Hold Ctrl and drag to measure distance in yalms. Esc clears it, right-click "
                + "copies it. Alt-drag moves the window.");
        Group("Map", "Edge pointers", Gratis);
        Add("EdgePointers", "Arrows to off-screen targets");
        Add("EdgePointerMax", "Most arrows shown", 1, 12, "%.0f");
        Add("EdgePointerDistance", "Show distances on arrows");
        Add("EdgePointerParty", "Include party members",
            tip: "Quest and objective targets always get arrows.");
        Group("Map", "Map labels and fog");
        Add("ShowTextLabels", "Text labels");
        Add("ScaleTextWithZoom", "Resize labels when zooming", when: c => c.ShowTextLabels);
        Add("LargeAreaTextScale", "Large label size", 1, 4, "%.2fx", when: c => c.ShowTextLabels);
        Add("SmallAreaTextScale", "Small label size", .5f, 3, "%.2fx", when: c => c.ShowTextLabels);
        Add("LargeAreaTextOpacity", "Large label opacity", .05f, 1, "%.2f", when: c => c.ShowTextLabels && c.IsGratis,
            tip: "Lower this if big area names cover markers.");
        Add("SmallAreaTextOpacity", "Small label opacity", .05f, 1, "%.2f", when: c => c.ShowTextLabels && c.IsGratis);
        Add("HideOverlappingLabels", "Hide overlapping labels", when: c => c.ShowTextLabels && c.IsGratis,
            tip: "When two labels would overlap, only the larger one is shown.");
        Add("ShowFogOfWar", "Fog of war");

        Group("Filters", "Categories in the list", Gratis);
        foreach (var category in Enum.GetValues<MarkerGroup>())
            Add("Filters." + category, MarkerVisibility.Names[(int)category],
                tip: "Show this category's button in the filter drawer. Markers on the map are unchanged.");
        Group("Filters", "Lenses", Gratis);
        Add("ActiveLens", "Active filter preset");
        Add("CustomLenses", "Custom filter presets");
        Add("LensTintsMap", "Tint map by filter preset");
        Add("LensAuto", "Pick filter preset automatically",
            tip: "Uses Minimal in duties, Gather near nodes on gathering jobs, and Explore otherwise.");
        Add("LensCycleKey", "Key to switch filter preset");
        Add("PeekKey", "Hold to peek",
            tip: "Hold to temporarily show everything. Right Alt by default; works only while the map "
                + "is focused or hovered.");
        Group("Markers", "Pins and notes", Gratis);
        Add("Pins", "User pins");
        Add("PinLabels", "Pin labels", choices: "On hover|Always|Never");
        Add("PinMaxPerMap", "Maximum pins per map", 1, 200, "%.0f");
        Group("Markers", "Visibility and interaction");
        Add("ShowPlayers", "Other players");
        Add("ShowMiscTooltips", "Extra marker tooltips");
        Add("Markers.Opacity", "Marker opacity", .2f, 1, "%.2f", when: c => c.IsGratis,
            tip: "How solid map markers look. A marker you point at is always fully solid.");
        Group("Markers", "Game icon scale", c => !FlatMarkers(c));
        Add("IconScale", "Icon scale", .1f, 3, "%.2fx");
        Group("Markers", "Legacy zoom scale", c => !c.IsGratis || !c.Markers.ScaleWithZoom);
        Add("ScaleWithZoom", "Resize icons when zooming");
        Group("Markers", "Marker colours and size", FlatMarkers);
        Add("FlatMarkerSize", "Marker size", 6, 24, "%.0f px",
            tip: "Base size before zooming.");
        Add("PoiMarkerColor", "Points of interest");
        Add("TransportMarkerColor", "Transport");
        Add("ObjectiveMarkerColor", "Objectives");
        Add("LinkMarkerColor", "Map links and exits");
        Add("ServiceMarkerColor", "Shops and services");
        Add("PartyMarkerColor", "Party and players");
        Group("Markers", "Player");
        Add("ShowPlayerIcon", "Player icon");
        Add("PlayerIconScale", "Player icon size", .1f, 4, "%.2fx", when: c => c.ShowPlayerIcon);
        Add("ScalePlayerCone", "Keep view cone the same size", tip: "Stops the cone growing and shrinking as you zoom.");
        Group("Markers", "Gratis player", Gratis);
        Add("FlatPlayer", "Arrow and soft view cone");
        Add("PlayerArrowSmoothing", "Arrow smoothing", choices: "Off|Low|Medium|High",
            tip: "Smooths how the arrow and cone turn. Off, or Reduce motion, turns them instantly.");
        Add("ArrowPoints", "Arrow points", choices: "Camera|Character",
            tip: "Whether your arrow follows the camera or your character. Uses the character if the camera is unavailable.");
        Add("PlayerInnerHighlight", "Arrow highlight", when: c => c.FlatPlayer && c.ShowPlayerIcon);
        Add("SoftConeLength", "Cone length", 0, 200, "%.0f px", when: c => c.FlatPlayer);
        Add("SoftConeOpacity", "Cone opacity", 0, .5f, when: c => c.FlatPlayer);
        Group("Markers", "Classic cone", Classic);
        Add("ConeSize", "Cone length", 0, 300, "%.0f px");
        Add("PlayerConeColor", "Cone colour");
        Add("PlayerConeOutlineColor", "Cone outline");
        Group("Markers", "Radar and areas");
        Add("ShowRadar", "Radar radius");
        Add("ShowRadarInDuties", "Radar in duties", when: c => c.ShowRadar);
        Add("RadarColor", "Radar fill", when: c => c.ShowRadar);
        Add("RadarOutlineColor", "Radar outline", when: c => c.ShowRadar);
        Add("AreaColor", "Area fill", tip: "Gratis category colours override RGB; alpha still controls radius fills.");
        Add("AreaOutlineColor", "Area outline",
            tip: "Gratis category colours override RGB; alpha still controls radius outlines.");

        Group("Chrome & motion", "Chrome visibility", Gratis);
        Add("AutoHideChrome", "Auto-hide controls",
            tip: "Fades the toolbar, location and coordinates when the mouse is idle. Hover the map to bring them back.");
        Add("PinChrome", "Keep controls visible");
        Add("ChromeTitleMinAlpha", "Location card opacity when idle", when: c => c.AutoHideChrome && !c.PinChrome,
            tip: "How visible the location card stays after the controls fade. 0 hides it completely.");
        Add("ChromeControlsMinAlpha", "Toolbar and coordinates opacity when idle",
            when: c => c.AutoHideChrome && !c.PinChrome,
            tip: "How visible the toolbar and coordinates stay after they fade. 0 hides them completely.");
        Add("ChromeIdleDelay", "Wait before fading", 0, 10, "%.1f s", when: c => c.AutoHideChrome && !c.PinChrome);
        Add("ChromeFadeDurationMs", "Fade time", 0, 2000, "%.0f ms", when: c => c.AutoHideChrome && !c.PinChrome);
        Add("GhostMode", "Click-through while moving",
            tip: "Fades the map and lets clicks reach the game while you move. Hovering the map "
                + "brings it back. Replaces the older map fade.");
        Add("GhostOpacity", "Opacity while moving", .05f, 1, "%.2f", when: c => c.GhostMode);
        Add("GhostRestoreDelay", "Click-through delay after stopping", 0, 5, "%.1f s", when: c => c.GhostMode,
            tip: "How long clicks keep passing through after you stop. Hovering restores them at once.");
        Add("RestoreOpacityOnHover", "Full opacity on hover",
            tip: "Show the map at normal opacity while hovered. Click-through already does this.");
        Group("Chrome & motion", "Chrome layout", Gratis);
        Add("ChromeScale", "Control size", .8f, 1.6f, "%.2fx");
        const string anchors = "Top left|Top centre|Top right|Bottom left|Bottom centre|Bottom right";
        Add("ToolbarAnchor", "Toolbar position", choices: anchors);
        Add("LocationAnchor", "Location card position", choices: anchors);
        Add("CoordinateAnchor", "Coordinates position", choices: anchors);
        Add("TravelCardAnchor", "Travel status position", choices: anchors,
            tip: "Items in the same corner stack inward. Location and coordinates share a row "
                + "when there is room. Reset section restores the default layout.");
        Group("Chrome & motion", "Toolbar visibility", c => !c.IsGratis || (!c.AutoHideChrome && !c.PinChrome));
        Add("AlwaysShowToolbar", "Always show toolbar");
        Add("ShowToolbarOnHover", "Show toolbar on hover", when: c => !c.AlwaysShowToolbar);
        Group("Chrome & motion", "Gratis toolbar", Gratis);
        Add("SegmentedToolbar", "Split toolbar into groups");
        Add("IconSize", "Toolbar icon size", 12, 24, "%.0f px", when: c => c.SegmentedToolbar);
        Group("Chrome & motion", "Classic toolbar", Classic);
        Add("ToolbarFade", "Toolbar opacity");
        Group("Chrome & motion", "Title");
        Add("ShowTitleOnMap", "Title on map");
        Add("ShowRegionLabel", "Region in title");
        Add("ShowMapLabel", "Map in title");
        Add("ShowAreaLabel", "Area in title", when: c => !c.IsGratis || !c.CompactTitle);
        Add("ShowSubAreaLabel", "Sub-area in title", when: c => !c.IsGratis || !c.CompactTitle);
        Group("Chrome & motion", "Gratis title", Gratis);
        Add("SubZoneBanner", "Sub-zone arrival banner",
            tip: "Show area changes for 1.8 seconds, even while the controls are hidden.");
        Add("CompactTitle", "Location card", when: c => c.ShowTitleOnMap);
        Add("LocationCardShows", "Location card shows", choices: "Area only|Zone|Zone + area|Region, zone and area",
            when: c => c.ShowTitleOnMap && c.CompactTitle);
        Group("Chrome & motion", "Classic title", Classic);
        Add("TitleCorner", "Title corner", choices: "Top left|Top right|Bottom left|Bottom right",
            when: c => c.ShowTitleOnMap);
        Add("TitleOffset", "Title offset", 0, 100, "%.0f px", when: c => c.ShowTitleOnMap);
        Add("TitleBelowToolbar", "Keep clear of toolbar", when: c => c.ShowTitleOnMap);
        Add("TitleScale", "Title size", .6f, 2.5f, "%.2fx", when: c => c.ShowTitleOnMap);
        Add("TitleSeparator", "Separator", when: c => c.ShowTitleOnMap);
        Add("TitleFont", "Title font", choices: "Dalamud default|Noto Sans|Axis|Meidinger|Trump Gothic|Jupiter",
            when: c => c.ShowTitleOnMap);
        Add("TitleBold", "Title boldness", 0, 1.5f, "%.1f px", when: c => c.ShowTitleOnMap);
        Add("TitleStacked", "Stacked title", when: c => c.ShowTitleOnMap);
        Add("TitleSecondaryAbove", "Secondary line above", when: c => c.ShowTitleOnMap && c.TitleStacked);
        Add("TitleSecondaryScale", "Secondary line size", .5f, 1.2f, "%.2fx",
            when: c => c.ShowTitleOnMap && c.TitleStacked);
        Add("TitleLineSpacing", "Line spacing", 0, 12, "%.0f px", when: c => c.ShowTitleOnMap && c.TitleStacked);
        Add("TitleSecondaryColor", "Secondary line colour", when: c => c.ShowTitleOnMap && c.TitleStacked);
        Add("TitleColor", "Title colour", when: c => c.ShowTitleOnMap);
        Add("TitleOutline", "Text outline", when: c => c.ShowTitleOnMap);
        Add("TitleOutlineColor", "Text outline colour", when: c => c.ShowTitleOnMap && c.TitleOutline);
        Add("TitleBackground", "Title background", when: c => c.ShowTitleOnMap);
        Add("TitleBackgroundColor", "Title background colour", when: c => c.ShowTitleOnMap && c.TitleBackground);
        Add("TitlePadding", "Title padding", 0, 30, "%.0f px", when: c => c.ShowTitleOnMap && c.TitleBackground);
        Add("TitleRounding", "Title corner radius", 0, 20, "%.0f px", when: c => c.ShowTitleOnMap && c.TitleBackground);
        Add("TitleBorder", "Title border", when: c => c.ShowTitleOnMap && c.TitleBackground);
        Add("TitleBorderColor", "Title border colour",
            when: c => c.ShowTitleOnMap && c.TitleBackground && c.TitleBorder);
        Group("Chrome & motion", "Coordinates");
        Add("ShowCoordinateBar", "Coordinates");
        Group("Chrome & motion", "Gratis coordinates", Gratis);
        Add("StyledCoordinates", "Coordinate chip", when: c => c.ShowCoordinateBar);
        Add("CoordinatesShow", "Coordinates show", choices: "Player|Cursor|Both", when: c => c.ShowCoordinateBar);
        Add("ShowEorzeaTime", "Eorzean time in coordinates", when: c => c.ShowCoordinateBar,
            tip: "Use the coordinate chip to show ET beside map coordinates.");
        Add("GratisContextMenu", "Gratis context menu");
        Add("ShowUpdateNotice", "Update notice",
            tip: "Shows a small notice in the top-right corner of the map when a new version of Gratis is available.");
        Add("ShowFeedbackButton", "Feedback button",
            tip: "A small button in the bottom-left corner of the map that opens the bug report page.");
        Group("Chrome & motion", "Classic coordinates", Classic);
        Add("CoordinateTextColor", "Coordinate text colour", when: c => c.ShowCoordinateBar);
        Add("CoordinateBarFade", "Coordinate bar opacity", when: c => c.ShowCoordinateBar);
        Group("Chrome & motion", "Hover cards", Gratis);
        Add("HoverCardStyle", "Hover card style", choices: "Gratis|Classic");
        Add("HoverCardSwatch", "Marker swatch", when: c => c.HoverCardStyle == 0);
        Group("Chrome & motion", "Motion", Gratis);
        Add("InterfaceAnimations", "Animations", choices: "Full|Reduced|Off");
        Add("AnimationSpeed", "Animation speed", .5f, 1.5f, "%.2fx",
            when: c => c.InterfaceAnimations != InterfaceAnimationMode.Off);
        Add("ReduceMotion", "Reduce motion");
        Add("FlagArrival", "Flag arrival notification");
        Add("FlagArrivalRadius", "Flag arrival radius", 1, 40, "%.1f yalms");
        Add("Motion.Pings", "Flag, quest and FATE pings", when: c => !c.ReduceMotion);
        Add("Motion.PingSeconds", "Ping duration", .3f, 1.5f, "%.2f s", when: c => !c.ReduceMotion && c.Motion.Pings);
        Add("Motion.PlayerTrail", "Player trail", when: c => !c.ReduceMotion);
        Add("Motion.TrailSeconds", "Trail duration", 0, 10, "%.1f s",
            when: c => !c.ReduceMotion && c.Motion.PlayerTrail);
        Add("Motion.TrailOpacity", "Trail opacity", .05f, .5f, when: c => !c.ReduceMotion && c.Motion.PlayerTrail);
        Add("Motion.EaseFollow", "Ease player follow", when: c => !c.ReduceMotion);
        Add("Motion.EaseZoom", "Ease wheel zoom", when: c => !c.ReduceMotion);
        Add("Motion.PanInertia", "Drag pan inertia", when: c => !c.ReduceMotion);
        Add("Motion.PanInertiaStrength", "Pan inertia strength", .1f, 1, "%.2f s",
            when: c => !c.ReduceMotion && c.Motion.PanInertia,
            tip: "How long the glide lasts. Clicking or zooming stops it.");
        Add("Motion.SettleSeconds", "Pan and zoom smoothing", .05f, .3f, "%.2f s",
            when: c => !c.ReduceMotion && (c.Motion.EaseFollow || c.Motion.EaseZoom));
        Add("Motion.ZoneTransition", "Zone fade and slide", when: c => !c.ReduceMotion);
        Add("Motion.ZoneSeconds", "Zone transition duration", .08f, .4f, "%.2f s",
            when: c => !c.ReduceMotion && c.Motion.ZoneTransition);
        Add("Motion.ZoneSlidePixels", "Zone slide distance", 0, 16, "%.0f px",
            when: c => !c.ReduceMotion && c.Motion.ZoneTransition);
        Group("Chrome & motion", "Map opacity");
        Add("FadeMode", "Fade map");
        Add("FadePercent", "Map opacity when faded", .05f, 1, when: c => (int)c.FadeMode != 0);

        Group("Modules / tools", "FATE list");
        Add("SetFlagOnFateClick", "Place flag on FATE click");

        Group("Advanced", "Window behaviour");
        Add("KeepOpen", "Keep map open");
        Add("LockWindow", "Lock window position");
        Add("MoveHandle", "Top-edge move handle", when: c => c.IsGratis);
        Add("ClassicMoveHandle", "Top-edge move handle", when: c => !c.IsGratis);
        Add("AltDragMovesWindow", "Alt-drag moves window");
        Add("MoveHandleSnap", "Snap window while moving",
            tip: "Double-click the handle to snap or restore. Hold Shift to align to screen edges.");
        Add("EnableShiftDragMove", "Shift-drag moves window");
        Add("NoFocusOnAppear", "Avoid focus on open");
        Add("HideWithGameGui", "Hide with game UI");
        Add("HideBetweenAreas", "Hide between areas");
        Add("HideInCombat", "Hide in combat");
        Add("SuppressNativeMapOpenSound", "Mute the game's map sound");
        Group("Advanced", "Native window", c => !c.IsGratis || !c.NeutralFrame);
        Add("HideWindowFrame", "Hide the game's window frame");
        Add("HideWindowBackground", "Hide the game's window background");
        Group("Advanced", "Map backdrop");
        Add("BlurBackground", "Blur behind map",
            tip: "Blurs everything behind the map. Frosted panels only blur the small panels.");
        Add("BlurStrength", "Blur strength", .5f, 10, "%.1fx", when: c => c.BlurBackground);
        Group("Advanced", "Window geometry");
        Add("WindowPosition", "Window position", -8192, 8192, "%.0f px");
        Add("WindowSize", "Window size", 200, 4096, "%.0f px");
        Group("Advanced", "Diagnostics");
        Add("DebugMode", "Debug marker details", tip: "Shows marker IDs on hover. Not saved.");
        Add("TextPixelSnap", "Snap text glyphs to pixels", when: c => c.IsGratis,
            tip: "Off spaces letters by their true widths. Applies after reloading the plugin.");
        Add("TextOversampleH", "Text horizontal oversample", 1, 2, "%.0f", when: c => c.IsGratis,
            tip: "2 smooths small text but uses twice the font atlas area. Applies after reloading the plugin.");
        return entries.OrderBy(GroupRank).ToArray();
    }

    private static int GroupRank(SettingsEntry entry) {
        var rank = Array.IndexOf(GroupPriority, $"{entry.Section}/{entry.Group}");
        return rank < 0 ? GroupPriority.Length : rank;
    }

    private static readonly string[] GroupPriority = [
        "Appearance/Style", "Appearance/Gratis style", "Appearance/Classic frame", "Appearance/Map recolour",
        "Appearance/Glass surfaces", "Appearance/Edge feather", "Appearance/Conic ring",
        "Map/Viewport", "Map/Zoom", "Map/Map labels and fog", "Map/Zone changes", "Map/On open", "Map/Place search",
        "Map/Travel", "Map/Map links", "Map/Window", "Map/Spotlight", "Map/Edge pointers", "Map/Measure",
        "Markers/Visibility and interaction", "Markers/Game icon scale", "Markers/Marker colours and size",
        "Markers/Legacy zoom scale", "Markers/Player", "Markers/Gratis player", "Markers/Pins and notes",
        "Chrome & motion/Map opacity", "Chrome & motion/Chrome visibility", "Chrome & motion/Toolbar visibility",
        "Chrome & motion/Gratis toolbar", "Chrome & motion/Classic toolbar", "Chrome & motion/Title",
        "Chrome & motion/Gratis title", "Chrome & motion/Classic title", "Chrome & motion/Coordinates",
        "Chrome & motion/Gratis coordinates", "Chrome & motion/Classic coordinates", "Chrome & motion/Chrome layout",
        "Chrome & motion/Hover cards", "Chrome & motion/Motion",
    ];
}
