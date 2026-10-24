using System;
using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using System.Text.Json.Serialization;
using Dalamud.Interface;
using KamiLib.Configuration;

namespace Mappy.Data;

public enum CenterTarget
{
    [Description("Disabled")]
    Disabled = 0,

    [Description("Player")]
    Player = 1,

    [Description("Map")]
    Map = 2,
}

[Flags]
public enum FadeMode
{
    [Description("Always")]
    Always = 1 << 0,

    [Description("When Moving")]
    WhenMoving = 1 << 2,

    [Description("When Focused")]
    WhenFocused = 1 << 3,

    [Description("When Unfocused")]
    WhenUnFocused = 1 << 4,
}

public class SystemConfig : CharacterConfiguration
{
    public bool UseLinearZoom = false;
    public float ZoomSpeed = 0.25f;
    public float IconScale = 0.50f;
    public bool ShowMiscTooltips = true;
    public bool HideWithGameGui = true;
    public bool HideBetweenAreas = false;
    public bool HideInCombat = false;
    public bool KeepOpen = false;
    public bool FollowOnOpen = false;
    public bool FollowPlayer = true;
    public CenterTarget CenterOnOpen = CenterTarget.Disabled;
    public bool ScalePlayerCone = false;
    public float ConeSize = 150.0f;
    public bool ShowRadar = true;
    public bool ShowRadarInDuties = false;
    public Vector4 RadarColor = KnownColor.Gray.Vector() with { W = 0.10f };
    public Vector4 RadarOutlineColor = KnownColor.Gray.Vector() with { W = 0.30f };
    public bool HideWindowFrame = false;
    public bool HideWindowBackground = false;
    public bool ShowTitleOnMap = true;
    public int TitleCorner = 1;
    public Vector2 TitleOffset = new(8.0f, 8.0f);
    public bool TitleBelowToolbar = true;
    public float TitleScale = 1.0f;
    public string TitleSeparator = " - ";
    public int TitleFont = 1;
    public float TitleBold = 0.5f;
    public bool TitleStacked = true;
    public bool TitleSecondaryAbove = true;
    public float TitleSecondaryScale = 0.8f;
    public float TitleLineSpacing = 2.0f;
    public Vector4 TitleSecondaryColor = new(0.85f, 0.85f, 0.85f, 0.7f);
    public Vector4 TitleColor = new(1.0f, 1.0f, 1.0f, 1.0f);
    public bool TitleOutline = true;
    public Vector4 TitleOutlineColor = new(0.0f, 0.0f, 0.0f, 0.8f);
    public bool TitleBackground = true;
    public Vector4 TitleBackgroundColor = new(0.08f, 0.07f, 0.06f, 0.65f);
    public Vector2 TitlePadding = new(8.0f, 4.0f);
    public float TitleRounding = 6.0f;
    public bool TitleBorder = false;
    public Vector4 TitleBorderColor = new(1.0f, 1.0f, 1.0f, 0.25f);
    public bool BlurBackground = false;
    public bool MapCircle = false;
    public float MapRounding = 0.0f;
    public bool MapBorder = false;
    public Vector4 MapBorderColor = new(0.35f, 0.70f, 1.00f, 0.90f);
    public float MapBorderThickness = 2.0f;
    public bool MapGlow = false;
    public Vector4 MapGlowColor = new(0.30f, 0.65f, 1.00f, 0.55f);
    public float MapGlowSize = 14.0f;
    public float MapGlowPulse = 0.35f;
    public float MapGlowSpeed = 1.5f;
    public float MapVignette = 0.0f;
    public float BlurStrength = 3.0f;
    public bool EnableShiftDragMove = false;
    public bool LockWindow = false;
    public float FadePercent = 0.60f;
    public FadeMode FadeMode = FadeMode.WhenUnFocused | FadeMode.WhenMoving;
    public Vector2 WindowPosition = new(1024.0f, 700.0f);
    public Vector2 WindowSize = new(500.0f, 500.0f);
    public bool AlwaysShowToolbar = false;
    public bool ShowToolbarOnHover = true;
    public bool ScaleWithZoom = true;
    public bool AcceptedSpoilerWarning = false;
    public Vector4 AreaColor = KnownColor.CornflowerBlue.Vector() with { W = 0.33f };
    public Vector4 AreaOutlineColor = KnownColor.CornflowerBlue.Vector() with { W = 0.30f };
    public Vector4 PlayerConeColor = KnownColor.CornflowerBlue.Vector() with { W = 0.33f };
    public Vector4 PlayerConeOutlineColor = KnownColor.CornflowerBlue.Vector() with { W = 1.0f };
    public bool CenterOnFlag = true;
    public bool CenterOnGathering = true;
    public bool CenterOnQuest = true;
    public bool LockCenterOnMap = false;
    public bool ShowCoordinateBar = true;
    public float ToolbarFade = 0.33f;
    public float CoordinateBarFade = 0.66f;
    public Vector4 CoordinateTextColor = KnownColor.White.Vector();
    public bool ZoomLocked = false;
    public bool ShowPlayers = true;
    public bool SetFlagOnFateClick = false;
    public bool ShowPlayerIcon = true;
    public float PlayerIconScale = 1.0f;
    public float MapScale = 1.0f;
    public bool AutoZoom = false;
    public bool ShowRegionLabel = true;
    public bool ShowMapLabel = true;
    public bool ShowAreaLabel = true;
    public bool ShowSubAreaLabel = true;
    public bool NoFocusOnAppear = false;
    public float LargeAreaTextScale = 1.5f;
    public float SmallAreaTextScale = 1.0f;
    public bool ShowTextLabels = true;
    public bool ShowFogOfWar = true;
    public bool ScaleTextWithZoom = true;
    public float AutoZoomScaleFactor = 0.33f;

    public bool SuppressNativeMapOpenSound = true;

    [JsonIgnore]
    public bool DebugMode = false;

    public static SystemConfig Load() => Service.PluginInterface.LoadConfigFile<SystemConfig>("System.config.json");

    public static void Save() => Service.PluginInterface.SaveConfigFile("System.config.json", System.SystemConfig);
}