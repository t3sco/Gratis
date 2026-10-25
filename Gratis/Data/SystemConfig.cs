using System;
using System.ComponentModel;
using System.Drawing;
using System.Numerics;
#if !GRATIS_PREVIEW
using Gratis.Compat.Configuration;
#endif

namespace Gratis.Data;

public enum CenterTarget {
    [Description("Disabled")]
    Disabled = 0,

    [Description("Player")]
    Player = 1,

    [Description("Map")]
    Map = 2,
}

[Flags]
public enum FadeMode {
    [Description("Always")]
    Always = 1 << 0,

    [Description("When Moving")]
    WhenMoving = 1 << 2,

    [Description("When Focused")]
    WhenFocused = 1 << 3,

    [Description("When Unfocused")]
    WhenUnFocused = 1 << 4,
}

public class SystemConfig : Gratis.Rendering.MapDrawingConfig {
    public Gratis.Rendering.MapColourSettings MapColour = new();

    public bool UseLinearZoom = false;
    public float ZoomSpeed = 0.085f;
    public bool ShowMiscTooltips = true;
    public bool HideWithGameGui = true;
    public bool HideBetweenAreas = true;
    public bool HideInCombat = false;
    public bool KeepOpen = false;
    public bool FollowOnOpen = true;
    public bool AutoSwitchOnZoneChange = true;
    public bool OnlyIfViewingCurrentZone = true;
    public bool SwitchInDuties = true;
    public CenterTarget CenterOnOpen = CenterTarget.Player;
    public bool ShowRadar = false;
    public bool ShowRadarInDuties = false;
    public Vector4 RadarColor = ColorVector(KnownColor.Gray) with { W = 0.10f };
    public Vector4 RadarOutlineColor = ColorVector(KnownColor.Gray) with { W = 0.30f };
    public bool HideWindowBackground = true;
    public bool BlurBackground = false;
    public float BlurStrength = 0.5f;
    public bool EnableShiftDragMove = false;
    public bool LockWindow = false;
    public bool MoveHandle = true;
    public bool AltDragMovesWindow = true;
    public bool MoveHandleSnap = true;
    public bool ClassicMoveHandle = false;
    public float FadePercent = 0.81f;
    public FadeMode FadeMode = FadeMode.WhenUnFocused;
    public Vector2 WindowPosition = new(1024.0f, 700.0f);
    public Vector2 WindowSize = new(500.0f, 500.0f);
    public bool AlwaysShowToolbar = false;
    public bool ShowToolbarOnHover = true;
    public bool AcceptedSpoilerWarning = false;
    public bool CenterOnFlag = true;
    public bool CenterOnGathering = true;
    public bool CenterOnQuest = true;
    public bool LockCenterOnMap = false;
    public bool ZoomLocked = false;
    public bool ShowPlayers = true;
    public bool SetFlagOnFateClick = false;
    public float MapScale = 1.0f;
    public bool AutoZoom = false;
    public bool ShowRegionLabel = true;
    public bool ShowMapLabel = true;
    public bool ShowAreaLabel = true;
    public bool ShowSubAreaLabel = true;
    public bool NoFocusOnAppear = false;
    public float LargeAreaTextScale = 1.0f;
    public float SmallAreaTextScale = 0.5f;
    public float LargeAreaTextOpacity = 0.30f;
    public float SmallAreaTextOpacity = 1.0f;
    public bool ShowTextLabels = true;
    public bool ShowFogOfWar = false;
    public bool ScaleTextWithZoom = false;
    public float AutoZoomScaleFactor = 0.38f;

    public bool SuppressNativeMapOpenSound = true;

#if !GRATIS_PREVIEW
    public static SystemConfig Load() => Service.PluginInterface.LoadConfigFile<SystemConfig>("System.config.json");

    public static void Save() => Service.PluginInterface.SaveConfigFile("System.config.json", System.SystemConfig);
#endif
}