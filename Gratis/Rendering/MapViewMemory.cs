using System;
using System.Collections.Generic;
using System.Numerics;

namespace Gratis.Rendering;

public sealed class RememberedMapView
{
    public uint MapId;
    public float Scale = 1, FrameExtent = 480;
    public Vector2? Offset;
    public string Lens = "Explore";

    public float ResolveScale(MapDrawingConfig config, Vector2 frame, Vector2 texture) =>
        ViewportMath.Constrain(config, frame, texture,
            Math.Clamp(Scale * (config.ScaleWithFrame ? ViewportMath.FrameExtent(frame) / MathF.Max(1, FrameExtent) : 1), .05f, 100), Vector2.Zero).Scale;
}

public sealed class MapViewMemory
{
    public const int Capacity = 100;
    private List<RememberedMapView>? loaded;

    public List<RememberedMapView> Normalize(List<RememberedMapView>? source)
    {
        if (source is not null && ReferenceEquals(source, loaded)) return source;
        var safe = new List<RememberedMapView>();
        if (source is not null) foreach (var entry in source) {
            if (entry is null || entry.MapId == 0 || !float.IsFinite(entry.Scale) || entry.Scale is < .05f or > 100 ||
                !float.IsFinite(entry.FrameExtent) || entry.FrameExtent is < 1 or > 32768) continue;
            if (entry.Offset is { } offset && (!float.IsFinite(offset.X) || !float.IsFinite(offset.Y) ||
                MathF.Abs(offset.X) > 100000 || MathF.Abs(offset.Y) > 100000)) entry.Offset = null;
            if (string.IsNullOrWhiteSpace(entry.Lens) || entry.Lens.Length > 80) entry.Lens = "Explore";
            safe.RemoveAll(value => value.MapId == entry.MapId);
            safe.Add(entry);
            if (safe.Count > Capacity) safe.RemoveAt(0);
        }
        return loaded = safe;
    }

    public RememberedMapView? Find(MapDrawingConfig config, uint mapId, bool touch = false)
    {
        var entries = config.MapViews = Normalize(config.MapViews);
        var index = entries.FindIndex(value => value.MapId == mapId);
        if (index < 0) return null;
        var entry = entries[index];
        if (touch) { entries.RemoveAt(index); entries.Add(entry); }
        return entry;
    }

    public bool Capture(MapDrawingConfig config, uint mapId, float scale, Vector2 offset, Vector2 frame)
    {
        if (!config.IsGratis || (!config.RememberMapView && !config.RememberMapLens) || mapId == 0 ||
            !float.IsFinite(scale) || scale is < .05f or > 100 || !float.IsFinite(offset.X) || !float.IsFinite(offset.Y)) return false;
        var previous = Find(config, mapId);
        var pan = config.FollowPlayer ? null : (Vector2?)offset;
        var extent = ViewportMath.FrameExtent(frame);
        if (previous is not null && previous.Scale == scale && previous.Offset == pan &&
            previous.FrameExtent == extent && previous.Lens == config.ActiveLens) return false;
        config.MapViews.RemoveAll(value => value.MapId == mapId);
        config.MapViews.Add(new RememberedMapView { MapId = mapId, Scale = scale, Offset = pan,
            FrameExtent = extent, Lens = config.ActiveLens ?? "Explore" });
        if (config.MapViews.Count > Capacity) config.MapViews.RemoveAt(0);
        return true;
    }
}
