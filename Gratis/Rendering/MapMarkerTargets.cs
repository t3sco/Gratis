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
            for (var i = 0; i < Targets.Count; i++) {
                var (marker, world) = Targets[i];
                if (MapDrawContext.Current.IsPvP && PvpMapPolicy.IsPlayer(marker))
                    continue;
                yield return new PlaceEntry($"frame:{MapId}:{i}",
                    marker.IsFlag ? PlaceKind.Flag : MarkerIcon.Family(marker) == MarkerIconFamily.Quest
                        ? PlaceKind.Quest : PlaceKind.Marker,
                    marker.PrimaryText?.Invoke() ?? MarkerIcon.Family(marker).ToString(), "Current map", MapId, 0,
                    world) { Icon = marker.IconId };
            }
        }
    }
}