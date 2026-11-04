namespace Gratis.Rendering;

public static partial class UnavailableReason {
    // Called only for a disabled setting; enabled controls have no explanatory tooltip.
    public static string Setting(string path) => path switch {
        "FlatCornerRadius" => "Requires a rectangular map with a neutral frame.",
        "MapColour.Brightness"
        or "MapColour.Contrast"
        or "MapColour.Saturation"
        or "MapColour.HueShift"
        or "MapColour.Tint"
        or "MapColour.TintStrength"
        or "MapColour.GradientEnabled" => "Choose Custom map colours to edit this.",
        "MapColour.Shadow"
        or "MapColour.Mid"
        or "MapColour.Highlight" => "Choose Custom map colours and enable Three-colour map.",
        "FeatherWidth" => "Enable Soften map edge first.",
        "RingThickness"
        or "RingColorA"
        or "RingColorB"
        or "RingAccentColor"
        or "RingAngle"
        or "RingSweepWidth"
        or "RingIntensity"
        or "RingRotate" => "Enable Gradient border first.",
        "RingRotationSpeed" => "Enable Gradient border and Rotate border first.",
        "GlassBlur"
        or "GlassTintOpacity"
        or "GlassHighlight"
        or "GlassGrain" => "Enable Frosted panels first.",
        "GlassBlurRadius" => "Enable Frosted panels and Blur behind panels first.",
        "GlassGrainIntensity" => "Enable Frosted panels and Grain first.",
        "MapRounding" => "Corner radius applies to rectangular maps.",
        "MapBorderColor"
        or "MapBorderThickness" => "Enable the map border first.",
        "MapGlowColor"
        or "MapGlowSize"
        or "MapGlowPulse" => "Enable map glow first.",
        "MapGlowSpeed" => "Enable map glow and set a glow pulse above zero.",
        "RememberMapView"
        or "RememberMapLens"
        or "FitSmallRoomsToContent"
        or "SightseeingLog"
        or "TripleTriadOpponents"
        or "Markers.ShowFateProgress"
        or "Markers.ShowAetherCurrents"
        or "Markers.ShowGatherWindows" => "This setting requires the Gratis visual style.",
        "FollowReturnToPlayer"
        or "Markers.Opacity"
        or "MoveHandle" => "This setting requires the Gratis visual style.",
        "FollowReturnTrigger" => "Use the Gratis style and enable Return to player after dragging.",
        "FollowReturnIdleSeconds" => "Enable Return to player after dragging and choose the idle trigger in Gratis.",
        "FollowReturnEaseMs" => "Enable Return to player after dragging and animations in Gratis.",
        "QuestLogKeepOpenOnShow" => "Unpin the quest log to change this.",
        "OnlyIfViewingCurrentZone"
        or "SwitchInDuties" => "Enable automatic map switching first.",
        "ScaleTextWithZoom"
        or "LargeAreaTextScale"
        or "SmallAreaTextScale" => "Enable text labels first.",
        "LargeAreaTextOpacity"
        or "SmallAreaTextOpacity"
        or "HideOverlappingLabels" => "Enable text labels in the Gratis style.",
        "PlayerIconScale" => "Enable the player icon first.",
        "PlayerInnerHighlight" => "Enable the player icon and flat player arrow first.",
        "SoftConeLength"
        or "SoftConeOpacity" => "Enable the flat player arrow first.",
        "ShowRadarInDuties"
        or "RadarColor"
        or "RadarOutlineColor" => "Enable radar first.",
        "ChromeTitleMinAlpha"
        or "ChromeControlsMinAlpha"
        or "ChromeIdleDelay"
        or "ChromeFadeDurationMs" => "Enable automatic chrome fading and unpin the chrome.",
        "GhostOpacity"
        or "GhostRestoreDelay" => "Enable click-through while moving first.",
        "ShowToolbarOnHover" => "Turn off Always show toolbar first.",
        "IconSize" => "Enable the segmented toolbar first.",
        "ShowAreaLabel"
        or "ShowSubAreaLabel"
        or "ClassicMoveHandle" => "This setting requires the Classic visual style.",
        "LocationCardShows"
        or "TitleCorner"
        or "TitleOffset"
        or "TitleBelowToolbar"
        or "TitleScale"
        or "TitleSeparator"
        or "TitleFont"
        or "TitleBold"
        or "TitleStacked"
        or "TitleColor"
        or "TitleOutline"
        or "TitleBackground" => "Enable the map title first.",
        "TitleSecondaryAbove"
        or "TitleSecondaryScale"
        or "TitleLineSpacing"
        or "TitleSecondaryColor" => "Enable the map title and stacked title first.",
        "TitleOutlineColor" => "Enable the map title and title outline first.",
        "TitleBackgroundColor"
        or "TitlePadding"
        or "TitleRounding"
        or "TitleBorder" => "Enable the map title and title background first.",
        "TitleBorderColor" => "Enable the map title, title background and title border first.",
        "CoordinatesShow"
        or "ShowEorzeaTime"
        or "CoordinateTextColor"
        or "CoordinateBarFade" => "Enable the coordinate bar first.",
        "HoverCardSwatch" => "This setting requires the original hover-card style.",
        "AnimationSpeed"
        or "Motion.Pings"
        or "Motion.PlayerTrail"
        or "Motion.EaseFollow"
        or "Motion.EaseZoom"
        or "Motion.PanInertia"
        or "Motion.ZoneTransition" => "Turn animations on first.",
        "Motion.PingSeconds" => "Turn animations on and enable pings first.",
        "Motion.TrailSeconds"
        or "Motion.TrailOpacity" => "Turn animations on and enable the player trail first.",
        "Motion.PanInertiaStrength" => "Turn animations on and enable drag pan inertia first.",
        "Motion.SettleSeconds" => "Turn animations on and enable eased follow or eased zoom first.",
        "Motion.ZoneSeconds"
        or "Motion.ZoneSlidePixels" => "Turn animations on and enable zone transitions first.",
        "FadePercent" => "Choose a map fading mode first.",
        "BlurStrength" => "Enable background blur first.",
        _ => "This setting is unavailable with the current options.",
    };
}
