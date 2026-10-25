using System;
using System.Numerics;
using Gratis.Controllers;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class MapAssistance {
    private PlaceEntry? pressedPlace;

    private SpotlightRequest? pressedObjective;

    private Vector2 pressPosition, pressedWorld;

    private bool markerDragged;

    private void OfferClickedObjective(IMapWindowHost host) {
        var context = MapDrawContext.Current;
        if (!TravelReady || !host.Config.TravelActions) {
            hovered = pending = pressedPlace = null;
            pressedObjective = null;
            return;
        }
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
            pressedPlace = null;
            pressedObjective = null;
            markerDragged = false;
            pressPosition = ImGui.GetMousePos();
            if (InputActive || context.WindowMoveInputBlocked || context.Measure.InputActive
                || !ImGui.IsWindowHovered(
                    ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem))
                return;
            foreach (var (marker, world) in context.MarkerTargets.Targets) {
                if (!marker.IsHovered)
                    continue;
                if (MarkerIcon.Family(marker) is MarkerIconFamily.Aetheryte or MarkerIconFamily.Aethernet) {
                    var place = FindTransport(marker, marker.PrimaryText?.Invoke() ?? "");
                    if (place != null && CanOffer(place)) {
                        pressedPlace = place;
                        pressedWorld = world;
                        break;
                    }
                }
                if (host.Config.AetheryteChips
                    && (marker.IsObjective || MarkerIcon.Family(
                        marker) is MarkerIconFamily.Quest or MarkerIconFamily.Leve)) {
                    pressedObjective = new(world, host.Renderer.ViewedMapId, SpotlightSource.Quest,
                        Icon: marker.IconId);
                    pressedWorld = world;
                    break;
                }
            }
        }
        markerDragged |= Vector2.DistanceSquared(pressPosition,
            ImGui.GetMousePos()) > MathF.Pow(ImGui.GetIO().MouseDragThreshold, 2);
        if (!ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            return;
        if (!markerDragged) {
            if (pressedPlace is { } place) {
                HoverPlace(place, pressPosition);
                anchorWorld = pressedWorld;
            }
            else if (pressedObjective is { } objective) {
                hovered = null;
                OfferContextTarget(objective, host.Now);
            }
        }
        pressedPlace = null;
        pressedObjective = null;
    }
}
