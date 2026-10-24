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

public sealed class MapMarkerTargets
{
    public List<(MarkerInfo Marker, Vector2 World)> Targets { get; } = [];
    private MapRendererFrame? renderer;
    public void Begin(MapRendererFrame frame) { renderer = frame; Targets.Clear(); }
    public void Observe(MarkerInfo marker)
    {
        if (renderer == null || !MapDrawContext.Current.Config.IsGratis) return;
        Targets.Add((marker, renderer.ScreenToWorld(ImGui.GetWindowPos() + marker.Position + marker.Offset, ImGui.GetWindowPos())));
    }
    public IEnumerable<PlaceEntry> Entries
    {
        get {
            if (renderer == null) yield break;
            for (var i = 0; i < Targets.Count; i++) {
                var (marker, world) = Targets[i];
                yield return new PlaceEntry($"frame:{renderer.ViewedMapId}:{i}", marker.IsFlag ? PlaceKind.Flag :
                    MarkerIcon.Family(marker) == MarkerIconFamily.Quest ? PlaceKind.Quest : PlaceKind.Marker,
                    marker.PrimaryText?.Invoke() ?? MarkerIcon.Family(marker).ToString(), "Current map", renderer.ViewedMapId, 0, world) { Icon = marker.IconId };
            }
        }
    }
}
