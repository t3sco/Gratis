using System.Numerics;
using Gratis.Classes;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal sealed record ScriptFrame(double Time, Vector2? Mouse = null, bool? Left = null, bool? Right = null,
    float Wheel = 0, bool? Focused = null, bool? Shift = null, bool? Control = null, Vector2? Player = null,
    float? Heading = null, bool? SettingsOpen = null, Vector2? Size = null, uint? MapId = null, int? Redraws = null,
    string? Screenshot = null, bool? Alt = null, ScriptQuestLink? QuestLink = null, bool Escape = false,
    uint? TerritoryId = null, ImGuiKey[]? Keys = null, string? Text = null, bool? Moving = null, bool MapKey = false,
    SubZoneLocation? SubZone = null, long? UnixTime = null, float? CameraYaw = null, float? NativeConeDegrees = null,
    bool? IsPvP = null, bool? TextureReady = null, Vector2? TextureSize = null, bool? ContentReady = null,
    MapContentBounds? Content = null, Vector2? ViewportSize = null, float? Dpi = null);

internal sealed record ScriptQuestLink(Vector2? Target, uint MapId, bool SameMap = true, uint? Icon = null);

internal sealed class FrameScript {
    public int Width { get; set; } = 960;
    public int Height { get; set; } = 640;
    public float Dpi { get; set; } = 1;
    public bool Palette { get; set; }
    public SubZoneLocation? SubZone { get; set; }
    public Vector2 ScreenOrigin { get; set; }
    public Vector2? DrawOffset { get; set; }
    public Vector2? PlayerCoordinates { get; set; }
    public Vector2? CursorCoordinates { get; set; }
    public string? Config { get; set; }
    public string? Texture { get; set; }
    public bool SmallInstance { get; set; }
    public bool AutoFit { get; set; }
    public bool InstanceMap { get; set; }
    public uint MapId { get; set; } = 1;
    public uint TerritoryId { get; set; } = 1;
    public Vector2 MapOffset { get; set; }
    public float MapSizeFactor { get; set; } = 1;
    public Vector2 Player { get; set; }
    public float Heading { get; set; } = .25f;
    public float? CameraYaw { get; set; }
    public float? NativeConeDegrees { get; set; }
    public string Title { get; set; } = "Eorzea - La Noscea - Middle La Noscea###GratisMapWindow";
    public List<ScriptMarker> Markers { get; set; } = [];
    public List<ScriptFrame> Frames { get; set; } = [];
}

internal sealed record ScriptMarker(uint Icon, Vector2 World, MarkerType Kind = MarkerType.Unknown,
    bool Objective = false, bool Relevant = true, MapMarkerSource Source = MapMarkerSource.Unspecified);

internal readonly record struct FrameSnapshot(double Time, int Frame, float Scale, Vector2 Offset, Vector2 DrawPosition,
    Vector2 Viewport, float ChromeProgress, FollowState Follow, bool Dragging, HoverFlags Hover, int MotionAdvances,
    int TexturePasses, int Widgets, int IdCollisions, int DuplicateChrome, int Blurred, int Refused, string ToolbarBlur,
    string TitleBlur, string CoordinatesBlur, int MarkersDrawn, int QuestsDrawn, int UnknownObjectives, uint MapId,
    float? PendingZoom, int ClipRefusals, int CircularRefusals, int AlphaRefusals, int ConfigRefusals, int HostRefusals,
    Vector2 WindowPosition, Vector2 WindowSize, bool MovingWindow, float HandleVisibility, Vector2 HandleCenter,
    bool SpotlightActive, float SpotlightOpacity, float SpotlightFitZoom, string SpotlightDecision) {
    public float GhostOpacity { get; init; }
    public bool GhostClickThrough { get; init; }
    public string GhostFlags { get; init; } = "";
    public float SubZoneOpacity { get; init; }
    public string SubZoneText { get; init; } = "";
    public string EorzeaText { get; init; } = "";
}