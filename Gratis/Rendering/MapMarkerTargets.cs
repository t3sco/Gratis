using System;
using System.Collections.Generic;
using System.Numerics;
using Gratis.Classes;
using Gratis.Controllers;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed class MapMarkerTargets {
    public List<(MarkerInfo Marker, Vector2 World)> Targets { get; } = [];
    private MapRendererFrame? renderer;
    public uint MapId { get; private set; }
    public void Begin(MapRendererFrame frame) {
        renderer = frame;
        MapId = frame.ViewedMapId;
        Targets.Clear();
    }

    public void Observe(MarkerInfo marker) {
        if (renderer == null || !MapDrawContext.Current.Config.IsGratis
            || MapDrawContext.Current.IsPvP && PvpMapPolicy.IsPlayer(marker))
            return;
        Targets.Add((marker, renderer.ScreenToWorld(ImGui.GetWindowPos() + marker.Position + marker.Offset,
            ImGui.GetWindowPos())));
    }
    public IEnumerable<PlaceEntry> Entries {
        get {
            if (renderer == null)
                yield break;
            var seen = new Dictionary<string, int>();
            for (var i = 0; i < Targets.Count; i++) {
                var (marker, world) = Targets[i];
                if (MapDrawContext.Current.IsPvP && PvpMapPolicy.IsPlayer(marker))
                    continue;
                var id = StableId(marker, world);
                seen[id] = seen.TryGetValue(id, out var count) ? count + 1 : 0;
                yield return new PlaceEntry(seen[id] == 0 ? id : $"{id}:{seen[id]}",
                    marker.IsFlag ? PlaceKind.Flag : MarkerIcon.Family(marker) == MarkerIconFamily.Quest
                        ? PlaceKind.Quest : PlaceKind.Marker,
                    marker.PrimaryText?.Invoke() ?? MarkerIcon.Family(marker).ToString(), "Current map", MapId, 0,
                    world) { Icon = marker.IconId };
            }
        }
    }

    // Derived from what the marker is, not where it sits in this frame's draw order.
    private string StableId(MarkerInfo marker, Vector2 world) =>
        $"frame:{MapId}:{(int)marker.MarkerType}:{marker.IconId}:"
        + (marker.ObjectiveId ?? marker.DataId) switch {
            { } id => id.ToString(),
            _ => marker.IsFlag ? "flag" : $"{MathF.Round(world.X):0}_{MathF.Round(world.Y):0}",
        };
}