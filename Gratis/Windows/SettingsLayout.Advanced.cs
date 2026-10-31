using System;
using System.Collections.Generic;
using System.Linq;
using Gratis.Rendering;

namespace Gratis.Windows;

public sealed partial class SettingsLayout {
    /// <summary>
    /// Rows hidden unless "Show advanced" is on or the user is searching. Paths only, so renaming a catalog label
    /// never changes what is advanced; CreateEntries throws on a path that is not in the catalog.
    /// </summary>
    private static HashSet<string> AdvancedPaths() {
        string[] advanced = [
            // Appearance
            "MapColour.HueShift", "MapColour.Tint", "MapColour.TintStrength", "MapColour.GradientEnabled",
            "MapColour.Shadow", "MapColour.Mid", "MapColour.Highlight", "FeatherWidth", "RingThickness",
            "RingColorA", "RingColorB", "RingAccentColor", "RingAngle", "RingSweepWidth", "RingIntensity",
            "RingRotate", "RingRotationSpeed", "MapBorder", "MapBorderColor", "MapBorderThickness", "MapGlow",
            "MapGlowColor", "MapGlowSize", "MapGlowPulse", "MapGlowSpeed", "MapVignette",
            // Map
            "TravelConfirmGil", "RememberMapLens", "FollowReturnIdleSeconds", "FollowReturnEaseMs",
            "LockCenterOnMap", "ZoomLocked", "UseLinearZoom", "ZoomSpeed", "AutoZoomScaleFactor", "PaletteImagery",
            "PaletteMaxResults", "QuestLogWidth", "OnlyIfViewingCurrentZone", "SwitchInDuties", "EdgePointerMax",
            "EdgePointerDistance", "ScaleTextWithZoom", "HideOverlappingLabels", "SpotlightDim", "SpotlightPulse",
            "SpotlightSeconds",
            // Filters
            "CustomLenses", "LensTintsMap", "LensCycleKey", "PeekKey",
            // Markers
            "PinMaxPerMap", "PoiMarkerColor", "TransportMarkerColor", "ObjectiveMarkerColor", "LinkMarkerColor",
            "ServiceMarkerColor", "PartyMarkerColor", "ScalePlayerCone", "PlayerInnerHighlight", "SoftConeLength",
            "SoftConeOpacity", "ConeSize", "PlayerConeColor", "PlayerConeOutlineColor", "ShowRadarInDuties",
            "RadarColor", "RadarOutlineColor", "AreaColor", "AreaOutlineColor",
            // Chrome & motion
            "ChromeTitleMinAlpha", "ChromeControlsMinAlpha", "ChromeIdleDelay", "ChromeFadeDurationMs",
            "GhostRestoreDelay", "RestoreOpacityOnHover", "ToolbarAnchor", "LocationAnchor", "CoordinateAnchor",
            "TravelCardAnchor", "SegmentedToolbar", "IconSize", "ToolbarFade", "TitleCorner", "TitleOffset",
            "TitleBelowToolbar", "TitleScale", "TitleSeparator", "TitleFont", "TitleBold", "TitleStacked",
            "TitleSecondaryAbove", "TitleSecondaryScale", "TitleLineSpacing", "TitleSecondaryColor", "TitleColor",
            "TitleOutline", "TitleOutlineColor", "TitleBackground", "TitleBackgroundColor", "TitlePadding",
            "TitleRounding", "TitleBorder", "TitleBorderColor", "ShowUpdateNotice", "ShowFeedbackButton",
            "ShowRegionLabel", "ShowMapLabel", "ShowAreaLabel", "ShowSubAreaLabel",
            "CoordinateTextColor", "CoordinateBarFade", "HoverCardStyle", "HoverCardSwatch", "FlagArrivalRadius",
            "Motion.Pings", "Motion.PingSeconds", "Motion.TrailSeconds", "Motion.TrailOpacity", "Motion.EaseFollow",
            "Motion.EaseZoom", "Motion.PanInertiaStrength", "Motion.SettleSeconds", "Motion.ZoneTransition",
            "Motion.ZoneSeconds", "Motion.ZoneSlidePixels",
            // Advanced
            "ClassicMoveHandle", "HideWindowFrame", "HideWindowBackground", "BlurBackground", "BlurStrength", "DebugMode",
            "TextPixelSnap", "TextOversampleH",
        ];
        return [.. advanced, .. Enum.GetValues<MarkerGroup>().Select(group => "Filters." + group)];
    }
}
