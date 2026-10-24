using System.Drawing;
using System.Numerics;
using System.Text.Json.Serialization;
#if !GRATIS_PREVIEW
using KamiLib.Configuration;
#endif

namespace Gratis.Rendering;

public class MapDrawingConfig
#if !GRATIS_PREVIEW
    : CharacterConfiguration
#endif
{
    public bool ScaleWithFrame = true;
    public bool ClampToMap = true;

    public bool QuestSpotlight = true;
    public float SpotlightDim = .35f;
    public bool SpotlightPulse = true;
    public float SpotlightSeconds = 8;
    public bool SpotlightIncludePlayer = true;
    public bool SpotlightDistanceChip = true;
    public bool SpotlightGuideLine = false;

    public void ResetViewport() => ScaleWithFrame = ClampToMap = true;

    public MarkerSettings Markers = new();
    public string ActiveLens = "Explore";
    public global::System.Collections.Generic.List<Lens> CustomLenses = [];
    public bool LensTintsMap = false;
    public bool LensAuto = false;
    public int LensCycleKey = 0;
    public MarkerPeekKey PeekKey = MarkerPeekKey.RightAlt;
    public void ResetMarkers() => Markers = new MarkerSettings();

    public bool ReduceMotion = false;
    public MotionSettings Motion = new();
    public void ResetMotion() { ReduceMotion = false; Motion = new MotionSettings(); }

    public int VisualStyle = 0;
    public int MarkerStyle = 1;
    public int HoverCardStyle = 0;
    public Vector4 AccentColor = GratisTheme.Accent;
    public float FlatMarkerSize = 14;
    public bool HoverCardSwatch = true;
    public Vector4 PoiMarkerColor = GratisTheme.Poi;
    public Vector4 TransportMarkerColor = GratisTheme.Transport;
    public Vector4 ObjectiveMarkerColor = GratisTheme.Objective;
    public Vector4 LinkMarkerColor = GratisTheme.Link;
    public Vector4 ServiceMarkerColor = GratisTheme.Service;
    public Vector4 PartyMarkerColor = GratisTheme.Party;
    public bool FlatPlayer = true;
    public bool PlayerInnerHighlight = true;
    public float SoftConeLength = 110;
    public float SoftConeOpacity = .35f;
    public bool SegmentedToolbar = true;
    public float IconSize = 16;
    public bool AutoHideChrome = true;
    public float ChromeIdleDelay = 1.2f;
    public float ChromeFadeDurationMs = 180;
    public float ChromeTitleMinAlpha = .35f;
    public float ChromeControlsMinAlpha = 0;
    public bool PinChrome = false;

    public void ResetChromeVisibility()
    {
        var defaults = new MapDrawingConfig();
        AutoHideChrome = defaults.AutoHideChrome;
        ChromeIdleDelay = defaults.ChromeIdleDelay;
        ChromeFadeDurationMs = defaults.ChromeFadeDurationMs;
        ChromeTitleMinAlpha = defaults.ChromeTitleMinAlpha;
        ChromeControlsMinAlpha = defaults.ChromeControlsMinAlpha;
        PinChrome = defaults.PinChrome;
    }

    public bool NeutralFrame = true;
    public FramePreset FramePreset = FramePreset.Hairline;
    public void ResetFramePreset() => FramePreset = global::Gratis.Rendering.FramePreset.Hairline;
    public float FlatCornerRadius = 10;
    public bool CompactTitle = true;
    public bool StyledCoordinates = true;
    public int FrameFont = 0;
    public bool FeatherEnabled = true;
    public float FeatherWidth = 6;
    // FeatherShape is derived from MapCircle / MapShape.Rounding; no second shape to desynchronize.
    [JsonIgnore] public string FeatherShape => MapCircle ? "Circle" : "Rounded rectangle";
    public bool RingEnabled = true;
    public float RingThickness = 2;
    public Vector4 RingColorA = new(1, 1, 1, .12f);
    public Vector4 RingColorB = new(1, 1, 1, .12f);
    public Vector4 RingAccentColor = new(1, 1, 1, .95f);
    public float RingAngle = -90;
    public float RingSweepWidth = 70;
    public float RingIntensity = .55f;
    public bool RingRotate = true;
    public float RingRotationSpeed = 6;
    public bool GlassEnabled = true;
    public bool GlassBlur = true;
    public float GlassBlurRadius = 12;
    public float GlassTintOpacity = .04f;
    public float GlassHighlight = .50f;
    public bool GlassGrain = false;
    public float GlassGrainIntensity = .018f;

    public void ResetFrameMaterials()
    {
        var defaults = new MapDrawingConfig();
        ResetFramePreset();
        FeatherEnabled = defaults.FeatherEnabled;
        FeatherWidth = defaults.FeatherWidth;
        RingEnabled = defaults.RingEnabled;
        RingThickness = defaults.RingThickness;
        RingColorA = defaults.RingColorA;
        RingColorB = defaults.RingColorB;
        RingAccentColor = defaults.RingAccentColor;
        RingAngle = defaults.RingAngle;
        RingSweepWidth = defaults.RingSweepWidth;
        RingIntensity = defaults.RingIntensity;
        RingRotate = defaults.RingRotate;
        RingRotationSpeed = defaults.RingRotationSpeed;
        GlassEnabled = defaults.GlassEnabled;
        GlassBlur = defaults.GlassBlur;
        GlassBlurRadius = defaults.GlassBlurRadius;
        GlassTintOpacity = defaults.GlassTintOpacity;
        GlassHighlight = defaults.GlassHighlight;
        GlassGrain = defaults.GlassGrain;
        GlassGrainIntensity = defaults.GlassGrainIntensity;
    }

    [JsonIgnore] public bool IsGratis => VisualStyle == 0;
    [JsonIgnore] public bool UseCustomWindowChrome => HideWindowFrame || (IsGratis && NeutralFrame);

    public bool ShowCoordinateBar = true;
    public float CoordinateBarFade = .66f;
    public Vector4 CoordinateTextColor = Vector4.One;

    public float IconScale = 0.50f;
    public bool FollowPlayer = true;
    public bool FollowReturnToPlayer = true;
    public FollowReturnTrigger FollowReturnTrigger = FollowReturnTrigger.WhenPlayerMoves;
    public float FollowReturnIdleSeconds = 3;
    public float FollowReturnEaseMs = 350;
    public bool ScalePlayerCone = false;
    public float ConeSize = 150.0f;
    public bool HideWindowFrame = false;
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
    public bool ScaleWithZoom = true;
    public Vector4 AreaColor = ColorVector(KnownColor.CornflowerBlue) with { W = 0.33f };
    public Vector4 AreaOutlineColor = ColorVector(KnownColor.CornflowerBlue) with { W = 0.30f };
    public Vector4 PlayerConeColor = ColorVector(KnownColor.CornflowerBlue) with { W = 0.33f };
    public Vector4 PlayerConeOutlineColor = ColorVector(KnownColor.CornflowerBlue) with { W = 1.0f };
    public float ToolbarFade = 0.33f;
    public bool ShowPlayerIcon = true;
    public float PlayerIconScale = 1.0f;
    public PlayerArrowSmoothing PlayerArrowSmoothing = global::Gratis.Rendering.PlayerArrowSmoothing.Medium;
    [JsonIgnore]
    public bool DebugMode = false;

    protected static Vector4 ColorVector(KnownColor color)
    {
        var value = Color.FromKnownColor(color);
        return new Vector4(value.R, value.G, value.B, value.A) / 255.0f;
    }
}
