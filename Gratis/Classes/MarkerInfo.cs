using System;
using System.Numerics;
using Gratis.Rendering;

namespace Gratis.Classes;

public class MarkerInfo {
    public MapMarkerSource Source { get; init; }
    public required Vector2 Position { get; set; }
    public required Vector2 Offset { get; set; }
    public required float Scale { get; set; }
    public uint? ObjectiveId { get; init; }
    public uint? DataId { get; set; }
    public bool? AetheryteAttuned { get; set; }
    public MarkerType MarkerType { get; set; }
    public MarkerCategory Category { get; set; }
    public MarkerIconFamily IconFamily { get; set; }
    public uint IconId { get; set; }
    public Func<string?>? PrimaryText { get; set; }
    public Func<string?>? SecondaryText { get; set; }
    public float? Radius { get; set; }
    public Vector4 RadiusColor { get; set; } = new Vector4(100 / 255.0f, 149 / 255.0f, 237 / 255.0f, 1.0f);
    public Vector4 RadiusOutlineColor { get; set; } = new Vector4(100 / 255.0f, 149 / 255.0f, 237 / 255.0f, 1.0f);
    public Action? OnRightClicked { get; set; }
    public Action? OnLeftClicked { get; set; }
    public bool IsDynamicMarker { get; init; }
    public bool IsRelevant { get; set; } = true;
    public bool IsTarget { get; init; }
    public bool IsFlag { get; init; }
    public bool IsTracked { get; init; }
    public bool IsObjective { get; set; }
    public bool IsHovered { get; internal set; }
    public bool IsSpotlighted { get; internal set; }
    public uint MotionRevision { get; set; }
}