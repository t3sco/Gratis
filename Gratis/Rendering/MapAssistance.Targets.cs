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

    // A quest marker opens that quest in the Gratis quest log; only markers the log cannot place fall back to travel.
    private uint pressedQuest;

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
            pressedQuest = 0;
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
                    pressedQuest = MarkerIcon.Family(marker) is MarkerIconFamily.Quest && marker.ObjectiveId is { } quest
                        && quest != 0 ? quest + 65536u : 0;
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
            else if (pressedQuest != 0 && context.QuestLog.OpenQuest(pressedQuest))
                hovered = null;
            else if (pressedObjective is { } objective) {
                hovered = null;
                OfferContextTarget(objective, host.Now);
            }
        }
        pressedPlace = null;
        pressedObjective = null;
    }
}
