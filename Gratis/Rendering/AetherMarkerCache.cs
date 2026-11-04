using System;
using System.Collections.Generic;
using Gratis.Classes;

namespace Gratis.Rendering;

public sealed class AetherMarkerCache {
    private AetherCurrentPoint[]? snapshot;
    private uint map;
    public MarkerInfo[] Markers { get; private set; } = [];
    private readonly HashSet<uint> submitted = new();
    public void BeginFrame() => submitted.Clear();
    public void Submitted(uint eobj) => submitted.Add(eobj);
    public bool ShouldDraw(uint eobj) => !submitted.Contains(eobj);
    public void Update(uint mapId, AetherCurrentPoint[] points) {
        if (map == mapId && ReferenceEquals(snapshot, points)) return;
        map = mapId;
        snapshot = points;
        Markers = new MarkerInfo[points.Length];
        for (var i = 0; i < points.Length; i++)
            Markers[i] = new MarkerInfo {
                Source = MapMarkerSource.NativeEvent, IconId = 60653, Position = default, Offset = default, Scale = 1,
                IconFamily = MarkerIconFamily.AetherCurrent,
                PrimaryText = static () => "Aether current",
                SecondaryText = static () => "Not yet collected",
            };
    }
}

