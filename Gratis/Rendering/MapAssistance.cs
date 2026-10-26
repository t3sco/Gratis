using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Gratis.Controllers;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class MapAssistance(ITravelProvider provider, ITravelPlaces places, Func<bool> canTravel,
    Action save, Func<string>? blockedReason = null) {
    public ITravelProvider Provider => available ? provider : NullTravelProvider.Instance;

    public ITravelPlaces Places => places;

    private bool inputActive;

    public bool InputActive {
        get => inputActive || LocationMenuDrawing.InputActive;
        private set => inputActive = value;
    }

    public bool OverlayOpen => hovered is not null || PinLayer?.CapturesInput == true || PinLayer?.CardVisible == true;

    public string Message { get; private set; } = "";

    public string DestinationName { get; private set; } = "";

    private PlaceEntry? hovered, pending;

    private Vector2 anchor;

    private PlaceEntry? retainedTravel;

    private Vector4 retainedTravelBounds;

    private double cardCreated, cardUntil, cardDrawn, armedAt;

    private bool playerMoving;

    private Vector2? anchorWorld;

    private bool TravelReady => !MapDrawContext.Current.IsPvP && !playerMoving && canTravel() && !provider.IsBusy;

    public string UnavailableReason {
        get {
            if (MapDrawContext.Current.IsPvP)
                return "Travel unavailable in PvP";
            if (playerMoving)
                return "Stop moving to teleport";
            if (!canTravel())
                return blockedReason?.Invoke() ?? "Teleport unavailable";
            return provider.IsBusy ? "Already travelling" : "";
        }
    }

    private uint pendingCost;

    private bool available;

    private readonly List<(Vector2 Min, Vector2 Max)> panelBounds = [];

    public Vector2 CardMin { get; private set; }

    public Vector2 CardMax { get; private set; }

    public Vector2 TravelButtonCenter { get; private set; }

    public Vector2 CancelButtonCenter { get; private set; }

    public void Prepare(MapDrawingConfig config, bool moving = false) {
        if (MapDrawContext.Current.IsPvP) {
            InputActive = available = false;
            return;
        }
        LocationMenuDrawing.PrepareInput();
        playerMoving = moving;
        InputActive = config.IsGratis && TravelReady
            && panelBounds.Exists(p => ImGui.IsMouseHoveringRect(p.Min, p.Max, false));
        panelBounds.Clear();
        if (!config.IsGratis) {
            PinLayer?.CancelEditor();
            return;
        }
        InputActive |= PinLayer?.CapturesInput == true;
        places.Refresh();
        available = provider.Available;
        if (available && !config.TravelActionsInitialized) {
            config.TravelActions = config.TravelActionsInitialized = true;
            save();
        }
    }

    public bool CanOffer(PlaceEntry place) => TravelReady && MapDrawContext.Current.Config.TravelActions
        && place.Kind == PlaceKind.Aetheryte && places.Access(place).Attuned && (!place.IsShard || available);

    private PlaceEntry? FindTransport(Classes.MarkerInfo marker, string title) =>
        places.Entries.FirstOrDefault(p => p.Kind == PlaceKind.Aetheryte && p.MapId == places.Context.MapId
            && (p.Name == title || p.AetheryteId == marker.DataId));

    public bool HoverTransport(Classes.MarkerInfo marker, string title) {
        if (!MapDrawContext.Current.Config.TravelActions
            || MarkerIcon.Family(marker) is not (MarkerIconFamily.Aetheryte or MarkerIconFamily.Aethernet))
            return false;
        var match = FindTransport(marker, title);
        if (match == null)
            return false;
        if (hovered?.Id == match.Id && TravelReady)
            return true;
        var reason = !places.Access(match).Attuned ? "Not yet attuned" : UnavailableReason;
        if (reason.Length == 0 && match.IsShard && !available)
            reason = "Lifestream unavailable";
        UiMotion.TooltipText(match.Name + (reason.Length > 0 ? " · " + reason : ""));
        return true;
    }

    public void HoverPlace(PlaceEntry place, Vector2 markerCenter) {
        if (!CanOffer(place))
            return;
        pending = null;
        Message = "";
        hovered = place;
        anchor = markerCenter;
        anchorWorld = null;
        cardCreated = cardDrawn = ImGui.GetTime();
        cardUntil = cardCreated + 8;
        contextTarget = null;
    }

    public string Request(PlaceEntry place, bool confirmed = false) {
        if (MapDrawContext.Current.IsPvP) {
            pending = null;
            return Message = "Travel unavailable in PvP";
        }
        places.Refresh();
        if (!CanOffer(place))
            return Message = "Travel is unavailable now.";
        if (!provider.Available && !place.IsShard) {
            Message = places.Teleport(place, confirmed && pending?.Id == place.Id);
            pending = Message.StartsWith("Confirm ", StringComparison.Ordinal) ? place : null;
            pendingCost = places.Access(place).Cost ?? 0;
            return Message;
        }
        var access = places.Access(place);
        if (!place.IsShard && access.Cost == null)
            return Message = "Teleport cost is unavailable.";
        var cost = place.IsShard ? 0 : access.Cost!.Value;
        if (cost >= Math.Max(0, MapDrawContext.Current.Config.TravelConfirmGil) && cost > 0
            && (!confirmed || pending?.Id != place.Id || pendingCost != cost)) {
            pending = place;
            pendingCost = cost;
            return Message = $"Confirm {cost:N0} gil to {place.Name}";
        }
        pending = null;
        var destination = new TravelDestination(place.AetheryteId, place.Name, Route(place), access.Attuned,
            place.IsShard, cost);
        if (!provider.Travel(destination))
            return Message = "Travel request was declined.";
        DestinationName = place.Name;
        return Message = "Travel requested.";
    }

    public static string Route(PlaceEntry place) => place.Subtitle + " > " + place.Name;

    public void Draw(IMapWindowHost host) {
        if (!host.Config.IsGratis || MapDrawContext.Current.IsPvP)
            return;
        OfferClickedObjective(host);
        DrawAetheryteChip(host);
        if (host.Config.TravelActions) {
            if (available)
                DrawProgress(host);
            DrawTravelCard(host);
        }
        PinLayer?.Draw(host, this);
        InputActive = panelBounds.Exists(p => ImGui.IsMouseHoveringRect(p.Min, p.Max, false))
            || PinLayer?.InputActive == true || PinLayer?.CapturesInput == true;
        if (PinLayer?.CapturesInput == true)
            CapturePinKeys?.Invoke();
    }

}